using UnityEngine;
using SystemShock.Resource;
using System.Collections;

namespace SystemShock {
    public interface IInteractable {
        bool Interact();
    }

    public abstract class Interactable : MonoBehaviour, IInteractable {
        protected IActionPermission PermissionProvider;
        protected ObjectFactory ObjectFactory;
        protected MessageBus MessageBus;

        public virtual bool Interact() {
            if (PermissionProvider.CanAct())
                return DoInteraction();

            return false;
        }

        protected abstract bool DoInteraction();

        protected virtual void Awake() {
            PermissionProvider = GetComponentInParent<IActionPermission>();

            ObjectFactory = ObjectFactory.GetController();
            MessageBus = MessageBus.GetController();
        }

        protected void WaitAndTrigger(ushort objectId, ushort delay) {
            ITriggerable Target = ObjectFactory.Get<ITriggerable>(objectId);
            if (Target != null)
                StartCoroutine(Triggerable.WaitAndTrigger(Target, delay));
        }
    }

    public abstract class Interactable<DataType> : Interactable {
        public DataType ActionData;

        protected override void Awake() {
            base.Awake();

            IActionProvider actionProvider = GetComponentInParent<IActionProvider>();
            ActionData = actionProvider.ActionData.Read<DataType>();
        }
    }
}