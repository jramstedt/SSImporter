using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SystemShock.Resource {
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteInEditMode]
    public class MeshProjector : MonoBehaviour {
        public Vector3 Size = Vector3.one;
        public Rect UVRect = new Rect(0f, 0f, 1f, 1f);
        public float ThresholdAngle = Mathf.Cos(Mathf.PI / 4f);
        public float Offset = 0.002f;

        protected MeshFilter meshFilter;
        protected MeshRenderer meshRenderer;

        protected Mesh projectedMesh;

        protected virtual void Awake() {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Update() {
            if (transform.hasChanged) {
                Project();
                transform.hasChanged = false;
            } 
        }

        private Vector3[] ClipTriangle(Vector3 Back, Vector3 FaceNormal, Vector3[] vertices, Plane plane) {
            if (vertices.Length != 3)
                throw new ArgumentOutOfRangeException(@"vertices");

            // 1. Calculate distances
            float[] distance = new float[] {
                plane.GetDistanceToPoint(vertices[0]),
                plane.GetDistanceToPoint(vertices[1]),
                plane.GetDistanceToPoint(vertices[2])
            };

            // 2. Cull
            if (Vector3.Dot(FaceNormal, Back) <= ThresholdAngle)
                return null;

            if (distance[0] >= 0f && distance[1] >= 0f && distance[2] >= 0f) // All outside
                return null;

            if (distance[0] <= 0f && distance[1] <= 0f && distance[2] <= 0f) // All inside
                return vertices;

            // 3. Clip
            if (distance[0] > 0 && distance[1] <= 0 && distance[2] <= 0) { // only first vertex is outside
                Array.Resize(ref vertices, 4);
                vertices[3] = Vector3.Lerp(vertices[0], vertices[2], distance[0] / (distance[0] - distance[2]));
                vertices[0] = Vector3.Lerp(vertices[0], vertices[1], distance[0] / (distance[0] - distance[1]));
            } else if (distance[1] > 0 && distance[0] <= 0 && distance[2] <= 0) { // only second vertex is outside
                Array.Resize(ref vertices, 4);
                vertices[3] = vertices[2];
                vertices[2] = Vector3.Lerp(vertices[1], vertices[2], distance[1] / (distance[1] - distance[2]));
                vertices[1] = Vector3.Lerp(vertices[1], vertices[0], distance[1] / (distance[1] - distance[0]));
            } else if (distance[2] > 0 && distance[1] <= 0 && distance[0] <= 0) { // only second vertex is outside
                Array.Resize(ref vertices, 4);
                vertices[3] = Vector3.Lerp(vertices[2], vertices[0], distance[2] / (distance[2] - distance[0]));
                vertices[2] = Vector3.Lerp(vertices[2], vertices[1], distance[2] / (distance[2] - distance[1]));
            } else if (distance[0] < 0 && distance[1] >= 0 && distance[2] >= 0) { // only first vertex is inside
                vertices[1] = Vector3.Lerp(vertices[0], vertices[1], distance[0] / (distance[0] - distance[1]));
                vertices[2] = Vector3.Lerp(vertices[0], vertices[2], distance[0] / (distance[0] - distance[2]));
            } else if (distance[1] < 0 && distance[0] >= 0 && distance[2] >= 0) { // only second vertex is inside
                vertices[0] = Vector3.Lerp(vertices[1], vertices[0], distance[1] / (distance[1] - distance[0]));
                vertices[2] = Vector3.Lerp(vertices[1], vertices[2], distance[1] / (distance[1] - distance[2]));
            } else if (distance[2] < 0 && distance[1] >= 0 && distance[0] >= 0) { // only third vertex is inside
                vertices[1] = Vector3.Lerp(vertices[2], vertices[1], distance[2] / (distance[2] - distance[1]));
                vertices[0] = Vector3.Lerp(vertices[2], vertices[0], distance[2] / (distance[2] - distance[0]));
            }

            return vertices;
        }

        protected Mesh Project(MeshFilter targetMeshFilter) {
            Matrix4x4 boxToMesh = targetMeshFilter.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            Matrix4x4 meshToBox = transform.worldToLocalMatrix * targetMeshFilter.transform.localToWorldMatrix;
            Matrix4x4 meshToClip = Matrix4x4.Scale(Size).inverse * meshToBox;

            Plane[] worldPlanes = new Plane[] {
                new Plane(-transform.up, transform.position + transform.up * (Size.y / 2f)),
                new Plane(-transform.right, transform.position + transform.right * (Size.x / 2f)),
                new Plane(-transform.forward, transform.position + transform.forward * (Size.z / 2f)),
                new Plane(transform.up, transform.position - transform.up * (Size.y / 2f)),
                new Plane(transform.right, transform.position - transform.right * (Size.x / 2f)),
                new Plane(transform.forward, transform.position - transform.forward * (Size.z / 2f))
            };

            Plane[] planes = new Plane[] {
                new Plane(boxToMesh.MultiplyVector(Vector3.up), boxToMesh.MultiplyPoint3x4(Vector3.up * (Size.y / 2f))),
                new Plane(boxToMesh.MultiplyVector(Vector3.right), boxToMesh.MultiplyPoint3x4(Vector3.right * (Size.x / 2f))),
                new Plane(boxToMesh.MultiplyVector(Vector3.forward), boxToMesh.MultiplyPoint3x4(Vector3.forward * (Size.z / 2f))),
                new Plane(boxToMesh.MultiplyVector(Vector3.down), boxToMesh.MultiplyPoint3x4(Vector3.down * (Size.y / 2f))),
                new Plane(boxToMesh.MultiplyVector(Vector3.left), boxToMesh.MultiplyPoint3x4(Vector3.left * (Size.x / 2f))),
                new Plane(boxToMesh.MultiplyVector(Vector3.back), boxToMesh.MultiplyPoint3x4(Vector3.back * (Size.z / 2f)))
            };

            Renderer renderer = targetMeshFilter.GetComponent<Renderer>();
            Mesh targetMesh = null;

            if (!GeometryUtility.TestPlanesAABB(worldPlanes, renderer.bounds))
                return null;

            if (renderer is SkinnedMeshRenderer) {
                targetMesh = new Mesh();
                (renderer as SkinnedMeshRenderer).BakeMesh(targetMesh);
            } else {
#if UNITY_EDITOR
                targetMesh = targetMeshFilter.sharedMesh;
#else
                targetMesh = targetMeshFilter.mesh;
#endif
            }

            int[] targetTriangles = targetMesh.triangles;
            Vector3[] targetVertices = targetMesh.vertices;

            Vector3 back = boxToMesh.MultiplyVector(Vector3.back);

            Queue<Vector3> culledVertices = new Queue<Vector3>();
            List<Vector3> vertices = new List<Vector3>();
            for (int triangleIndex = 0; triangleIndex < targetTriangles.Length; ) {
                culledVertices.Clear();

                Vector3 A = targetVertices[targetTriangles[triangleIndex++]];
                Vector3 B = targetVertices[targetTriangles[triangleIndex++]];
                Vector3 C = targetVertices[targetTriangles[triangleIndex++]];
                Vector3 faceNormal = Vector3.Cross(B - A, C - A).normalized;

                culledVertices.Enqueue(A);
                culledVertices.Enqueue(B);
                culledVertices.Enqueue(C);

                for (int planeIndex = 0; planeIndex < planes.Length; ++planeIndex) {
                    int triangleCount = culledVertices.Count / 3;
                    for (int culledTriangleIndex = 0; culledTriangleIndex < triangleCount; ++culledTriangleIndex) {
                        Vector3[] triangle = new Vector3[] {
                            culledVertices.Dequeue(),
                            culledVertices.Dequeue(),
                            culledVertices.Dequeue()
                        };

                        Vector3[] result = ClipTriangle(back, faceNormal, triangle, planes[planeIndex]);

                        if (result == null)
                            continue;

                        culledVertices.Enqueue(result[0]);
                        culledVertices.Enqueue(result[1]);
                        culledVertices.Enqueue(result[2]);

                        if (result.Length > 3) {
                            culledVertices.Enqueue(result[0]);
                            culledVertices.Enqueue(result[2]);
                            culledVertices.Enqueue(result[3]);
                        }
                    }
                }

                vertices.AddRange(culledVertices);
            }

            if (vertices.Count == 0)
                return null;

            // 4. Project to clipspace & calculate UV
            Vector3[] projectedPositions = new Vector3[vertices.Count];
            Vector2[] projectedUVs = new Vector2[vertices.Count];
            int[] triangles = new int[vertices.Count];

            for (int vertexIndex = 0; vertexIndex < vertices.Count; ++vertexIndex) {
                projectedPositions[vertexIndex] = meshToBox.MultiplyPoint3x4(vertices[vertexIndex]) + (Vector3.back * Offset);
                Vector2 clipSpacePosition = meshToClip.MultiplyPoint3x4(vertices[vertexIndex]);

                projectedUVs[vertexIndex] = new Vector2(
                    Mathf.Lerp(UVRect.xMin, UVRect.xMax, Mathf.InverseLerp(-0.5f, 0.5f, clipSpacePosition.x)),
                    Mathf.Lerp(UVRect.yMin, UVRect.yMax, Mathf.InverseLerp(-0.5f, 0.5f, clipSpacePosition.y))
                );

                triangles[vertexIndex] = vertexIndex;
            }

            Mesh returnMesh = new Mesh();

            returnMesh.vertices = projectedPositions;
            returnMesh.uv = projectedUVs;
            returnMesh.triangles = triangles;

            return returnMesh;
        }

        protected void Project() {

            MeshFilter[] targetMeshFilters = FindObjectsOfType<MeshFilter>();

            if (projectedMesh == null)
                projectedMesh = new Mesh();
            else
                projectedMesh.Clear();

            List<CombineInstance> combineInstances = new List<CombineInstance>();

            int levelGeometryLayer = LayerMask.NameToLayer(@"Level Geometry");
            foreach (MeshFilter targetMeshFilter in targetMeshFilters) {
                if (targetMeshFilter.gameObject.layer != levelGeometryLayer)
                    continue;

#if UNITY_EDITOR
                if (targetMeshFilter == meshFilter || !(targetMeshFilter.sharedMesh ?? targetMeshFilter.mesh).isReadable)
                    continue;
#else
                if (targetMeshFilter == meshFilter || targetMeshFilter.mesh == null || !targetMeshFilter.mesh.isReadable)
                    continue;
#endif

                Mesh mesh = Project(targetMeshFilter);

                if (mesh != null) {
                    combineInstances.Add(new CombineInstance() {
                        mesh = mesh,
                        subMeshIndex = 0,
                        transform = Matrix4x4.identity
                    });
                }
            }

            projectedMesh.CombineMeshes(combineInstances.ToArray(), true, false);
            projectedMesh.RecalculateNormals();
            projectedMesh.Optimize();

            meshFilter.sharedMesh = projectedMesh;

            Bounds meshBounds = projectedMesh.bounds;

            Collider collider = GetComponent<Collider>();
            if (collider != null) {
                if (collider is BoxCollider) {
                    ((BoxCollider)collider).center = meshBounds.center;
                    ((BoxCollider)collider).size = meshBounds.size;
                } else if (collider is SphereCollider) {
                    Vector3 radius = meshBounds.extents;
                    ((SphereCollider)collider).center = meshBounds.center;
                    ((SphereCollider)collider).radius = Mathf.Min(radius.x, radius.y, radius.z);
                } else if (collider is MeshCollider) {
                    ((MeshCollider)collider).sharedMesh = projectedMesh;
                }
            }
        }

        private void OnDrawGizmosSelected() {
            Color color = Color.yellow;
            color.a = 0.05f;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = color;
            Gizmos.DrawCube(Vector3.zero, Size);
        }
    }
}