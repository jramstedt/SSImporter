﻿using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class Spawn : Triggerable<ObjectInstance.Trigger.Spawn> {
        protected override void Awake() {
            base.Awake();
        }

        protected override bool DoTrigger() {
            SystemShockObject FirstCorner = ObjectFactory.Get(ActionData.Corner1ObjectId);
            SystemShockObject SecondCorner = ObjectFactory.Get(ActionData.Corner2ObjectId);

            if (FirstCorner == null || SecondCorner == null)
                return false;

            Bounds bounds = new Bounds(FirstCorner.transform.position, Vector3.zero);
            bounds.Encapsulate(SecondCorner.transform.position);

            uint combinedType = ActionData.CombinedType;
            uint Class = (combinedType >> 16) & 0xFF;
            uint Subclass = (combinedType >> 8) & 0xFF;
            uint Type = combinedType & 0xFF;

            ObjectData objectData = ObjectPropertyLibrary.GetLibrary().GetResource(combinedType);
            BaseProperties baseProperties = objectData.Base;

            LevelInfo levelInfo = ObjectFactory.LevelInfo;

            for (int i = 0; i < ActionData.Amount; ++i) {
                ObjectInstance objectInstance = new ObjectInstance();
                objectInstance.InUse = 1;
                objectInstance.Class = (ObjectClass)Class;
                objectInstance.SubClass = (byte)Subclass;
                objectInstance.Type = (byte)Type;
                objectInstance.State = (byte)ActionData.State;

                LevelInfo.Tile Tile;
                float X = 0f, Y = 0f, Z = 0f;
                do {
                    X = Random.Range(bounds.min.x, bounds.max.x);
                    Z = Random.Range(bounds.min.z, bounds.max.z);

                    Tile = levelInfo.Tiles[(int)X, (int)Z];
                    if (Tile == null)
                        continue;

                    Y = Tile.Floor * levelInfo.MapScale;// Mathf.Clamp(Random.Range(bounds.min.y, bounds.max.y), Tile.Floor * levelInfo.MapScale, Tile.Ceiling * levelInfo.MapScale);
                } while (Tile == null);

                objectInstance.X = (ushort)(X * 256f);
                objectInstance.Y = (ushort)(Z * 256f);
                objectInstance.Z = (byte)(Y / levelInfo.HeightFactor);
                objectInstance.Hitpoints = baseProperties.Hitpoints;

                IClassData classData = levelInfo.ClassDataTemplates[(int)Class].Clone();
                classData.ObjectId = ObjectFactory.NextFreeId();

                ObjectFactory.Instantiate(objectInstance, classData);
            }

            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            SystemShockObject FirstCorner = ObjectFactory.Get(ActionData.Corner1ObjectId);
            SystemShockObject SecondCorner = ObjectFactory.Get(ActionData.Corner2ObjectId);

            if (FirstCorner == null || SecondCorner == null)
                return;

            Bounds bounds = new Bounds(FirstCorner.transform.position, Vector3.zero);
            bounds.Encapsulate(SecondCorner.transform.position);

            Color color = Color.red;
            color.a = 0.1f;
            Gizmos.color = color;

            Gizmos.DrawCube(bounds.center, bounds.size);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}