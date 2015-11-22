using UnityEngine;
using System.Collections;

namespace SystemShock {
    public abstract class Screen : MonoBehaviour {
        [SerializeField, HideInInspector]
        protected Material Material;

        protected Renderer Renderer;

        [SerializeField]
        protected int[] MaterialIndices = new int[0];

        protected virtual void Awake() {
            Renderer = GetComponentInChildren<Renderer>();
        }

        protected virtual void OnEnable() {
            Material[] sharedMaterials = Renderer.sharedMaterials;

            for (int i = 0; i < MaterialIndices.Length; ++i)
                sharedMaterials[MaterialIndices[i]] = Material;

            Renderer.sharedMaterials = sharedMaterials;

            DynamicGI.UpdateMaterials(Renderer);
        }

        // TODO Get rid of this. Create shared screen material and change texture using propery block
        public virtual void SetupMaterial(ref Material material, int[] nullMaterialIndices) {
            MaterialIndices = nullMaterialIndices;

            if (material == null) {
                material = new Material(Shader.Find(@"Standard"));
                material.color = Color.black;
                material.SetFloat(@"_Glossiness", 0.75f); // Add little gloss to screens
                material.SetColor(@"_EmissionColor", Color.white);
                material.EnableKeyword(@"_EMISSION");
            }

            Material = material;
        }
    }
}