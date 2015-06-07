using UnityEngine;

using System.Collections;
using System.Collections.Generic;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock {
    public abstract class Triggerable: MonoBehaviour {
        public abstract void Trigger();

        protected static IEnumerator WaitAndTrigger(Triggerable target, ushort delay) {
            yield return new WaitForSeconds(delay / 65536f);
            target.Trigger();
        }
    }

    public abstract class Triggerable<ActionDataType> : Triggerable {
        protected ActionDataType ActionData;

        protected virtual void Awake() {
            ITriggerActionProvider trigger = GetComponent<ITriggerActionProvider>();
            if (trigger != null)
                ActionData = trigger.TriggerData.Read<ActionDataType>();
        }
    }

    public interface ITriggerActionProvider {
        byte[] TriggerData { get; }
    }
}