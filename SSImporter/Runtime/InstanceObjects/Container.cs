using UnityEngine;
using System.Collections;

using SystemShock.Object;
using SystemShock.Resource;

namespace SystemShock.InstanceObjects {
    public partial class Container : SystemShockObject<ObjectInstance.Container> {
        public override void InitializeInstance() {
            SystemShockObjectProperties properties = GetComponent<SystemShockObjectProperties>();

            if (properties.Base.DrawType == Resource.DrawType.NoDraw)
                return;

            SystemShockObject ssobject = GetComponent<SystemShockObject>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            MeshFilter meshFilter = GetComponent<MeshFilter>();

            if (ssobject.SubClass == 0) { // Crates
                TextureLibrary modelTextureLibrary = TextureLibrary.GetLibrary(@"citmat.res");

                ushort materialBase = 51;

                Material topBottomMaterial = modelTextureLibrary.GetMaterial(ClassData.TopBottomTexture > 0 ? (ushort)(materialBase + ClassData.TopBottomTexture) : (ushort)63);
                Material sideMaterial = modelTextureLibrary.GetMaterial(ClassData.SideTexture > 0 ? (ushort)(materialBase + ClassData.SideTexture) : (ushort)62);

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

