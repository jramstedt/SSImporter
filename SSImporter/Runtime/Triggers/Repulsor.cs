using UnityEngine;

using System;
using System.Runtime.InteropServices;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    [ExecuteInEditMode]
    [RequireComponent(typeof(BoxCollider))]
    public class Repulsor : MonoBehaviour {
        [SerializeField, ReadOnly]
        private RepulsorData Data;

        private void Awake() {
            BoxCollider collider = GetComponent<BoxCollider>();
            collider.isTrigger = true;

            ITriggerActionProvider actionDataProvider = GetComponent<ITriggerActionProvider>();
            if (actionDataProvider != null)
                Data = actionDataProvider.TriggerData.Read<RepulsorData>();

            LevelInfo levelInfo = GameObject.FindObjectOfType<LevelInfo>();

            InstanceObjects.Trigger trigger = GetComponent<InstanceObjects.Trigger>();

            Vector3 colliderSize = collider.size;
            colliderSize.y = (Data.EndHeight - Data.StartHeight) / 256f;
            collider.size = colliderSize;

            Vector3 colliderCenter = collider.center;
            colliderCenter.y = colliderSize.y / 2f + Data.StartHeight / 256f - transform.position.y;
            collider.center = colliderCenter;
        }

        private void OnTriggerStay(Collider other) {
            if (other.attachedRigidbody)
                other.attachedRigidbody.AddForce((Data.ForceDirection == RepulsorData.Direction.Up ? -Physics.gravity : Physics.gravity) * 1.2f, ForceMode.Acceleration);
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct RepulsorData {
            public enum Direction : byte {
                Down,
                Up
            }

            public uint Unknown;
            public Direction ForceDirection;
            public ushort StartHeight;
            public ushort Unknown1;
            public ushort EndHeight;
            public byte Unknown2;
            public uint Reversible;
        }
    }
}