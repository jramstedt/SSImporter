using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using static SS.Resources.ResourceFile;

namespace SS.Resources {
  public class MeshProvider : IResProvider<MeshInfo> {
    private class MeshLoader : LoaderBase<MeshInfo> {
      public MeshLoader(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        InvokeCompletionEvent(Load(resFile, resInfo, blockIndex));
      }

      private unsafe MeshInfo Load(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
        byte[] rawResource = resFile.GetResourceData(resInfo, blockIndex);

        using BlobBuilder blobBuilder = new(Allocator.Temp);
        ref var blobArray = ref blobBuilder.ConstructRoot<BlobArray<byte>>();
        var blobBuilderArray = blobBuilder.Allocate(ref blobArray, rawResource.Length);
        UnsafeUtility.MemCpy(blobBuilderArray.GetUnsafePtr(), UnsafeUtility.PinGCArrayAndGetDataAddress(rawResource, out ulong gcHandle), rawResource.Length);
        UnsafeUtility.ReleaseGCObject(gcHandle);
        var blobAssetReference = blobBuilder.CreateBlobAssetReference<BlobArray<byte>>(Allocator.Persistent);

        return new MeshInfo { Commands = blobAssetReference };
      }
    }

    IResHandle<MeshInfo> IResProvider<MeshInfo>.Provide(ResourceFile resFile, ResourceInfo resInfo, ushort blockIndex) {
      if (resInfo.info.ContentType != ResourceFile.ContentType.Obj3D)
        throw new Exception($"Resource {resInfo.info.Id:X4}:{blockIndex:X4} is not {nameof(ResourceFile.ContentType.Obj3D)}.");

      return new MeshLoader(resFile, resInfo, blockIndex);
    }
  }
  public struct MeshInfo : IComponentData {
    public BlobAssetReference<BlobArray<byte>> Commands;
  }
}
