using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using static Unity.Mathematics.math;

namespace SS.System {
  [UpdateInGroup (typeof(FixedStepSimulationSystemGroup))]
  public partial class TriggerSystem : SystemBase {
    private const double NextContinuousSeconds = 5.0;

    private EntityQuery triggerQuery;
    private EntityArchetype triggerEventArchetype;

    private NativeArray<Random> Randoms;

    protected override void OnCreate() {
      base.OnCreate();

      Randoms = new NativeArray<Random>(JobsUtility.MaxJobThreadCount, Allocator.Persistent);
      for (int i = 0; i < Randoms.Length; ++i)
        Randoms[i] = Random.CreateFromIndex((uint)i);

      triggerQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
          ComponentType.ReadOnly<ObjectInstance>(),
          ComponentType.ReadOnly<ObjectInstance.Trigger>(),
          ComponentType.ReadOnly<TriggerActivateTag>()
        }
      });

      triggerEventArchetype = World.EntityManager.CreateArchetype(
        typeof(ScheduleEvent)
      );
    }

    protected override void OnDestroy() {
      base.OnDestroy();

      Randoms.Dispose();
    }

    protected override void OnUpdate() {
      var ecbSystem = World.GetExistingSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
      var commandBuffer = ecbSystem.CreateCommandBuffer();

      var level = GetSingleton<Level>();

      var triggerJob = new TriggerJob {
        entityTypeHandle = GetEntityTypeHandle(),
        instanceTypeHandle = GetComponentTypeHandle<ObjectInstance>(),
        triggerTypeHandle = GetComponentTypeHandle<ObjectInstance.Trigger>(),

        TileMapBlobAsset = level.TileMap,
        ObjectInstancesBlobAsset = level.ObjectInstances,

        Randoms = Randoms,
        TimeData = Time,
        LevelInfo = GetSingleton<LevelInfo>(),
        triggerEventArchetype = triggerEventArchetype,
        
        CommandBuffer = commandBuffer.AsParallelWriter(),

        MapElementFromEntity = GetComponentDataFromEntity<MapElement>(),
        InstanceFromEntity = GetComponentDataFromEntity<ObjectInstance>(),
        TriggerFromEntity = GetComponentDataFromEntity<ObjectInstance.Trigger>(),
      };

      var trigger = triggerJob.ScheduleParallel(triggerQuery, dependsOn: Dependency);
      Dependency = trigger;
      trigger.Complete();
    }

    [BurstCompile]
    struct TriggerJob : IJobEntityBatch {
      [NativeSetThreadIndex] internal readonly int threadIndex;

      [ReadOnly] public EntityTypeHandle entityTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance> instanceTypeHandle;
      [ReadOnly] public ComponentTypeHandle<ObjectInstance.Trigger> triggerTypeHandle;

      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> TileMapBlobAsset;
      [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;

      [NativeDisableContainerSafetyRestriction] public NativeArray<Random> Randoms;
      [ReadOnly] public TimeData TimeData;
      [ReadOnly] public LevelInfo LevelInfo;
      [ReadOnly] public Hacker Player;
      [ReadOnly] public EntityArchetype triggerEventArchetype;

      [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

      [NativeDisableContainerSafetyRestriction] public ComponentDataFromEntity<MapElement> MapElementFromEntity;
      [NativeDisableContainerSafetyRestriction] public ComponentDataFromEntity<ObjectInstance> InstanceFromEntity;
      [NativeDisableContainerSafetyRestriction] public ComponentDataFromEntity<ObjectInstance.Trigger> TriggerFromEntity;
      
      public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
        var entities = batchInChunk.GetNativeArray(entityTypeHandle);
        var instances = batchInChunk.GetNativeArray(instanceTypeHandle);
        var triggers = batchInChunk.GetNativeArray(triggerTypeHandle);

        for (int i = 0; i < batchInChunk.Count; ++i) {
          var entity = entities[i];
          var instance = instances[i];
          var trigger = triggers[i];

          Activate(entity, batchIndex, out bool message);

          CommandBuffer.RemoveComponent<TriggerActivateTag>(batchIndex, entity);
        }
      }

      private bool Activate(in Entity entity, int batchIndex, out bool message) {
        message = false;

        if (TriggerFromEntity.HasComponent(entity)) {
          var instance = InstanceFromEntity[entity];
          var trigger = TriggerFromEntity[entity];

          //Debug.Log($"Activate e:{entity.Index} o:{trigger.Link.ObjectIndex}");

          int comparator = instance.Info.Type switch {
            0 => 0, // DEATHWATCH_TRIGGER_TYPE
            5 => 0, // AREA_ENTRY_TRIGGER_TYPE
            6 => 0, // AREA_CONTINUOUS_TRIGGER_TYPE
            _ => trigger.Comparator
          };

          if (comparatorCheck(comparator, entity, out byte specialCode))
            processTrigger(entity, batchIndex); // TODO Activate actually

          return true;
        } // else fixture
  
        return false;
      }

      private bool comparatorCheck(int comparator, in Entity entity, out byte specialCode) {
        specialCode = 0;
        return true;
      }

      // TODO interfaces
      private unsafe void changeLighting (ref ObjectInstance instance, ref ObjectInstance.Trigger trigger, int batchIndex, bool floor, uint actionParam1, uint actionParam2, uint actionParam3, uint actionParam4) {
        var transitionType = actionParam2 & 0xFFFF;
        var numSteps = transitionType != 0 ? (int)((actionParam2 >> 16) & 0xFFF) : -1;
        byte lightState = (byte)(actionParam2 >> 28); // Are we turning on or off. Last nibble.
        
        var values = stackalloc byte[] {
          (byte)(actionParam4 & 0xFF),
          (byte)((actionParam4 >> 8) & 0xFF),
          (byte)((actionParam4 >> 16) & 0xFF),
          (byte)(actionParam4 >> 24)
        };

        // Debug.Log($"changeLighting ls:{lightState} lt:{actionParam3} tt:{transitionType} ns:{numSteps}");

        int2 rectMin, rectMax;
        if (actionParam3 == 3) { // Radial
          short radius = questDataParse((ushort)(actionParam1 & 0xFFFF));
          rectMin = int2(instance.Location.TileX - radius, instance.Location.TileY - radius);
          rectMax = int2(instance.Location.TileX + radius, instance.Location.TileY + radius);
        } else {
          if (values[0] > 0x0F || values[1] > 0x0F || values[2] > 0x0F || values[3] > 0x0F) return;

          var firstObjID = questDataParse((ushort)(actionParam1 & 0xFFFF));
          var secondObjID = questDataParse((ushort)(actionParam1 >> 16));
          if (firstObjID == 0 || secondObjID == 0) return;

          var firstEntity = ObjectInstancesBlobAsset.Value[firstObjID];
          var secondEntity = ObjectInstancesBlobAsset.Value[secondObjID];
          //if (!InstanceFromEntity.HasComponent(ulEntity) || !InstanceFromEntity.HasComponent(lrEntity)) return;

          var firstObj = InstanceFromEntity[firstEntity];
          var secondObj = InstanceFromEntity[secondEntity];

          rectMin = int2(min(firstObj.Location.TileX, secondObj.Location.TileX), min(firstObj.Location.TileY, secondObj.Location.TileY));
          rectMax = int2(max(firstObj.Location.TileX, secondObj.Location.TileX), max(firstObj.Location.TileY, secondObj.Location.TileY));
        }

        if (numSteps == -1 || numSteps == NUM_LIGHT_STEPS) { // Toggle state if done transitioning
          if (lightState == 0)
            trigger.ActionParam2 |= 0x10000000;
          else
            trigger.ActionParam2 &= 0x0FFFFFFF;
        }

        byte startShade;
        byte endShade;
        byte deltaShade;
        if (actionParam3 == 0) { // All tiles are the same
          startShade = lightState != 0 ? values[0] : values[1];
          endShade = lightState != 0 ? values[1] : values[0];

          // Debug.Log($"All ls:{lightState} start:{startShade} end:{endShade}");

          if (numSteps >= 0 && numSteps < NUM_LIGHT_STEPS)
            startShade -= (byte)(((startShade - endShade) * (NUM_LIGHT_STEPS - numSteps)) / NUM_LIGHT_STEPS);

          deltaShade = 0;
        } else {
          startShade =        lightState != 0 ? values[0] : values[2];
          var startShadeEnd = lightState != 0 ? values[2] : values[0];

          endShade =        lightState != 0 ? values[1] : values[3];
          var endShadeEnd = lightState != 0 ? values[3] : values[1];

          if (numSteps >= 0 && numSteps < NUM_LIGHT_STEPS) {
            startShade -= (byte)(((startShade - startShadeEnd) * (NUM_LIGHT_STEPS - numSteps)) / NUM_LIGHT_STEPS);
            endShade -= (byte)(((endShade - endShadeEnd) * (NUM_LIGHT_STEPS - numSteps)) / NUM_LIGHT_STEPS);
          }

          if (actionParam3 == 1) // EW Smooth
            deltaShade = (byte)((endShade - startShade) / (rectMax.x - rectMin.x));
          else if (actionParam3 == 2) // NS Smooth
            deltaShade = (byte)((endShade - startShade) / (rectMax.y - rectMin.y));
          else // Radial
            deltaShade = (byte)(endShade - startShade);
        }

        var tempLight = startShade;
        for (var y = rectMin.y; y <= rectMax.y; ++y) {
          for (var x = rectMin.x; x <= rectMax.x; ++x) {
            var mapEntity = TileMapBlobAsset.Value[y * LevelInfo.Width + x];
            var mapElement = MapElementFromEntity[mapEntity];
            
            if (actionParam3 == 3) { // Radial
              var delta = length(float2(
                (x << 8 - instance.Location.X) / 255f,
                (y << 8 - instance.Location.Y) / 255f
              ));
              var radius = (float)(actionParam1 & 0xFFFF);
              if (delta <= radius)
                tempLight = (byte)(startShade + (delta/radius) * deltaShade);
              else
                tempLight = floor ? mapElement.ShadeFloorModifier : mapElement.ShadeCeilingModifier;

              if (tempLight > MAX_LIGHT_VAL)
                tempLight = MAX_LIGHT_VAL;
            } else if (actionParam3 == 1) { // EW Smooth
              if (x == rectMax.x-1) // end
                tempLight = endShade;
              else
                tempLight += deltaShade;
            }

            // Debug.Log($"Setting x:{x} y:{y} f:{floor} l:{tempLight}");

            if (floor)
              mapElement.ShadeFloorModifier = tempLight;
            else
              mapElement.ShadeCeilingModifier = tempLight;

            MapElementFromEntity[mapEntity] = mapElement;

            CommandBuffer.AddComponent<LightmapRebuildTag>(batchIndex, mapEntity);
          }

          if (actionParam3 == 1) { // EW Smooth
            tempLight = startShade;
          } else if (actionParam3 == 2) { // NS Smooth
            if (y == rectMax.y-1)
              tempLight = endShade;
            else
              tempLight += deltaShade;
          }
        }
      }

      private const byte NUM_LIGHT_STEPS = 8;
      private const byte MAX_LIGHT_VAL = 15;

      private const byte TRAP_TIME_UNIT = 10;

      private unsafe void processTrigger(in Entity entity, int batchIndex) {
        if (!TriggerFromEntity.HasComponent(entity)) return;

        var instance = InstanceFromEntity[entity];
        var trigger = TriggerFromEntity[entity]; // TODO interfaces
        
        var actionParam1 = trigger.ActionParam1;
        var actionParam2 = trigger.ActionParam2;
        var actionParam3 = trigger.ActionParam3;
        var actionParam4 = trigger.ActionParam4;

        if (trigger.ActionType == ActionType.Propagate) {
          timedMulti(actionParam1, batchIndex);
          timedMulti(actionParam2, batchIndex);
          timedMulti(actionParam3, batchIndex);
          timedMulti(actionParam4, batchIndex);
        } else if (trigger.ActionType == ActionType.Lighting) {
          // Debug.Log($"Light e:{entity.Index} o:{trigger.Link.ObjectIndex} ap3:{trigger.ActionParam3}");

          if ((actionParam3 & 0x10000) == 0x10000 || (actionParam3 & 0x20000) == 0x20000)
            changeLighting(ref instance, ref trigger, batchIndex, false, actionParam1, actionParam2, actionParam3 & 0xFFFF, actionParam4);
          if ((actionParam3 & 0x10000) != 0x10000)
            changeLighting(ref instance, ref trigger, batchIndex, true, actionParam1, actionParam2, actionParam3 & 0xFFFF, actionParam4);

          if ((actionParam2 & 0xFFFF) != 0) {
            var steps = (actionParam2 >> 16) & 0xFFF;
            trigger.ActionParam2 &= 0xF000FFFF; // clear step count
            
            if (steps < NUM_LIGHT_STEPS) {
              trigger.ActionParam2 |= ++steps << 16;

              var gameTicks = TimeUtils.SecondsToFastTicks(TimeData.ElapsedTime);
              var timeStamp = (ushort)(TimeUtils.FastTicksToTimestamp((uint)(gameTicks + (TimeUtils.CIT_CYCLE >> 4))) + 1);

              var scheduleEvent = new ScheduleEvent {
                Timestamp = timeStamp,
                Type = EventType.Trap
              };
              *((TrapScheduleEvent*)scheduleEvent.Data) = new TrapScheduleEvent {
                TargetObjectIndex = 0,
                SourceObjectIndex = (short)trigger.Link.ObjectIndex
              };

              // Debug.Log($"Schedule Light steps:{steps} t:{trigger.ActionParam1 & 0xFFF} s:{trigger.Link.ObjectIndex}");

              var eventEntity = CommandBuffer.CreateEntity(batchIndex, triggerEventArchetype);
              CommandBuffer.SetComponent(batchIndex, eventEntity, scheduleEvent);
            }
          }
        } else if (trigger.ActionType == ActionType.Scheduler) {
          if (actionParam3 >= 0xFFFF
              || (actionParam3 > 0x1000 && Player.GetQuestBit((int)(actionParam3 & 0xFFF)))
              || (actionParam3 < 0x1000 && actionParam3 > 0)
          ) {
            if (actionParam3 < 0x1000 && actionParam3 > 0)
              --trigger.ActionParam3;

            var gameTicks = TimeUtils.SecondsToFastTicks(TimeData.ElapsedTime);

            var timeUnits = questDataParse((ushort)actionParam2);
            var timeStamp = (ushort)(TimeUtils.FastTicksToTimestamp((uint)(gameTicks + (TimeUtils.CIT_CYCLE * timeUnits) / TRAP_TIME_UNIT)) + 1);

            var randomTime = questDataParse((ushort)actionParam4);
            if (randomTime != 0) {
              var random = Randoms[threadIndex];
              timeStamp += (ushort)random.NextUInt((uint)randomTime);
              Randoms[threadIndex] = random;
            }

            var scheduleEvent = new ScheduleEvent {
              Timestamp = timeStamp,
              Type = EventType.Trap
            };
            *((TrapScheduleEvent*)scheduleEvent.Data) = new TrapScheduleEvent {
              TargetObjectIndex = questDataParse((ushort)actionParam1),
              SourceObjectIndex = (short)trigger.Link.ObjectIndex
            };

            Debug.Log($"Schedule t:{trigger.ActionParam1 & 0xFFF} s:{trigger.Link.ObjectIndex} ts:{timeStamp}");

            var eventEntity = CommandBuffer.CreateEntity(batchIndex, triggerEventArchetype);
            CommandBuffer.SetComponent(batchIndex, eventEntity, scheduleEvent);
          } else {
            Debug.LogWarning($"Scheduling failed e:{entity.Index} o:{trigger.Link.ObjectIndex} p3:{trigger.ActionParam3}");
          }
        } else {
          Debug.LogWarning($"Not supported e:{entity.Index} o:{trigger.Link.ObjectIndex} at:{trigger.ActionType}");
        }

        if (trigger.DestroyCount > 0) {
          if (--trigger.DestroyCount == 0)
            CommandBuffer.DestroyEntity(batchIndex, entity);
        }

        TriggerFromEntity[entity] = trigger;
      }

      private unsafe void timedMulti(uint param, int batchIndex) {
        uint timeUnits = param >> 16; // 0.1 seconds per unit
        if (timeUnits != 0) {
          var gameTicks = TimeUtils.SecondsToFastTicks(TimeData.ElapsedTime);

          var scheduleEvent = new ScheduleEvent {
            Timestamp = (ushort)(TimeUtils.FastTicksToTimestamp((uint)(gameTicks + (TimeUtils.CIT_CYCLE * timeUnits) / 10)) + 1),
            Type = EventType.Trap
          };
          *((TrapScheduleEvent*)scheduleEvent.Data) = new TrapScheduleEvent {
            TargetObjectIndex = questDataParse((ushort)(param & 0xFFFF)),
            SourceObjectIndex = -1
          };

          // Debug.Log($"Schedule t:{param & 0xFFF}");

          var entity = CommandBuffer.CreateEntity(batchIndex, triggerEventArchetype);
          CommandBuffer.SetComponent(batchIndex, entity, scheduleEvent);
        } else {
          multi(questDataParse((ushort)(param & 0xFFFF)));
        }
      }

      private void multi(short objectIndex) {
        if (objectIndex == 0) return;

        var entity = ObjectInstancesBlobAsset.Value[objectIndex];
        if (TriggerFromEntity.HasComponent(entity)) {
          Activate(entity, objectIndex, out bool message);
        } else {
          // TODO
          // if door then unlock it
          // object use
        }
      }

      private unsafe short questDataParse(ushort qdata) {
        short contents = (short)(qdata & 0xFFF);
        if ((qdata & 0x1000) == 0x1000) {
          if (contents >= Shodan.FIRST_SHODAN_QUEST_VAR && (contents <= Shodan.FIRST_SHODAN_QUEST_VAR + Shodan.NUM_SHODAN_LEVELS)) {
            if (Player.GetQuestVar(Hacker.MISSION_DIFF_QUEST_VAR) <= 1)
              return 0;

            var shodanSecurityLevel = (short)(Player.GetQuestVar(contents) * 255 / Player.initialShodanSecurityLevels[contents - Shodan.FIRST_SHODAN_QUEST_VAR]);
            return shodanSecurityLevel > 255 ? (short)255 : shodanSecurityLevel;
          } else {
            return Player.GetQuestVar(contents);
          }
        } else if ((qdata & 0x2000) == 0x2000) {
          return (short)(Player.GetQuestBit(contents) ? 1 : 0);
        } else {
          return contents;
        }
      }
    }
  }

  public struct TriggerActivateTag : IComponentData { }
}
