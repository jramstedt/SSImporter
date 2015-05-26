using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class PropagateConditional : Triggerable<ObjectInstance.Trigger.Propagate> {
        public ITriggerable Target1;
        public ITriggerable Target2;
        public ITriggerable Target3;
        public ITriggerable Target4;

        public bool condition;

        private LevelInfo levelInfo;

        protected override void Awake() {
            base.Awake();

            levelInfo = GameObject.FindObjectOfType<LevelInfo>();

            //Debug.LogFormat("Propagate {0} {1} {2} {3}", ActionData.ObjectToTrigger1, ActionData.ObjectToTrigger2, ActionData.ObjectToTrigger3, ActionData.ObjectToTrigger4);
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

            if (ActionData.ObjectToTrigger3 != 0 && levelInfo.Objects.TryGetValue((uint)ActionData.ObjectToTrigger3, out ssObject)) {
                Target3 = ssObject.GetComponent<ITriggerable>();

                if (Target3 == null)
                    Debug.Log("Tried to link trigger! " + ssObject, ssObject);
            } else if (ActionData.ObjectToTrigger3 != 0) {
                Debug.Log("Tried to find object! " + ActionData.ObjectToTrigger3, this);
            }

            if (ActionData.ObjectToTrigger4 != 0 && levelInfo.Objects.TryGetValue((uint)ActionData.ObjectToTrigger4, out ssObject)) {
                Target4 = ssObject.GetComponent<ITriggerable>();

                if (Target4 == null)
                    Debug.Log("Tried to link trigger! " + ssObject, ssObject);
            } else if (ActionData.ObjectToTrigger4 != 0) {
                Debug.Log("Tried to find object! " + ActionData.ObjectToTrigger4, this);
            }
        }

        public override void Trigger() {
            condition = !condition;

            if (!condition)
                return;

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