using UnityEngine;
using System.Collections;

namespace SystemShock.UserInterface {
    public interface IRaycastFilter {
        bool TestRaycast(RaycastHit hit);
    }
}