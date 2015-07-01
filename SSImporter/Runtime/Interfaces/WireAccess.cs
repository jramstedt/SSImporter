﻿using UnityEngine;
using System.Collections;
using SystemShock.Object;

namespace SystemShock.Interfaces {
    [ExecuteInEditMode]
    public class WireAccess : Triggerable<ObjectInstance.Interface.WireAccess> {
        public Triggerable Target;

        private LevelInfo levelInfo;

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
        }

        private void Start() {
            SystemShockObject ssObject;
            if (ActionData.ObjectToTrigger != 0 && levelInfo.Objects.TryGetValue(ActionData.ObjectToTrigger, out ssObject)) {
                Target = ssObject.GetComponent<Triggerable>();

                if (Target == null)
                    Debug.Log("Tried to link trigger! " + ssObject, ssObject);
            } else if (ActionData.ObjectToTrigger != 0) {
                Debug.Log("Tried to find object! " + ActionData.ObjectToTrigger, this);
            }
        }

        public override void Trigger() {
            if (Target != null)
                Target.Trigger();
        }

        private void OnMouseDown() {
            Trigger();
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