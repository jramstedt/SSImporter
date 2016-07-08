using UnityEngine;
using System.Collections;
using SystemShock.Resource;
using SystemShock.Object;
using System;

namespace SystemShock {
    public abstract class TriggerAction : MonoBehaviour {
        public abstract bool Act();

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
        protected MessageBus MessageBus;

        protected virtual void Awake() {
            PermissionProvider = GetComponentInParent<IActionPermission>();
            IActionProvider actionProvider = GetComponentInParent<IActionProvider>();
            ActionData = actionProvider.ActionData.Read<ActionDataType>();
            ObjectFactory = ObjectFactory.GetController();
            MessageBus = MessageBus.GetController();
        }

        public override bool Act() {
            if (PermissionProvider.CanAct()) {
                DoAct();
                return true;
            }

            return false;
        }

        protected abstract void DoAct();

        protected void WaitAndTrigger(ushort objectId, ushort delay) {
            TriggerAction Target = ObjectFactory.Get<TriggerAction>(objectId);
            if (Target != null)
                StartCoroutine(WaitAndTrigger(Target, delay));
        }
    }

    public interface IActionProvider {
        byte[] ActionData { get; }
    }

    public interface IActionPermission {
        bool CanAct();
    }
}
