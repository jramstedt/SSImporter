using UnityEngine;

using SystemShock.Object;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class SetVariable : Triggerable<ObjectInstance.Trigger.SetVariable> {
        private void Start() {
            //Debug.LogFormat(this, "{0} = {1}", ActionData.Variable, ActionData.Value);
        }

        public override void Trigger() {

        }
    }
}