using UnityEngine;
using System.Collections;

using SystemShock.Object;

namespace SSImporter.Resource {
    public static class MeshUtils {
        public static Mesh CreateTwoSidedPlane() { return CreateTwoSidedPlane(new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), new Rect(0f, 0f, 1f, 1f)); }
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
    }
}