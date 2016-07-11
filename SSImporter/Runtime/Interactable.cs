using UnityEngine;
using SystemShock.Resource;
using SystemShock.Object;
using System.Collections;

namespace SystemShock {
    public abstract class Interactable<DataType> : MonoBehaviour {
        protected IActionPermission PermissionProvider;
        public DataType ActionData;
        protected ObjectFactory ObjectFactory;
        protected MessageBus MessageBus;

        protected virtual void Awake() {
            PermissionProvider = GetComponentInParent<IActionPermission>();
            IActionProvider actionProvider = GetComponentInParent<IActionProvider>();
            ActionData = actionProvider.ActionData.Read<DataType>();
            ObjectFactory = ObjectFactory.GetController();
            MessageBus = MessageBus.GetController();
        }

        protected static IEnumerator WaitAndTrigger(TriggerAction target, ushort delay) {
            if (delay > 0)
                yield return new WaitForSeconds(delay / 10f);

            target.Act();
        }

        protected void WaitAndTrigger(ushort objectId, ushort delay) {
            TriggerAction Target = ObjectFactory.Get<TriggerAction>(objectId);
            if (Target != null)
                StartCoroutine(WaitAndTrigger(Target, delay));
        }
    }
}