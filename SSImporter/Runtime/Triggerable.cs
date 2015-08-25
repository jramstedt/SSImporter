using UnityEngine;

using System.Collections;
using System.Collections.Generic;

using SystemShock.Object;
using SystemShock.Resource;
using System;

namespace SystemShock {
    public abstract class Triggerable: MonoBehaviour {
        public abstract void Trigger();

        protected static IEnumerator WaitAndTrigger(Triggerable target, ushort delay) {
            if(delay > 0)
                yield return new WaitForSeconds(delay / 10f);

            target.Trigger();
        }
    }

    public abstract class Triggerable<ActionDataType> : Triggerable {
        protected IActionProvider ActionProvider;
        protected ActionDataType ActionData;

        protected virtual void Awake() {
            ActionProvider = GetComponent<IActionProvider>();
            if (ActionProvider != null)
                ActionData = ActionProvider.ActionData.Read<ActionDataType>();
        }

        public bool CanActivate {
            get { return ActionProvider.CanActivate; }
        }
    }

    public interface IActionProvider {
        bool CanActivate { get; }
        byte[] ActionData { get; }
    }
}