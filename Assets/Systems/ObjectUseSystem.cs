using System;
using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using EventType = SS.Resources.EventType;

namespace SS.System {
  [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
  public partial struct ObjectUseSystem : ISystem {
    private EntityQuery useQuery;
    
    private ComponentLookup<ObjectInstance> instanceLookup;
    private ComponentLookup<ObjectInstance.Item> itemLookup;
    private ComponentLookup<ObjectInstance.Trigger> triggerLookup;
    private ComponentLookup<ObjectInstance.DoorAndGrating> doorLookup;
    
    private EntityArchetype triggerEventArchetype;

    private TimeData timeData;

    [BurstCompile]
    public void OnCreate(ref SystemState state) {
      state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
      state.RequireForUpdate<Level>();
      state.RequireForUpdate<Hacker>();
      
      useQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<ObjectInstance>()
        .WithPresent<ObjectUseTag>()
        .Build(ref state);
      
      instanceLookup = state.GetComponentLookup<ObjectInstance>();
      itemLookup = state.GetComponentLookup<ObjectInstance.Item>();
      triggerLookup = state.GetComponentLookup<ObjectInstance.Trigger>();
      doorLookup = state.GetComponentLookup<ObjectInstance.DoorAndGrating>();
      
      triggerEventArchetype = state.EntityManager.CreateArchetype(stackalloc[] { 
        ComponentType.ReadWrite<ScheduleEvent>(),
      });
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
      var ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
      
      var level = SystemAPI.GetSingleton<Level>();
      var player = SystemAPI.GetSingleton<Hacker>();
      
      instanceLookup.Update(ref state);
      itemLookup.Update(ref state);
      triggerLookup.Update(ref state);
      doorLookup.Update(ref state);
      
      var eventCommandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
      
      var animationCommandListSystem = state.WorldUnmanaged.GetExistingUnmanagedSystem<AnimationCommandListSystem>();
      //var animateObjectSystemData = state.EntityManager.GetComponentDataRW<AnimateObjectSystemData>(animationCommandListSystem).ValueRW;
      var animationCommandListSystemData = SystemAPI.GetComponent<AnimateObjectSystemData>(animationCommandListSystem); // TODO GetSingleton?
      var animationList = animationCommandListSystemData.AllocateWriter(JobsUtility.ThreadIndexCount, state.WorldUpdateAllocator);
      
      var usedEntities = useQuery.ToEntityArray(Allocator.Temp);

      timeData = SystemAPI.Time;
      
      // Debug.Log($"ObjectUseSystem animationList.Commands.BeginForEachIndex {JobsUtility.ThreadIndex} ");
      animationList.Commands.BeginForEachIndex(JobsUtility.ThreadIndex);
      
      for (var index = 0; index < usedEntities.Length; ++index) {
        var entity = usedEntities[index];
        UseObject(entity, player, ref level, ref eventCommandBuffer, ref animationList, 0);
      }
      
      animationList.Commands.EndForEachIndex();

      // TODO Change to Enable component
      state.EntityManager.RemoveComponent<ObjectUseTag>(usedEntities);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }

    private bool UseObject(in Entity entity, in Hacker player, ref Level level, ref EntityCommandBuffer commandBuffer, ref AnimateObjectSystemData.Writer animationList, byte flags) {
      var instanceData = instanceLookup.GetRefRO(entity).ValueRO;

      if (instanceData.Class == ObjectClass.DoorAndGrating)
        return UseDoor(entity, ref instanceData, player, ref level, ref commandBuffer, ref animationList, (DoorUseFlags)flags);

      return false;
    }
    
    private bool UseDoor(in Entity doorEntity, ref ObjectInstance doorInstance, in Hacker player, ref Level level, ref EntityCommandBuffer commandBuffer, ref AnimateObjectSystemData.Writer animationList, DoorUseFlags useFlags) {
      var cardUsed = false;
      
      var door = doorLookup.GetRefRO(doorEntity).ValueRO;

      if (door.AccessLevel != 0) {
        if (door.AccessLevel == ObjectInstance.DoorAndGrating.COMPAR_ESC_ACCESS) {
          var comparatorEntity = level.ObjectInstances[door.Lock];
          var trigger = triggerLookup.GetRefRO(comparatorEntity).ValueRO;
          
          if (!ComparatorCheck(trigger.Comparator, doorEntity, out var specialCode))
            return (trigger.Comparator >> 24) != 0 && specialCode == 0;
          
          door.AccessLevel = 0;
          door.Lock = 0;
          
          goto accessOk;
        }
        
        var accessBit = 1 << door.AccessLevel;
        for (var i = 0; i < Hacker.NUM_GENERAL_SLOTS; ++i) {
          var inventoryItem = player.GetGeneralInventoryItem(i);
          var inventoryItemEntity = level.ObjectInstances[inventoryItem];
          var inventoryItemInstance = instanceLookup.GetRefRO(inventoryItemEntity).ValueRO;
          if (inventoryItemInstance.Triple == 0x80400 /* GENCARDS_TRIPLE */) {
            var itemInstance = itemLookup.GetRefRO(inventoryItemEntity).ValueRO;

            if ((itemInstance.AccessBits & accessBit) == 0) continue;
            
            cardUsed = true;
            break;
          }
        }

        if (!cardUsed) {
          if ((useFlags & DoorUseFlags.DontShowMessage) == 0)
            Debug.Log($"REF_STR_DoorWrongAccess {door.AccessLevel} {door.LockMessage}");

          return true;
        }
      }
      
      accessOk:

      var allAnimationData = level.Animations.AsReadOnly();
      
      if (ObjectInstance.DoorAndGrating.Closed(doorInstance) || door.IsMoving(allAnimationData, true)) {
        // Open closed door

        if (door.Lock != 0 &&
            player.GetQuestBit(door.Lock) &&
            (player.GetQuestVar(Hacker.MISSION_DIFF_QUEST_VAR) >= 1 || door.Lock == Hacker.DOOR_UNUSABLE_QUEST_VAR || door.Lock == Hacker.DOOR_UNUSABLE2_QUEST_VAR))
        {
          if ((useFlags & DoorUseFlags.DontShowMessage) == 0) {
            if (cardUsed)
              Debug.Log($"REF_STR_DoorCardGoodButLocked {door.AccessLevel} {door.LockMessage}");
            else
              Debug.Log($"REF_STR_DoorLocked {door.AccessLevel} {door.LockMessage}");
          }
        } else {
          if (cardUsed && (useFlags & DoorUseFlags.DontShowMessage) == 0) {
            // TODO Build card use string
            Debug.Log($"REF_STR_DoorCardGood {door.AccessLevel} {door.LockMessage}");
          }
          
          Debug.Log($"DOOR OPEN Animation add! {doorEntity.Index}");
          
          animationList.AddAnimation(door.Link.ObjectIndex, false, false, false, ObjectInstance.DoorAndGrating.ANIM_SPEED, AnimationData.Callback.Null, 0, AnimationData.AnimationCallbackType.Null);
          // TODO Play sound

          doorInstance.Info.Flags &= ~InstanceFlags.BlockRendering;

          if (door is { AutocloseTime: > 0, NeverAutoClose: false }) {
            var gameTicks = TimeUtils.SecondsToFastTicks(timeData.ElapsedTime); // TODO player game_time

            var newCode = (byte)((ObjectInstance.DoorAndGrating.GetAutoCloseCode(doorInstance) + 1) & 0x03);
            
            var scheduleEvent = new ScheduleEvent {
              Timestamp = (ushort)(TimeUtils.FastTicksToTimestamp((uint)(gameTicks + (TimeUtils.CIT_CYCLE * door.AutocloseTime) / ObjectInstance.DoorAndGrating.DOOR_TIME_UNIT)) + 1),
              Type = EventType.Door
            };

            unsafe {
              *((DoorScheduleEvent*)scheduleEvent.Data) = new DoorScheduleEvent {
                ObjectIndex = door.Link.ObjectIndex,
                AutoClose = newCode
              };
            }
            ObjectInstance.DoorAndGrating.SetAutoCloseCode(doorInstance, newCode);

            var eventEntity = commandBuffer.CreateEntity(triggerEventArchetype);
            commandBuffer.SetComponent(eventEntity, scheduleEvent);
          }
        }
      } else {
        Debug.Log($"DOOR CLOSE Animation add! {doorEntity}");
        
        // Close open door
        animationList.AddAnimation(door.Link.ObjectIndex, false, true, false, ObjectInstance.DoorAndGrating.ANIM_SPEED, AnimationData.Callback.Null, 0, AnimationData.AnimationCallbackType.Null);
        // TODO Play sound
      }

      if (door.OtherHalf != 0 && (useFlags & DoorUseFlags.DontUseOtherHalf) == 0) {
        var otherDoorEntity = level.ObjectInstances[door.OtherHalf];
        var otherDoorInstance = instanceLookup.GetRefRO(otherDoorEntity).ValueRO;
        var isDoor = otherDoorInstance.Class == ObjectClass.DoorAndGrating;
          
        var doorFound = doorLookup.TryGetRefRO(otherDoorEntity, out var otherDoorRef);
        if (!isDoor || !doorFound || door.IsMoving(allAnimationData, true) != (ObjectInstance.DoorAndGrating.IsReallyClosed(otherDoorInstance) || otherDoorRef.ValueRO.IsMoving(allAnimationData, true)))
          UseObject(otherDoorEntity, player, ref level, ref commandBuffer, ref animationList, (byte)(isDoor ? useFlags | DoorUseFlags.DontUseOtherHalf : DoorUseFlags.None));
      }
      
      return true;
    }
    
    private readonly bool ComparatorCheck(uint comparator, in Entity entity, out byte specialCode) { // TODO FIXME comparator_check
      specialCode = 0;
      return true;
    }
  }

  public struct ObjectUseTag : IComponentData { }

  [Flags]
  public enum DoorUseFlags : byte {
    None = 0x00,
    DontUseOtherHalf = 0x01,
    DontShowMessage = 0x02,
  }
}