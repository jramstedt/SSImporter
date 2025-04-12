#if false
using System;
using Render;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NotImplementedException = System.NotImplementedException;

namespace SS.System {
    public partial class ObjectPickerSystem : SystemBase {
        private NativeArray<uint> objectIds;
        private NativeArray<uint> objectIdsTransparent;
        private bool[] runningRequests;

        private ObjectPickerRenderPass objectPickerRenderPass;

        private bool runningGPUReadbackRequest;

        protected override void OnCreate() {
            var material = new Material(Shader.Find(@"Universal Render Pipeline/Unlit"));
            var resolution = new int2(Screen.width >> 2, Screen.height >> 2);
            objectPickerRenderPass = new ObjectPickerRenderPass(material, resolution);
            objectPickerRenderPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            
            objectIds = new NativeArray<uint>(Screen.width * Screen.height, Allocator.Persistent);
            objectIdsTransparent = new NativeArray<uint>(Screen.width * Screen.height, Allocator.Persistent);

            runningRequests = new bool[2];
        }

        protected override void OnDestroy() {
            AsyncGPUReadback.WaitAllRequests();
            objectPickerRenderPass.Dispose();
            objectIds.Dispose();
        }
        
        protected override void OnStartRunning() {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera camera) {
            if (camera.cameraType != CameraType.Game) return;
            
            // TODO update meshes?
            
            camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(objectPickerRenderPass);
        }

        protected override void OnUpdate() {
            // TODO reads are longer than one frame
            
            if (objectPickerRenderPass.ObjectPickerRenderTextureHandle != null)
                RunAsyncGPUReadback(objectPickerRenderPass.ObjectPickerRenderTextureHandle, objectIds, 0);
            
            if (objectPickerRenderPass.ObjectPickerTransparentRenderTextureHandle != null)
                RunAsyncGPUReadback(objectPickerRenderPass.ObjectPickerTransparentRenderTextureHandle, objectIdsTransparent, 1);
        }

        private void RunAsyncGPUReadback(RTHandle textureHandle, NativeArray<uint> target, int semaphore) {
            Texture texture = textureHandle;
            
            if (!SystemInfo.supportsAsyncGPUReadback) {
                if (texture is Texture2D { isReadable: true } texture2D)
                    target.CopyFrom(texture2D.GetRawTextureData<uint>());
                else
                    throw new Exception("Texture is not readable");
            } else {
                if (runningRequests[semaphore]) return;
                runningRequests[semaphore] = true;

                AsyncGPUReadback.RequestIntoNativeArray(ref target, texture, 0, request => { runningRequests[semaphore] = false; });
            }
        }

        protected override void OnStopRunning() {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        }
    }
}
#endif
