using UnityEngine;

using SystemShock.Resource;
using SystemShock.Object;

namespace SystemShock.DataObjects {
    public partial class DecorationAllMonoBehaviour : ISystemShockObjectPrefab {
        public void Setup(byte classIndex, byte subclassIndex, byte typeIndex) {
            if (subclassIndex == 2) {
                if (typeIndex == 3) { // Text
                    DestroyImmediate(GetComponent<MeshProjector>());
                    DestroyImmediate(GetComponent<MeshFilter>());

                    TextMesh textMesh = gameObject.AddComponent<TextMesh>();
                    textMesh.offsetZ = -0.0001f;
                    textMesh.alignment = TextAlignment.Center;
                    textMesh.anchor = TextAnchor.MiddleCenter;
                    textMesh.richText = false;
                }
            } else if (subclassIndex == 3) {
                Light light = gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 4f;
                light.shadows = LightShadows.Soft;
            }
        }
    }
}
