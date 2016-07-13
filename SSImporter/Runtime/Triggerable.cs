using UnityEngine;
using System.Collections;
using SystemShock.Resource;
using SystemShock.Object;
using System;

namespace SystemShock {

    public interface ITriggerable {
        bool Trigger();

#if UNITY_EDITOR
        Transform transform { get; }
#endif
    }

    public abstract class Triggerable : MonoBehaviour, ITriggerable {
        protected IActionPermission PermissionProvider;
        protected ObjectFactory ObjectFactory;
        protected MessageBus MessageBus;

        public virtual bool Trigger() {
            if (PermissionProvider.CanAct())
                return DoTrigger();

            return false;
        }

        protected abstract bool DoTrigger();

        protected virtual void Awake() {
            PermissionProvider = GetComponentInParent<IActionPermission>();
            ObjectFactory = ObjectFactory.GetController();
            MessageBus = MessageBus.GetController();
        }

        protected void WaitAndTrigger(ushort objectId, ushort delay) {
            ITriggerable Target = ObjectFactory.Get<ITriggerable>(objectId);
            if (Target != null)
                StartCoroutine(WaitAndTrigger(Target, delay));
        }

        public static IEnumerator WaitAndTrigger(ITriggerable target, ushort delay) {
            if (delay > 0)
                yield return new WaitForSeconds(delay / 10f);

            target.Trigger();
        }
    }

    public abstract class Triggerable<ActionDataType> : Triggerable {

        public ActionDataType ActionData;

        protected override void Awake() {
            base.Awake();

            IActionProvider actionProvider = GetComponentInParent<IActionProvider>();
            ActionData = actionProvider.ActionData.Read<ActionDataType>();
        }

    }

    public interface IActionProvider {
        byte[] ActionData { get; }
    }

    public interface IActionPermission {
        bool CanAct();
    }
}
