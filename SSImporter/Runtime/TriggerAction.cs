using UnityEngine;
using System.Collections;
using SystemShock.Resource;
using SystemShock.Object;
using System;

namespace SystemShock {
    public abstract class TriggerAction : MonoBehaviour {
        public abstract void Act();

        protected static IEnumerator WaitAndTrigger(TriggerAction target, ushort delay) {
            if (delay > 0)
                yield return new WaitForSeconds(delay / 10f);

            target.Act();
        }
    }

    public abstract class TriggerAction<ActionDataType> : TriggerAction {
        protected IActionPermission PermissionProvider;
        public ActionDataType ActionData;
        protected ObjectFactory ObjectFactory;

        protected virtual void Awake() {
            PermissionProvider = GetComponentInParent<IActionPermission>();
            IActionProvider actionProvider = GetComponentInParent<IActionProvider>();
            ActionData = actionProvider.ActionData.Read<ActionDataType>();
            ObjectFactory = ObjectFactory.GetController();
        }

        public override void Act() {
            if (PermissionProvider.CanAct())
                DoAct();
        }

        protected abstract void DoAct();
    }

    public interface IActionProvider {
        byte[] ActionData { get; }
    }

    public interface IActionPermission {
        bool CanAct();
    }
}
