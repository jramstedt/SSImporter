using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;
using System;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class Awaken : Triggerable<ObjectInstance.Trigger.Awaken> {
        public SystemShockObject FirstCorner;
        public SystemShockObject SecondCorner;

        private void Start() {
            FirstCorner = ObjectFactory.Get(ActionData.Corner1ObjectId) ?? GetComponent<SystemShockObject>();
            SecondCorner = ObjectFactory.Get(ActionData.Corner2ObjectId);
        }

        protected override bool DoTrigger() { return true; }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (FirstCorner == null || SecondCorner == null)
                return;

            Bounds bounds = new Bounds(FirstCorner.transform.position, Vector3.zero);
            bounds.Encapsulate(SecondCorner.transform.position);

            Color color = Color.green;
            color.a = 0.1f;
            Gizmos.color = color;

            Gizmos.DrawCube(bounds.center, bounds.size);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}