using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

namespace SystemShock.Resource {
    [RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshRenderer)), ExecuteInEditMode]
    public sealed class MeshText : MonoBehaviour {
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        [SerializeField, HideInInspector]
        private Mesh mesh;

        private void Awake() {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Start() {
            Font.textureRebuilt += OnFontTextureRebuilt;

            Update();
            UpdateTexture();

            if (!Application.isPlaying)
                RebuildMesh();
        }

        private void OnFontTextureRebuilt(Font changedFont) {
            if (changedFont != Font) return;

            RebuildMesh();
        }

        private void UpdateTexture() {
            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetTexture(@"_MainTex", Font.material.mainTexture);
            meshRenderer.SetPropertyBlock(materialPropertyBlock);
        }

        private void RebuildMesh() {
            if (Font == null)
                return;

            if (mesh == null)
                meshFilter.mesh = mesh = new Mesh();

            UpdateTexture();

            textGenerator.Invalidate();

            textGenerator.Populate(text, GetTextGenerationSettings());
            mesh.name = text;

            using(VertexHelper vh = new VertexHelper()) {
                vh.AddUIVertexStream(textGenerator.verts.ToList(), null);

                UIVertex vertex = UIVertex.simpleVert;
                for(int vertexIndex = 0; vertexIndex < vh.currentVertCount; ++vertexIndex) {
                    vh.PopulateUIVertex(ref vertex, vertexIndex);
                    vertex.position.z -= 0.01f;
                    vh.SetUIVertex(vertex, vertexIndex);
                }

                for (int vertexIndex = 0; vertexIndex < textGenerator.vertexCount; vertexIndex += 4) {
                    vh.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
                    vh.AddTriangle(vertexIndex + 2, vertexIndex + 3, vertexIndex);
                }

                vh.FillMesh(mesh);
            }
            
            //mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            ;
            mesh.RecalculateBounds();
        }

        private TextGenerationSettings GetTextGenerationSettings() {
            TextGenerationSettings settings = new TextGenerationSettings();

            settings.textAnchor = TextAnchor.MiddleCenter;
            settings.color = Color.white;
            settings.font = Font;
            settings.pivot = new Vector2(0.5f, 0.5f);
            settings.richText = false;
            settings.lineSpacing = 1f;
            settings.resizeTextForBestFit = false;
            settings.updateBounds = true;
            settings.generateOutOfBounds = true;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.alignByGeometry = true;

            if (Font.dynamic) {
                settings.fontSize = 11;
                settings.fontStyle = FontStyle.Normal;
            }

            return settings;
        }

        private void Update() {
            if (Font.dynamic)
                Font.RequestCharactersInTexture(text);
        }

        private void OnDestroy() {
            Font.textureRebuilt -= OnFontTextureRebuilt;
        }

        [SerializeField]
        private string text;
        public string Text {
            get { return text; }
            set { text = value; RebuildMesh(); }
        }

        [SerializeField]
        private Font font;
        public Font Font {
            get { return font; }
            set { font = value; RebuildMesh(); }
        }

        private TextGenerator textGeneratorCache;
        private TextGenerator textGenerator {
            get { return textGeneratorCache ?? (textGeneratorCache = !string.IsNullOrEmpty(text) ? new TextGenerator(text.Length) : new TextGenerator()); }
        }
    }
}