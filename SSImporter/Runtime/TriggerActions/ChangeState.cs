﻿using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class ChangeState : Triggerable<ObjectInstance.Trigger.ChangeState> {
        public SystemShockObject Target;

        private LevelInfo levelInfo;

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
        }

        private void Start() {
            if (ActionData.ObjectId != 0 && !levelInfo.Objects.TryGetValue((ushort)ActionData.ObjectId, out Target))
                Debug.Log("Tried to find object! " + ActionData.ObjectId, this);
        }

        public override void Trigger() {
            if (!CanActivate)
                return;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (Target != null)
                Gizmos.DrawLine(transform.position, Target.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}