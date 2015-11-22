using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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

            //Debug.LogFormat(gameObject, "Verts {0} {1}", text, textGenerator.vertexCount);
            /*
            Rect extents = textGenerator.rectExtents;
            Vector2 refPoint = Vector2.zero;

            Vector3[] vertices = new Vector3[textGenerator.verts.Count];
            for(int vertexIndex = 0; vertexIndex < vertices.Length; ++vertexIndex) {
                Vector3 position = textGenerator.verts[vertexIndex].position;
                position.x -= extents.width / 2f;
                position.y -= extents.height / 2f;
                vertices[vertexIndex] = position;
            }

            mesh.vertices = vertices;
            */

            mesh.vertices = textGenerator.verts.Select(v => v.position).ToArray();
            mesh.colors32 = textGenerator.verts.Select(v => v.color).ToArray();
            mesh.normals = textGenerator.verts.Select(v => v.normal).ToArray();
            mesh.tangents = textGenerator.verts.Select(v => v.tangent).ToArray();
            mesh.uv = textGenerator.verts.Select(v => v.uv0).ToArray();
            mesh.uv2 = textGenerator.verts.Select(v => v.uv1).ToArray();

            int[] triangles = new int[(textGenerator.vertexCount / 4) * 6];
            for (int triangleIndex = 0, vertexIndex = 0; triangleIndex < triangles.Length; vertexIndex += 4) {
                triangles[triangleIndex++] = vertexIndex;
                triangles[triangleIndex++] = vertexIndex + 1;
                triangles[triangleIndex++] = vertexIndex + 2;
                triangles[triangleIndex++] = vertexIndex;
                triangles[triangleIndex++] = vertexIndex + 2;
                triangles[triangleIndex++] = vertexIndex + 3;
            }

            mesh.triangles = triangles;

            //mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.Optimize();
            mesh.RecalculateBounds();
        }

        private TextGenerationSettings GetTextGenerationSettings() {
            TextGenerationSettings settings = new TextGenerationSettings();

            settings.textAnchor = TextAnchor.LowerLeft;
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