using UnityEngine.EventSystems;
using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.InstanceObjects {
    public partial class Item : SystemShockObject<ObjectInstance.Item> {
        public SystemShockObjectProperties Properties;

        private void Awake() {
            Properties = GetComponent<SystemShockObjectProperties>();
        }
    }
}