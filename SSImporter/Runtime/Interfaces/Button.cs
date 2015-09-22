using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.Interfaces {
    public class Button : MonoBehaviour, IActionPermission {
        private bool hasBeenActivated = false;
        private GameVariables gameVariables;

        private ObjectInstance.Interface interf;

        private const ushort SCOPEMASK = 0xE000;
        private const ushort VARIABLEMASK = 0x1FFF;

        private const ushort INVERT = 0x8000;

        // Message index 255 == shodan security

        private void Awake() {
            gameVariables = GameVariables.GetController();
            interf = GetComponent<InstanceObjects.Interface>().ClassData;
        }

        public bool CanAct() {
            //if (hasBeenActivated && interf.OnceOnly == 1)
            //    return false;

            bool invert = (interf.ConditionVariable & INVERT) == INVERT;
            ushort ConditionVariable = (ushort)(interf.ConditionVariable & VARIABLEMASK);

            ushort conditionValue;
            gameVariables.TryGetValue(ConditionVariable, out conditionValue);

            bool canActivate = invert ^ (conditionValue == interf.ConditionValue);

            // TODO message player if cant activate

            return hasBeenActivated = canActivate;
        }
    }
}