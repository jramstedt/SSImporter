using SS.Resources;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
using Vertex = SS.Data.Vertex;

namespace SS {
    public static class MeshUtils {
        public const byte BIGSTUFF_MODEL_XY_SHF = 13;
        public const byte BIGSTUFF_MODEL_Z_SHF = 10;
        public const byte CONTAINER_MODEL_SHF = 10;
        
        public static readonly NativeParallelHashMap<int, (byte SizeX, byte SizeY, byte SizeZ, byte SideTexture, byte TopBottomTexture)> DefaultSpecialMeshParams;

        static MeshUtils() {
            DefaultSpecialMeshParams = new (8, Allocator.Persistent) {
                [0x70700] = (0x04, 0x04, 0x01, 0x80, 0x80),
                [0x70701] = (0x02, 0x04, 0x01, 0x80, 0x80),
                [0x70706] = (0x02, 0x02, 0xB0, 0x80, 0x81),
                [0x70707] = (0x02, 0x04, 0x01, 0x80, 0x80),
                [0x70709] = (0x00, 0x00, 0x00, 0x00, 0x00), // ??
                [0x80509] = (0x04, 0x01, 0x10, 0x00, 0x00),
                [0xD0000] = (0x08, 0x08, 0x08, 0x0C, 0x0B),
                [0xD0001] = (0x10, 0x10, 0x10, 0x0C, 0x0B),
                [0xD0002] = (0x20, 0x20, 0x20, 0x0A, 0x0A),
            };
        }
        
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
