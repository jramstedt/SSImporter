using SS.Resources;
using UnityEngine;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
using Vertex = SS.Data.Vertex;

namespace SS {
    public static class MeshUtils {
        public static void BuildPlaneMesh(Mesh mesh, Bitmap bitmapDescription, float scale, bool centerPivot, bool doubleSided) {
            var pivot = bitmapDescription.AnchorPoint;
            var size = bitmapDescription.Size;

            if (pivot.x <= 0 && pivot.y <= 0) {
                pivot.x = (short)(size.x >> 1);
                pivot.y = centerPivot ? (short)(size.y >> 1) : (short)(size.y - 1);
            }
            
            mesh.SetVertexBufferParams(4,
                new VertexAttributeDescriptor(VertexAttribute.Position),
                new VertexAttributeDescriptor(VertexAttribute.Normal),
                new VertexAttributeDescriptor(VertexAttribute.Tangent),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 1)
            );

            mesh.SetVertexBufferData(new[] {
                new Vertex { pos = float3(-pivot.x, pivot.y, 0f) * scale, uv = half2(half(0f), half(1f)), light = 1f },
                new Vertex { pos = float3(size.x-pivot.x, pivot.y, 0f) * scale, uv = half2(half(1f), half(1f)), light = 1f },
                new Vertex { pos = float3(size.x-pivot.x, -(size.y-pivot.y), 0f) * scale, uv = half2(half(1f), half(0f)), light = 0f },
                new Vertex { pos = float3(-pivot.x, -(size.y-pivot.y), 0f) * scale, uv = half2(half(0f), half(0f)), light = 0f },
            }, 0, 0, 4);

            mesh.subMeshCount = 1;

            if (doubleSided) {
                mesh.SetIndexBufferParams(12, IndexFormat.UInt16);
                mesh.SetIndexBufferData(new ushort[] { 0, 1, 2, 2, 3, 0, 2, 1, 0, 0, 3, 2 }, 0, 0, 12);
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, 12, MeshTopology.Triangles));
            } else {
                mesh.SetIndexBufferParams(6, IndexFormat.UInt16);
                mesh.SetIndexBufferData(new ushort[] { 0, 1, 2, 2, 3, 0 }, 0, 0, 6);
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, 6, MeshTopology.Triangles));
            }

            mesh.RecalculateNormals();
            // mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
        }
    }
}
