using UnityEngine;

using SystemShock.Object;
using SystemShock.Gameplay;
using SystemShock.UserInterface;

namespace SystemShock.TriggerActions {
    [ExecuteInEditMode]
    public class SetVariable : Triggerable<ObjectInstance.Trigger.SetVariable> {
        private GameVariables gameVariables;

        private const ushort NUMBER_FLAG = 0x1000;
        //private const ushort NUMBER_KEY_MASK = 0x003F;
        //private const ushort NUMBER_MASK = 0xFFF;

        //private const ushort BOOLEAN_KEY_MASK = 0x01FF;

        private void Start() {
            gameVariables = GameVariables.GetController();

            //Debug.LogFormat(this, "{0} = {1}", ActionData.Variable, ActionData.Value);
        }

        protected override bool DoTrigger() {
            for (int i = 0; i < ActionData.Variable.Length; ++i) {
                if((ActionData.Variable[i] & NUMBER_FLAG) == NUMBER_FLAG) {
                    //ushort variableKey = (ushort)(ActionData.Variable[i] & SetVariable.NUMBER_KEY_MASK);
                    ushort variableKey = ActionData.Variable[i];

                    ushort currentValue;
                    gameVariables.TryGetValue(variableKey, out currentValue);

                    if (ActionData.Operation == ObjectInstance.Trigger.SetVariable.VariableOperation.Set)
                        currentValue = ActionData.Value;
                    else if (ActionData.Operation == ObjectInstance.Trigger.SetVariable.VariableOperation.Add)
                        currentValue += ActionData.Value;
                    else if (ActionData.Operation == ObjectInstance.Trigger.SetVariable.VariableOperation.Add)
                        currentValue -= ActionData.Value;
                    else if (ActionData.Operation == ObjectInstance.Trigger.SetVariable.VariableOperation.Add)
                        currentValue *= ActionData.Value;
                    else if (ActionData.Operation == ObjectInstance.Trigger.SetVariable.VariableOperation.Add && ActionData.Value != 0)
                        currentValue /= ActionData.Value;

                    gameVariables[variableKey] = currentValue;

                    if (currentValue > 0)
                        MessageBus.Send(new TrapMessage((byte)ActionData.MessageOn));
                    else
                        MessageBus.Send(new TrapMessage((byte)ActionData.MessageOff));
                } else {
                    //ushort variableKey = (ushort)(ActionData.Variable[i] & SetVariable.BOOLEAN_KEY_MASK);
                    ushort variableKey = ActionData.Variable[i];

                    ushort currentValue;
                    gameVariables.TryGetValue(variableKey, out currentValue);

                    if (ActionData.Value == (ushort)ObjectInstance.Trigger.SetVariable.BooleanAction.Toggle)
                        currentValue = (ushort)~currentValue;
                    else
                        currentValue = ActionData.Value;

                    gameVariables[variableKey] = currentValue;

                    if (currentValue > 0)
                        if(ActionData.MessageOn != 0) MessageBus.Send(new TrapMessage((byte)ActionData.MessageOn));
                    else
                        if(ActionData.MessageOff != 0) MessageBus.Send(new TrapMessage((byte)ActionData.MessageOff));
                }
            }

            return true;
        }
    }
}