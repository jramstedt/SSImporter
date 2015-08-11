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

        private Mesh mesh;

        private void Awake() {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            meshFilter.sharedMesh = mesh = new Mesh();

            //Material material = new Material(Shader.Find(@"UI/Default Font"));

            Material material = new Material(Shader.Find(@"Standard"));
            material.color = Color.white;
            material.SetFloat(@"_Mode", 2f); // Fade
            material.SetFloat(@"_Glossiness", 0f);

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;

            material.EnableKeyword(@"_EMISSION");

            meshRenderer.sharedMaterial = material;
        }

        private void Start() {
            Font.textureRebuilt += OnFontTextureRebuilt;

            Update();

            RebuildMesh();
        }

        private void OnFontTextureRebuilt(Font changedFont) {
            if (changedFont != Font) return;

            RebuildMesh();
        }

        private void RebuildMesh() {
            if (Font == null)
                return;

            meshRenderer.sharedMaterial.mainTexture = Font.material.mainTexture;
            meshRenderer.sharedMaterial.color = Color;
            meshRenderer.sharedMaterial.SetColor(@"_EmissionColor", Color * 0.1f);

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
            settings.color = Color;
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
            if(Font.dynamic)
                Font.RequestCharactersInTexture(text);
        }

        private void OnDestroy() {
            Font.textureRebuilt -= OnFontTextureRebuilt;
        }

        [SerializeField, HideInInspector]
        private string text;
        public string Text {
            get { return text; }
            set { text = value; RebuildMesh(); }
        }

        [SerializeField, HideInInspector]
        private Color color;
        public Color Color {
            get { return color; }
            set { color = value; RebuildMesh(); }
        }

        [SerializeField, HideInInspector]
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