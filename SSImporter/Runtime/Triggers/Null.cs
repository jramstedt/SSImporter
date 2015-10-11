using UnityEngine;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.Triggers {
    public class Null : MonoBehaviour, IActionPermission {
        private bool hasBeenActivated = false;
        protected GameVariables gameVariables { get; private set; }
        protected ObjectInstance.Trigger trigger { get; private set; }

        private const ushort SCOPEMASK = 0xE000;
        private const ushort VARIABLEMASK = 0x1FFF;

        private const ushort INVERT = 0x8000;

        protected virtual void Awake() {
            gameVariables = GameVariables.GetController();
            trigger = GetComponent<InstanceObjects.Trigger>().ClassData;
        }

        public bool CanAct() {
            if (hasBeenActivated && trigger.OnceOnly == 1)
                return false;

            if (trigger.ConditionVariable == 0) {
                hasBeenActivated = true;
                return true;
            }

            bool invert = (trigger.ConditionVariable & INVERT) == INVERT;
            ushort ConditionVariable = (ushort)(trigger.ConditionVariable & VARIABLEMASK);

            ushort conditionValue;
            gameVariables.TryGetValue(ConditionVariable, out conditionValue);

            bool canActivate = invert ^ (conditionValue == trigger.ConditionValue);

            return hasBeenActivated = canActivate;
        }
    }
}