using UnityEngine;
using SystemShock.Resource;
using SystemShock.Object;

namespace SystemShock {
    public class Interactable<DataType> : MonoBehaviour {
        protected IActionPermission PermissionProvider;
        public DataType ActionData;
        protected ObjectFactory ObjectFactory;

        protected virtual void Awake() {
            PermissionProvider = GetComponentInParent<IActionPermission>();
            IActionProvider actionProvider = GetComponentInParent<IActionProvider>();
            ActionData = actionProvider.ActionData.Read<DataType>();
            ObjectFactory = ObjectFactory.GetController();
        }
    }
}