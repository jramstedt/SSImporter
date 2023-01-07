using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SS.Resources {
  public unsafe class MeshProvider : ResourceProviderBase {
    private Dictionary<string, MeshInfo> cachedMeshInfos = new Dictionary<string, MeshInfo>();

    public override Type GetDefaultType(IResourceLocation location) => typeof(MeshInfo);

    public override void Provide(ProvideHandle provideHandle) {
      var location = provideHandle.Location;
      var key = provideHandle.ResourceManager.TransformInternalId(location);

      if (cachedMeshInfos.TryGetValue(key, out MeshInfo meshInfo)) {
        provideHandle.Complete(meshInfo, true, null);
        return;
      }

      var resFile = provideHandle.GetDependency<ResourceFile>(0);
      if (resFile == null) {
        provideHandle.Complete<MeshInfo>(default, false, new Exception($"Resource file failed to load for location {location.PrimaryKey}."));
        return;
      }

      if (!Utils.ExtractResourceIdAndBlock(key, out ushort resId, out ushort block)) {
        provideHandle.Complete<MeshInfo>(default, false, new Exception($"Resource {location.InternalId} with key {key} is not valid."));
        return;
      }

      if (resFile.GetResourceInfo(resId).info.ContentType != ResourceFile.ContentType.Obj3D) {
        provideHandle.Complete<MeshInfo>(default, false, new Exception($"Resource {location.InternalId} is not {nameof(ResourceFile.ContentType.Obj3D)}."));
        return;
      }

      var commands = resFile.GetResourceData(resId, block);

      using BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp);
      ref var commandsBlobArray = ref blobBuilder.ConstructRoot<BlobArray<byte>>();
      var commandBlobArray = blobBuilder.Allocate(ref commandsBlobArray, commands.Length);
      UnsafeUtility.MemCpy(commandBlobArray.GetUnsafePtr(), UnsafeUtility.PinGCArrayAndGetDataAddress(commands, out ulong gcHandle), commands.Length);
      UnsafeUtility.ReleaseGCObject(gcHandle);
      var blobAssetReference = blobBuilder.CreateBlobAssetReference<BlobArray<byte>>(Allocator.Persistent);

      meshInfo = new MeshInfo { Commands = blobAssetReference };
      provideHandle.Complete(meshInfo, cachedMeshInfos.TryAdd(key, meshInfo), null);
    }
  }

  public struct MeshInfo : IComponentData {
    public BlobAssetReference<BlobArray<byte>> Commands;
  }
}
