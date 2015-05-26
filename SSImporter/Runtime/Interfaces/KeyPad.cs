using UnityEngine;
using System.Collections;
using SystemShock.Object;

namespace SystemShock.Interfaces {
    [ExecuteInEditMode]
    public class KeyPad : Triggerable<ObjectInstance.Interface.KeyPad> {
        public ITriggerable Target1;
        public ITriggerable Target2;

        private LevelInfo levelInfo;

        private void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();
        }

        private void Start() {
            SystemShockObject ssObject;
            if (ActionData.ObjectToTrigger1 != 0 && levelInfo.Objects.TryGetValue((uint)ActionData.ObjectToTrigger1, out ssObject)) {
                Target1 = ssObject.GetComponent<ITriggerable>();

                if (Target1 == null)
                    Debug.Log("Tried to link trigger! " + ssObject, ssObject);
            } else if (ActionData.ObjectToTrigger1 != 0) {
                Debug.Log("Tried to find object! " + ActionData.ObjectToTrigger1, this);
            }

            if (ActionData.ObjectToTrigger2 != 0 && levelInfo.Objects.TryGetValue((uint)ActionData.ObjectToTrigger2, out ssObject)) {
                Target2 = ssObject.GetComponent<ITriggerable>();

                if (Target2 == null)
                    Debug.Log("Tried to link trigger! " + ssObject, ssObject);
            } else if (ActionData.ObjectToTrigger2 != 0) {
                Debug.Log("Tried to find object! " + ActionData.ObjectToTrigger2, this);
            }
        }

        public override void Trigger() {
            if (Target1 != null)
                Target1.Trigger();

            if (Target2 != null)
                Target2.Trigger();
        }

        private void OnMouseDown() {
            Trigger();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (Target1 != null)
                Gizmos.DrawLine(transform.position, Target1.transform.position);

            if (Target2 != null)
                Gizmos.DrawLine(transform.position, Target2.transform.position);
        }

        private void OnDrawGizmosSelected() {

        }
#endif
    }
}