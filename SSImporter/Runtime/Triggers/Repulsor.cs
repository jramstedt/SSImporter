using UnityEngine;

using System;
using System.Runtime.InteropServices;

using SystemShock.Resource;

namespace SystemShock.Triggers {
    [ExecuteInEditMode]
    [RequireComponent(typeof(BoxCollider))]
    public class Repulsor : Null {
        [ReadOnly]
        public RepulsorData Data;

        protected override void Awake() {
            base.Awake();

            BoxCollider collider = GetComponent<BoxCollider>();
            collider.isTrigger = true;

            IActionProvider actionDataProvider = GetComponent<IActionProvider>();
            if (actionDataProvider != null)
                Data = actionDataProvider.ActionData.Read<RepulsorData>();

            Vector3 colliderSize = collider.size;
            colliderSize.y = (Data.EndHeight - Data.StartHeight) / 65536f;
            collider.size = colliderSize;

            Vector3 colliderCenter = collider.center;
            colliderCenter.y = colliderSize.y / 2f + Data.StartHeight / 65536f - transform.position.y;
            collider.center = colliderCenter;
        }

        private void OnTriggerStay(Collider other) {
            if (other.attachedRigidbody)
                other.attachedRigidbody.AddForce((Data.ForceDirection == RepulsorData.Direction.Up ? -Physics.gravity : Physics.gravity) * 1.2f, ForceMode.Acceleration);
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct RepulsorData {
            public enum Direction : uint {
                Up,
                Down
            }

            public uint Unknown;
            public uint StartHeight;
            public uint EndHeight;
            public Direction ForceDirection;
        }
    }
}