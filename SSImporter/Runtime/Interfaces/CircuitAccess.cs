using UnityEngine;
using System.Collections;
using SystemShock.Object;

namespace SystemShock.Interfaces {
    [ExecuteInEditMode]
    public class CircuitAccess : Triggerable<ObjectInstance.Interface.CircuitAccess> {
        public ITriggerable Target;

        private LevelInfo levelInfo;

        private void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
        }

        private void Start() {
            SystemShockObject ssObject;
            if (ActionData.ObjectToTrigger != 0 && levelInfo.Objects.TryGetValue((uint)ActionData.ObjectToTrigger, out ssObject)) {
                Target = ssObject.GetComponent<ITriggerable>();

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