using UnityEngine;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.InstanceObjects {
    public partial class Interface : SystemShockObject<ObjectInstance.Interface> {
        public override void InitializeInstance() {
            SystemShockObjectProperties properties = GetComponent<SystemShockObjectProperties>();

            if (properties.Base.DrawType == Resource.DrawType.NoDraw)
                return;
        }
    }
}