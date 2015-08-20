using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class SetVariable : Triggerable<ObjectInstance.Trigger.SetVariable> {
        private GameVariables gameVariables;

        private void Start() {
            gameVariables = GameVariables.GetController();

            //Debug.LogFormat(this, "{0} = {1}", ActionData.Variable, ActionData.Value);
        }

        public override void Trigger() {
            if (!CanActivate)
                return;

            if (ActionData.Action == ObjectInstance.Trigger.SetVariable.VariableAction.Set) {
                gameVariables[(ushort)ActionData.Variable] = ActionData.Value;
            } else if (ActionData.Action == ObjectInstance.Trigger.SetVariable.VariableAction.Add) {
                ushort currentValue;

                if (gameVariables.TryGetValue((ushort)ActionData.Variable, out currentValue))
                    currentValue += ActionData.Value;
                else
                    currentValue = ActionData.Value;

                gameVariables[(ushort)ActionData.Variable] = currentValue;
            }
        }
    }
}