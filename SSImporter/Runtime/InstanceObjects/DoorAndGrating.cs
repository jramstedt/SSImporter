using UnityEngine;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;
using System;
using SystemShock.UserInterface;

namespace SystemShock.InstanceObjects {
    public partial class DoorAndGrating : SystemShockObject<ObjectInstance.DoorAndGrating> {
        [SerializeField, HideInInspector]
        protected Color colorOverride;
        [SerializeField, HideInInspector]
        protected Color emissionOverride;

        protected void Start() {
            SystemShockObjectProperties properties = GetComponent<SystemShockObjectProperties>();
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (properties.Base.DrawType == Resource.DrawType.ForceDoor && meshRenderer) {
                MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(materialPropertyBlock);
                materialPropertyBlock.SetColor(@"_Color", colorOverride);
                materialPropertyBlock.SetColor(@"_EmissionColor", emissionOverride);
                meshRenderer.SetPropertyBlock(materialPropertyBlock);
            }
        }

        protected override void InitializeInstance() {
            SystemShockObjectProperties properties = GetComponent<SystemShockObjectProperties>();

            if (properties.Base.DrawType == Resource.DrawType.NoDraw)
                return;

            if (properties.Base.DrawType == Resource.DrawType.ForceDoor) {
                colorOverride = new Color(0.5f, 0f, 0f, 0.75f);
                emissionOverride = new Color(0.25f, 0f, 0f, 1f);

                if (ClassData.ForceColor == 253) {
                    colorOverride = new Color(0f, 0f, 0.75f, 0.75f);
                    emissionOverride = new Color(0f, 0f, 0.4f, 1f);
                } else if (ClassData.ForceColor == 254) {
                    colorOverride = new Color(0f, 0.75f, 0f, 0.75f);
                    emissionOverride = new Color(0f, 0.4f, 0f, 1f);
                }
            }
        }
    }
}

