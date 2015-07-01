using UnityEngine;

using System.Collections;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class Propagate : Triggerable<ObjectInstance.Trigger.Propagate> {
        public Triggerable Target1;
        public Triggerable Target2;
        public Triggerable Target3;
        public Triggerable Target4;

        private LevelInfo levelInfo;

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();

            //Debug.LogFormat("Propagate {0} {1} {2} {3}", ActionData.ObjectToTrigger1, ActionData.ObjectToTrigger2, ActionData.ObjectToTrigger3, ActionData.ObjectToTrigger4);
        }

        private void Start() {
            SystemShockObject ssObject;
            if (ActionData.ObjectToTrigger1 != 0 && levelInfo.Objects.TryGetValue(ActionData.ObjectToTrigger1, out ssObject)) {
                Target1 = ssObject.GetComponent<Triggerable>();

                if (Target1 == null) {
                    Debug.Log("1Tried to link trigger! " + ssObject, ssObject);
                    Debug.Log("2Tried to link trigger! " + ssObject, this);
                }
            } else if (ActionData.ObjectToTrigger1 != 0) {
                Debug.Log("Tried to find object! " + ActionData.ObjectToTrigger1, this);
            }

            if (ActionData.ObjectToTrigger2 != 0 && levelInfo.Objects.TryGetValue(ActionData.ObjectToTrigger2, out ssObject)) {
                Target2 = ssObject.GetComponent<Triggerable>();

                if (Target2 == null) {
                    Debug.Log("1Tried to link trigger! " + ssObject, ssObject);
                    Debug.Log("2Tried to link trigger! " + ssObject, this);
                }
            } else if (ActionData.ObjectToTrigger2 != 0) {
                Debug.Log("Tried to find object! " + ActionData.ObjectToTrigger2, this);
            }

            if (ActionData.ObjectToTrigger3 != 0 && levelInfo.Objects.TryGetValue(ActionData.ObjectToTrigger3, out ssObject)) {
                Target3 = ssObject.GetComponent<Triggerable>();

                if (Target3 == null) {
                    Debug.Log("1Tried to link trigger! " + ssObject, ssObject);
                    Debug.Log("2Tried to link trigger! " + ssObject, this);
                }
            } else if (ActionData.ObjectToTrigger3 != 0) {
                Debug.Log("Tried to find object! " + ActionData.ObjectToTrigger3, this);
            }

            if (ActionData.ObjectToTrigger4 != 0 && levelInfo.Objects.TryGetValue(ActionData.ObjectToTrigger4, out ssObject)) {
                Target4 = ssObject.GetComponent<Triggerable>();

                if (Target4 == null) {
                    Debug.Log("1Tried to link trigger! " + ssObject, ssObject);
                    Debug.Log("2Tried to link trigger! " + ssObject, this);
                }
            } else if(ActionData.ObjectToTrigger4 != 0) {
                Debug.Log("Tried to find object! " + ActionData.ObjectToTrigger4, this);
            }
        }

        public override void Trigger() {
            if (Target1 != null)
                StartCoroutine(WaitAndTrigger(Target1, ActionData.Delay1));

            if (Target2 != null)
                StartCoroutine(WaitAndTrigger(Target2, ActionData.Delay2));

            if (Target3 != null)
                StartCoroutine(WaitAndTrigger(Target3, ActionData.Delay3));

            if (Target4 != null)
                StartCoroutine(WaitAndTrigger(Target4, ActionData.Delay4));
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (Target1 != null)
                Gizmos.DrawLine(transform.position, Target1.transform.position);

            if (Target2 != null)
                Gizmos.DrawLine(transform.position, Target2.transform.position);

            if (Target3 != null)
                Gizmos.DrawLine(transform.position, Target3.transform.position);

            if (Target4 != null)
                Gizmos.DrawLine(transform.position, Target4.transform.position);
        }
        
        private void OnDrawGizmosSelected() {

        }
#endif
    }
}