using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.Interfaces {
    public class Button : MonoBehaviour, IActionPermission {
        private bool hasBeenActivated = false;
        private GameVariables gameVariables;

        private ObjectInstance.Interface interf;

        // Message index 255 == shodan security

        private void Awake() {
            gameVariables = GameVariables.GetController();
            interf = GetComponent<InstanceObjects.Interface>().ClassData;
        }

        public bool CanAct() {
            //if (hasBeenActivated && interf.OnceOnly == 1)
            //    return false;

            if (interf.ConditionVariable == 0)
                return true;

            bool invert = (interf.ConditionVariable & GameVariables.INVERT) == GameVariables.INVERT;
            bool shodan = (interf.ConditionVariable & GameVariables.SHODAN) == GameVariables.SHODAN;
            ushort ConditionVariable = (ushort)(interf.ConditionVariable & GameVariables.VARIABLEMASK);

            // TODO SHODAN get level security and check against it

            ushort conditionValue;
            gameVariables.TryGetValue(ConditionVariable, out conditionValue);

            bool canActivate = invert ^ (conditionValue == interf.ConditionValue);

            // TODO message player if cant activate

            return hasBeenActivated = canActivate;
        }
    }
}