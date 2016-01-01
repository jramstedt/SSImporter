using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;
using System;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class SetVariable : TriggerAction<ObjectInstance.Trigger.SetVariable> {
        private GameVariables gameVariables;

        private void Start() {
            gameVariables = GameVariables.GetController();

            //Debug.LogFormat(this, "{0} = {1}", ActionData.Variable, ActionData.Value);
        }

        protected override void DoAct() {
            for (int i = 0; i < ActionData.Variable.Length; ++i) {
                ObjectInstance.Trigger.SetVariable.VariableAction action = ActionData.Action;
                ushort variable = (ushort)(ActionData.Variable[i] & GameVariables.VARIABLEMASK);

                if ((action & ObjectInstance.Trigger.SetVariable.VariableAction.Set) == ObjectInstance.Trigger.SetVariable.VariableAction.Set) {
                    ushort currentValue;
                    gameVariables.TryGetValue(variable, out currentValue);

                    if (ActionData.Operation == ObjectInstance.Trigger.SetVariable.VariableOperation.Increment)
                        currentValue += 1;
                    else
                        currentValue = 1;

                    gameVariables[variable] = currentValue;
                }
                
                if ((action & ObjectInstance.Trigger.SetVariable.VariableAction.Toggle) == ObjectInstance.Trigger.SetVariable.VariableAction.Toggle)
                    gameVariables[variable] = (ushort)~gameVariables[variable];
                
                
                if(action == 0)
                    gameVariables[variable] = 0;
            }
        }
    }
}