using UnityEngine;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.InstanceObjects {
    public partial class Container : SystemShockObject<ObjectInstance.Container> {
        protected override void InitializeInstance() {
            SystemShockObjectProperties properties = GetComponent<SystemShockObjectProperties>();

            if (properties.Base.DrawType == Resource.DrawType.NoDraw)
                return;

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            MeshFilter meshFilter = GetComponent<MeshFilter>();

            if (SubClass == 0) { // Crates
                KnownChunkId materialBase = KnownChunkId.DynamicModelTexturesStart;

                TextureLibrary textureLibrary = ResourceLibrary.GetController().TextureLibrary;

                Material topBottomMaterial = textureLibrary.GetResource(materialBase + (ushort)(ClassData.TopBottomTexture > 0 ? ClassData.TopBottomTexture : (byte)12));
                Material sideMaterial = textureLibrary.GetResource(materialBase + (ushort)(ClassData.SideTexture > 0 ? ClassData.SideTexture : (byte)11));

                Vector3 size = new Vector3( ClassData.Width > 0 ? ClassData.Width / 32f : properties.Base.Size.x,
                                            ClassData.Height > 0 ? ClassData.Height / 32f : properties.Base.Size.y,
                                            ClassData.Depth > 0 ? ClassData.Depth / 32f : properties.Base.Size.x);

                meshFilter.sharedMesh = MeshUtils.CreateCubeCenterPivot(size);
                meshRenderer.sharedMaterials = new Material[] {
                    topBottomMaterial,
                    sideMaterial
                };

                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                BoxCollider boxCollider = GetComponent<BoxCollider>();
                boxCollider.center = meshBounds.center;
                boxCollider.size = meshBounds.size;
            }
        }
    }
}

