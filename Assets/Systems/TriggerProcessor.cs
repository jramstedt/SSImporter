using SS.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using EventType = SS.Resources.EventType;
using Random = Unity.Mathematics.Random;

namespace SS.System {
  [BurstCompile]
  public struct TriggerProcessor {
    private const byte NUM_LIGHT_STEPS = 8;
    private const byte MAX_LIGHT_VAL = 15;

    private const byte TRAP_TIME_UNIT = 10;

    public int unfilteredChunkIndex;

    [WriteOnly] public EntityCommandBuffer.ParallelWriter CommandBuffer;

    [ReadOnly] public EntityArchetype TriggerEventArchetype;

    [ReadOnly] public Hacker Player;
    [ReadOnly] public TimeData TimeData;
    [ReadOnly] public LevelInfo LevelInfo;

    [ReadOnly] public BlobAssetReference<BlobArray<Entity>> TileMapBlobAsset;
    [ReadOnly] public BlobAssetReference<BlobArray<Entity>> ObjectInstancesBlobAsset;

    [NativeDisableContainerSafetyRestriction] public ComponentLookup<MapElement> MapElementLookupRW;
    [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance> InstanceLookupRW;
    [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance.Trigger> TriggerLookupRW;
    [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance.Interface> InterfaceLookupRW;
    [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance.Decoration> DecorationLookupRW;
    [NativeDisableContainerSafetyRestriction] public ComponentLookup<ObjectInstance.DoorAndGrating> DoorLookupRW;
    [NativeDisableContainerSafetyRestriction] public NativeArray<Random> RandomsRW;

    public AnimateObjectSystemData.Writer animationList;

    public bool Activate(in Entity entity, out bool message) {
      message = false;

      if (TriggerLookupRW.HasComponent(entity)) {
        var instance = InstanceLookupRW[entity];
        var trigger = TriggerLookupRW[entity];

        //Debug.Log($"Activate e:{entity.Index} o:{trigger.Link.ObjectIndex}");

        uint comparator = instance.Info.Type switch {
          0 /* DEATHWATCH_TRIGGER_TYPE */       => 0,
          5 /* AREA_ENTRY_TRIGGER_TYPE */       => 0,
          6 /* AREA_CONTINUOUS_TRIGGER_TYPE */  => 0,
          _ => trigger.Comparator
        };

        if (ComparatorCheck(comparator, entity, out byte specialCode))
          ProcessTrigger(entity);

        return true;
      } else if (InterfaceLookupRW.HasComponent(entity)) {
        var instance = InstanceLookupRW[entity];
        var fixture = InterfaceLookupRW[entity];

        uint comparator = instance.SubClass switch {
          1 /* FIXTURE_SUBCLASS_RECEPTACLE */   => 0,
          4 /* FIXTURE_SUBCLASS_VENDING */      => 0,
          _ => fixture.Comparator
        };

        if (ComparatorCheck(comparator, entity, out byte specialCode))
          ProcessTrigger(entity);

        return true;
      }

      return false;
    }

    private unsafe void ProcessTrigger(in Entity entity) {
      if (!TriggerLookupRW.HasComponent(entity)) return;

      var instance = InstanceLookupRW[entity];
      var trigger = TriggerLookupRW[entity]; // TODO interfaces

      var actionParam1 = trigger.ActionParam1;
      var actionParam2 = trigger.ActionParam2;
      var actionParam3 = trigger.ActionParam3;
      var actionParam4 = trigger.ActionParam4;

      if (trigger.ActionType == ActionType.Propagate) {
        TimedMulti(actionParam1);
        TimedMulti(actionParam2);
        TimedMulti(actionParam3);
        TimedMulti(actionParam4);
      } else if (trigger.ActionType == ActionType.Lighting) {
        // Debug.Log($"Light e:{entity.Index} o:{trigger.Link.ObjectIndex} ap3:{trigger.ActionParam3}");

        if ((actionParam3 & 0x10000) == 0x10000 || (actionParam3 & 0x20000) == 0x20000)
          ChangeLighting(ref instance, ref trigger, false, actionParam1, actionParam2, actionParam3 & 0xFFFF, actionParam4);
        if ((actionParam3 & 0x10000) != 0x10000)
          ChangeLighting(ref instance, ref trigger, true, actionParam1, actionParam2, actionParam3 & 0xFFFF, actionParam4);

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

            var eventEntity = CommandBuffer.CreateEntity(unfilteredChunkIndex, TriggerEventArchetype);
            CommandBuffer.SetComponent(unfilteredChunkIndex, eventEntity, scheduleEvent);
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

          var timeUnits = QuestDataParse((ushort)actionParam2);
          var timeStamp = (ushort)(TimeUtils.FastTicksToTimestamp((uint)(gameTicks + (TimeUtils.CIT_CYCLE * timeUnits) / TRAP_TIME_UNIT)) + 1);

          var randomTime = QuestDataParse((ushort)actionParam4);
          if (randomTime != 0) {
            var threadIndex = JobsUtility.ThreadIndex;
            var random = RandomsRW[threadIndex];
            timeStamp += (ushort)random.NextUInt((uint)randomTime);
            RandomsRW[threadIndex] = random;
          }

          var scheduleEvent = new ScheduleEvent {
            Timestamp = timeStamp,
            Type = EventType.Trap
          };
          *((TrapScheduleEvent*)scheduleEvent.Data) = new TrapScheduleEvent {
            TargetObjectIndex = QuestDataParse((ushort)actionParam1),
            SourceObjectIndex = (short)trigger.Link.ObjectIndex
          };

          // Debug.Log($"Schedule t:{trigger.ActionParam1 & 0xFFF} s:{trigger.Link.ObjectIndex} ts:{timeStamp}");

          var eventEntity = CommandBuffer.CreateEntity(unfilteredChunkIndex, TriggerEventArchetype);
          CommandBuffer.SetComponent(unfilteredChunkIndex, eventEntity, scheduleEvent);
        } else {
          Debug.LogWarning($"Scheduling failed e:{entity.Index} o:{trigger.Link.ObjectIndex} p3:{trigger.ActionParam3}");
        }
      } else if (trigger.ActionType == ActionType.PropagateAlternating) {
        var triggerIndex = trigger.Link.ObjectIndex;

        var phase = actionParam4;
        var maxPhase = actionParam3 == 0 ? 1 : 2;

        if (phase == 0)
          TimedMulti((uint)QuestDataParse((ushort)actionParam1));
        else if (phase == 1)
          TimedMulti((uint)QuestDataParse((ushort)actionParam2));
        else if (phase == 2)
          TimedMulti((uint)QuestDataParse((ushort)actionParam3));

        if (++phase > maxPhase)
          phase = 0;

        SetTrapData(triggerIndex, 4, phase);
      } else if (trigger.ActionType == ActionType.ChangeClassData) {
        ChangeInstance((ushort)QuestDataParse((ushort)(actionParam1 & 0xFFFF)), actionParam2, actionParam3, actionParam4);
        ChangeInstance((ushort)QuestDataParse((ushort)(actionParam1 >> 16)), actionParam2, actionParam3, actionParam4);
      } else if (trigger.ActionType == ActionType.ChangeAnimation) {
        ChangeAnimation((ushort)QuestDataParse((ushort)(actionParam1 & 0xFFFF)), actionParam2, actionParam3, actionParam4 != 0);
        ChangeAnimation((ushort)QuestDataParse((ushort)(actionParam1 >> 16)), actionParam2, actionParam3, actionParam4 != 0);
      } else {
        Debug.LogWarning($"Not supported e:{entity.Index} o:{trigger.Link.ObjectIndex} at:{trigger.ActionType}");
      }

      if (trigger.DestroyCount > 0) {
        if (--trigger.DestroyCount == 0)
          CommandBuffer.DestroyEntity(unfilteredChunkIndex, entity);
      }

      TriggerLookupRW[entity] = trigger;
    }

    // TODO interfaces
    private unsafe void ChangeLighting(ref ObjectInstance instance, ref ObjectInstance.Trigger trigger, bool floor, uint actionParam1, uint actionParam2, uint actionParam3, uint actionParam4) {
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
        short radius = QuestDataParse((ushort)(actionParam1 & 0xFFFF));
        rectMin = int2(instance.Location.TileX - radius, instance.Location.TileY - radius);
        rectMax = int2(instance.Location.TileX + radius, instance.Location.TileY + radius);
      } else {
        if (values[0] > 0x0F || values[1] > 0x0F || values[2] > 0x0F || values[3] > 0x0F) return;

        var firstObjID = QuestDataParse((ushort)(actionParam1 & 0xFFFF));
        var secondObjID = QuestDataParse((ushort)(actionParam1 >> 16));
        if (firstObjID == 0 || secondObjID == 0) return;

        var firstEntity = ObjectInstancesBlobAsset.Value[firstObjID];
        var secondEntity = ObjectInstancesBlobAsset.Value[secondObjID];
        //if (!InstanceFromEntity.HasComponent(ulEntity) || !InstanceFromEntity.HasComponent(lrEntity)) return;

        var firstObj = InstanceLookupRW[firstEntity];
        var secondObj = InstanceLookupRW[secondEntity];

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
        startShade = lightState != 0 ? values[0] : values[2];
        var startShadeEnd = lightState != 0 ? values[2] : values[0];

        endShade = lightState != 0 ? values[1] : values[3];
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
          var mapElement = MapElementLookupRW[mapEntity];

          if (actionParam3 == 3) { // Radial
            var delta = length(float2(
              (x << 8 - instance.Location.X) / 255f,
              (y << 8 - instance.Location.Y) / 255f
            ));
            var radius = (float)(actionParam1 & 0xFFFF);
            if (delta <= radius)
              tempLight = (byte)(startShade + (delta / radius) * deltaShade);
            else
              tempLight = floor ? mapElement.ShadeFloorModifier : mapElement.ShadeCeilingModifier;

            if (tempLight > MAX_LIGHT_VAL)
              tempLight = MAX_LIGHT_VAL;
          } else if (actionParam3 == 1) { // EW Smooth
            if (x == rectMax.x - 1) // end
              tempLight = endShade;
            else
              tempLight += deltaShade;
          }

          // Debug.Log($"Setting x:{x} y:{y} f:{floor} l:{tempLight}");

          if (floor)
            mapElement.ShadeFloorModifier = tempLight;
          else
            mapElement.ShadeCeilingModifier = tempLight;

          MapElementLookupRW[mapEntity] = mapElement;

          CommandBuffer.AddComponent<LightmapRebuildTag>(unfilteredChunkIndex, mapEntity);
        }

        if (actionParam3 == 1) { // EW Smooth
          tempLight = startShade;
        } else if (actionParam3 == 2) { // NS Smooth
          if (y == rectMax.y - 1)
            tempLight = endShade;
          else
            tempLight += deltaShade;
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectIndex"></param>
    /// <param name="parameterNumber">From 1 to 4</param>
    /// <param name="value"></param>
    private void SetTrapData(ushort objectIndex, byte parameterNumber, uint value) {
      if (parameterNumber < 1 || parameterNumber > 4) return;
      if (objectIndex == 0) return;

      var entity = ObjectInstancesBlobAsset.Value[objectIndex];
      if (TriggerLookupRW.HasComponent(entity)) {
        var trigger = TriggerLookupRW[entity];

        if (parameterNumber == 1) trigger.ActionParam1 = value;
        if (parameterNumber == 2) trigger.ActionParam2 = value;
        if (parameterNumber == 3) trigger.ActionParam3 = value;
        if (parameterNumber == 4) trigger.ActionParam4 = value;

        TriggerLookupRW[entity] = trigger;
      } else if (InterfaceLookupRW.HasComponent(entity)) {
        var fixture = InterfaceLookupRW[entity];

        if (parameterNumber == 1) fixture.ActionParam1 = value;
        if (parameterNumber == 2) fixture.ActionParam2 = value;
        if (parameterNumber == 3) fixture.ActionParam3 = value;
        if (parameterNumber == 4) fixture.ActionParam4 = value;

        InterfaceLookupRW[entity] = fixture;
      }
    }

    private void ChangeInstance(ushort objectIndex, uint actionParam2, uint actionParam3, uint actionParam4) {
      if (objectIndex == 0) return;

      var entity = ObjectInstancesBlobAsset.Value[objectIndex];
      if (InstanceLookupRW.HasComponent(entity)) {
        var instance = InstanceLookupRW[entity];

        if (!instance.Active) return;

        if (instance.Class == ObjectClass.Decoration) {
          var decoration = DecorationLookupRW[entity];
          if (actionParam2 != uint.MaxValue) decoration.Cosmetic = (ushort)actionParam2;
          if (actionParam3 != uint.MaxValue) decoration.Data1 = actionParam3;
          if (actionParam4 != uint.MaxValue) decoration.Data2 = actionParam4;
          DecorationLookupRW[entity] = decoration;
        } else if (instance.Class == ObjectClass.DoorAndGrating) {
          var door = DoorLookupRW[entity];
          if (actionParam2 != uint.MaxValue) door.Lock = (ushort)actionParam2;
          if (actionParam3 != uint.MaxValue) {
            door.LockMessage = (byte)(actionParam3 >> 8);
            door.Color = (byte)(actionParam3 & 0xFF);
          }
          if (actionParam4 != uint.MaxValue) {
            door.AccessLevel = (byte)(actionParam4 >> 8);
            door.AutocloseTime = (byte)(actionParam4 & 0xFF);
          }
          DoorLookupRW[entity] = door;
        } else if (instance.Class == ObjectClass.Interface || instance.Class == ObjectClass.Trigger) {
          SetTrapData(objectIndex, (byte)actionParam2, actionParam3);
        }
      }
    }

    public void ChangeAnimation(ushort objectIndex, uint actionParam2, uint actionParam3, bool removeAnimation) {
      if (objectIndex == 0) return;

      byte frames = 0;

      var entity = ObjectInstancesBlobAsset.Value[objectIndex];
      if (InstanceLookupRW.HasComponent(entity) && DecorationLookupRW.HasComponent(entity)) {
        var instance = InstanceLookupRW[entity];

        if (!instance.Active) return;
        if (instance.Info.Hitpoints == 0) return;

        frames = (byte)DecorationLookupRW[entity].Cosmetic;

        if (frames == 0) frames = 4;

        if (removeAnimation) animationList.RemoveAnimation(objectIndex);

        if (actionParam2 == 0) {
          var reverse = (actionParam3 & 0x8000) == 0x8000; // 1 << 15
          var cycle = (actionParam3 & 0x10000) == 0x10000; // 1 << 16

          if ((actionParam3 & 0xF0000000) > 0) frames = (byte)(actionParam3 >> 28);
          ChangeInstance(objectIndex, frames, uint.MaxValue, actionParam3 & 0x7FFF);
          instance.Info.CurrentFrame = (sbyte)(reverse ? frames - 1 : 0);
          instance.Info.TimeRemaining = 0;

          animationList.AddAnimation(objectIndex, true, reverse, cycle, 0, AnimationData.Callback.Null, 0, AnimationData.AnimationCallbackType.Null);
        } else {
          var reverse = (actionParam2 & 0x8000) == 0x8000; // 1 << 15
          var cycle = (actionParam2 & 0x10000) == 0x10000; // 1 << 16

          if ((actionParam2 & 0xF0000000) > 0) frames = (byte)(actionParam2 >> 28);
          ChangeInstance(objectIndex, frames, uint.MaxValue, actionParam2 & 0x7FFF);
          instance.Info.CurrentFrame = (sbyte)(reverse ? frames - 1 : 0);
          instance.Info.TimeRemaining = 0;

          if (actionParam3 != 0)
            animationList.AddAnimation(objectIndex, false, reverse, cycle, 0, AnimationData.Callback.Animate, actionParam3, AnimationData.AnimationCallbackType.Remove);
          else
            animationList.AddAnimation(objectIndex, false, reverse, cycle, 0, AnimationData.Callback.Null, 0, AnimationData.AnimationCallbackType.Null);
        }

        InstanceLookupRW[entity] = instance;
      }
    }

    public void Multi(short objectIndex) {
      if (objectIndex == 0) return;

      var entity = ObjectInstancesBlobAsset.Value[objectIndex];
      if (TriggerLookupRW.HasComponent(entity)) {
        Activate(entity, out bool message);
      } else { // TODO Interfaces?
        // TODO
        // if door then unlock it

        // object use !!!
      }
    }

    public unsafe void TimedMulti(uint param) {
      uint timeUnits = param >> 16; // 0.1 seconds per unit
      if (timeUnits != 0) {
        var gameTicks = TimeUtils.SecondsToFastTicks(TimeData.ElapsedTime);

        var scheduleEvent = new ScheduleEvent {
          Timestamp = (ushort)(TimeUtils.FastTicksToTimestamp((uint)(gameTicks + (TimeUtils.CIT_CYCLE * timeUnits) / 10)) + 1),
          Type = EventType.Trap
        };
        *((TrapScheduleEvent*)scheduleEvent.Data) = new TrapScheduleEvent {
          TargetObjectIndex = QuestDataParse((ushort)(param & 0xFFFF)),
          SourceObjectIndex = -1
        };

        // Debug.Log($"Schedule t:{param & 0xFFF}");

        var entity = CommandBuffer.CreateEntity(unfilteredChunkIndex, TriggerEventArchetype);
        CommandBuffer.SetComponent(unfilteredChunkIndex, entity, scheduleEvent);
      } else {
        Multi(QuestDataParse((ushort)(param & 0xFFFF)));
      }
    }

    private readonly bool ComparatorCheck(uint comparator, in Entity entity, out byte specialCode) { // TODO FIXME
      specialCode = 0;
      return true;
    }

    private unsafe short QuestDataParse(ushort qdata) {
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
