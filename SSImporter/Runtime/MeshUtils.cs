using UnityEngine;
using System.Collections;

using SystemShock.Object;

namespace SystemShock.Resource {
    public static class MeshUtils {
        public static Mesh CreateTwoSidedPlane() { return CreateTwoSidedPlane(new Vector2(1f, 1f)); }
        public static Mesh CreateTwoSidedPlane(Vector2 size) { return CreateTwoSidedPlane(new Vector2(0.5f, 0.5f), size); }
        public static Mesh CreateTwoSidedPlane(Vector2 pivot, Vector2 size) { return CreateTwoSidedPlane(pivot, size, new Rect(0f, 0f, 1f, 1f)); }
        public static Mesh CreateTwoSidedPlane(Vector2 pivot, Vector2 size, Rect uvRect) {
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[] {
                new Vector3(-pivot.x * size.x, -pivot.y * size.y, 0f), new Vector3(-pivot.x * size.x, (1f-pivot.y) * size.y, 0f), new Vector3((1f-pivot.x) * size.x, (1f-pivot.y) * size.y, 0f), new Vector3((1f-pivot.x) * size.x, -pivot.y * size.y, 0f),
                new Vector3(-pivot.x * size.x, -pivot.y * size.y, 0f), new Vector3(-pivot.x * size.x, (1f-pivot.y) * size.y, 0f), new Vector3((1f-pivot.x) * size.x, (1f-pivot.y) * size.y, 0f), new Vector3((1f-pivot.x) * size.x, -pivot.y * size.y, 0f)
            };
            mesh.uv = new Vector2[] {
                uvRect.min, new Vector2(uvRect.xMin, uvRect.yMax), uvRect.max, new Vector2(uvRect.xMax, uvRect.yMin),
                uvRect.min, new Vector2(uvRect.xMin, uvRect.yMax), uvRect.max, new Vector2(uvRect.xMax, uvRect.yMin),
            };

            mesh.triangles = new int[] {
                0, 1, 2, 2, 3, 0,
                6, 5, 4, 4, 7, 6
            };

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.Optimize();
            mesh.RecalculateBounds();

            return mesh;
        }

        public static Mesh CreatePlane() { return CreatePlane(new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), new Rect(0f, 0f, 1f, 1f)); }
        public static Mesh CreatePlane(Vector2 pivot, Vector2 size, Rect uvRect) {
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[] {
                new Vector3(-pivot.x * size.x, -pivot.y * size.y, 0f), new Vector3(-pivot.x * size.x, (1f-pivot.y) * size.y, 0f), new Vector3((1f-pivot.x) * size.x, (1f-pivot.y) * size.y, 0f), new Vector3((1f-pivot.x) * size.x, -pivot.y * size.y, 0f)
            };
            mesh.uv = new Vector2[] {
                uvRect.min, new Vector2(uvRect.xMin, uvRect.yMax), uvRect.max, new Vector2(uvRect.xMax, uvRect.yMin),
            };

            mesh.triangles = new int[] {
                0, 1, 2, 2, 3, 0
            };

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.Optimize();
            mesh.RecalculateBounds();

            return mesh;
        }

        public static Mesh CreateCubeTopPivot(float width = 1f, float length = 1f, float height = 0.025f) {
            return CreateCube(new Vector3(0.5f, 0f, 0.5f), width, length, height);
        }

        public static Mesh CreateCubeCenterPivot(Vector3 size) {
            return CreateCube(new Vector3(0.5f, 0.5f, 0.5f), size);
        }

        public static Mesh CreateCube(Vector3 pivot, Vector3 size) {
            return CreateCube(pivot, size.x, size.z, size.y);
        }

        public static Mesh CreateCube(Vector3 pivot, float width = 1f, float length = 1f, float height = 0.025f) {
            Mesh mesh = new Mesh();

            Vector3[] vertexTemplate = new Vector3[] {
                new Vector3(-pivot.x * width, (1f-pivot.y) * height, -pivot.z * length), new Vector3(-pivot.x * width, (1f-pivot.y) * height, (1f-pivot.z) * length), new Vector3((1f - pivot.x) * width, (1f-pivot.y) * height, (1f-pivot.z) * length), new Vector3((1f - pivot.x) * width, (1f-pivot.y) * height, -pivot.z * length), // Top
                new Vector3(-pivot.x * width, -pivot.y * height, -pivot.z * length), new Vector3(-pivot.x * width, -pivot.y * height, (1f-pivot.z) * length), new Vector3((1f - pivot.x) * width, -pivot.y * height, (1f-pivot.z) * length), new Vector3((1f - pivot.x) * width, -pivot.y * height, -pivot.z * length) // Bottom
            };

            mesh.vertices = new Vector3[] {
                vertexTemplate[0], vertexTemplate[1], vertexTemplate[2], vertexTemplate[3], // Top
                vertexTemplate[4], vertexTemplate[5], vertexTemplate[6], vertexTemplate[7], // Bottom
                vertexTemplate[4], vertexTemplate[0], vertexTemplate[3], vertexTemplate[7], // Back
                vertexTemplate[7], vertexTemplate[3], vertexTemplate[2], vertexTemplate[6], // Left
                vertexTemplate[6], vertexTemplate[2], vertexTemplate[1], vertexTemplate[5], // Front 
                vertexTemplate[5], vertexTemplate[1], vertexTemplate[0], vertexTemplate[4], // Right
            };

            mesh.uv = new Vector2[] {
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f)
            };

            mesh.subMeshCount = 2;

            mesh.SetTriangles(new int[] {
                0, 1, 2, 2, 3, 0, // Top
                6, 5, 4, 4, 7, 6, // Bottom
            }, 0);

            mesh.SetTriangles(new int[] {
                8, 9, 10, 10, 11, 8, // Back
                12, 13, 14, 14, 15, 12, // Left
                16, 17, 18, 18, 19, 16, // Front
                20, 21, 22, 22, 23, 20  // Right
            }, 1);

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.Optimize();
            mesh.RecalculateBounds();

            return mesh;
        }

        /* Derived from
         * Lengyel, Eric. "Computing Tangent Space Basis Vectors for an Arbitrary Mesh". Terathon Software 3D Graphics Library, 2001.
         * [url]http://www.terathon.com/code/tangent.html[/url]
         */
        public static void RecalculateTangents(this Mesh mesh) {
            int vertexCount = mesh.vertexCount;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] texcoords = mesh.uv;
            int[] triangles = mesh.triangles;

            Vector4[] tangents = new Vector4[vertexCount];
            Vector3[] tan1 = new Vector3[vertexCount];
            Vector3[] tan2 = new Vector3[vertexCount];

            int tri = 0;
            while (tri < triangles.Length) {
                int i1 = triangles[tri++];
                int i2 = triangles[tri++];
                int i3 = triangles[tri++];

                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];
                Vector3 v3 = vertices[i3];

                Vector2 w1 = texcoords[i1];
                Vector2 w2 = texcoords[i2];
                Vector2 w3 = texcoords[i3];

                float x1 = v2.x - v1.x;
                float x2 = v3.x - v1.x;
                float y1 = v2.y - v1.y;
                float y2 = v3.y - v1.y;
                float z1 = v2.z - v1.z;
                float z2 = v3.z - v1.z;

                float s1 = w2.x - w1.x;
                float s2 = w3.x - w1.x;
                float t1 = w2.y - w1.y;
                float t2 = w3.y - w1.y;

                float r = 1.0f / (s1 * t2 - s2 * t1);
                Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

                tan1[i1] += sdir;
                tan1[i2] += sdir;
                tan1[i3] += sdir;

                tan2[i1] += tdir;
                tan2[i2] += tdir;
                tan2[i3] += tdir;
            }

            for (int i = 0; i < vertexCount; ++i) {
                Vector3 n = normals[i];
                Vector3 t = tan1[i];

                // Gram-Schmidt orthogonalize
                Vector3.OrthoNormalize(ref n, ref t);

                tangents[i].x = t.x;
                tangents[i].y = t.y;
                tangents[i].z = t.z;

                // Calculate handedness
                tangents[i].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0.0f) ? -1.0f : 1.0f;
            }

            mesh.tangents = tangents;
        }
    }
}