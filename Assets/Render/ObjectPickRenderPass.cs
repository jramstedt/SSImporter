using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// TODO Is it possible to replace depth prepass with this?

namespace Render {
    public class ObjectPickerRenderPass : ScriptableRenderPass, IDisposable {
        private static readonly List<ShaderTagId> shaderTagList = new ();

        public RTHandle ObjectPickerRenderTextureHandle;
        public RTHandle ObjectPickerTransparentRenderTextureHandle;
        
        private readonly Material objectIdMaterial;
        private readonly int2 resolution;

        private class PassData {
            public RendererListHandle rendererListHandle;
        }

        public ObjectPickerRenderPass(Material objectIdMaterial, int2 resolution) {
            this.objectIdMaterial = objectIdMaterial;
            this.resolution = resolution;
            
            //shaderTagList.Add(new ShaderTagId("DepthOnly"));
            shaderTagList.Add(new ShaderTagId("SRPDefaultUnlit"));
            shaderTagList.Add(new ShaderTagId("UniversalForward"));
            shaderTagList.Add(new ShaderTagId("UniversalForwardOnly"));
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();
            
            lightData.Reset(); // No lights
            
            var drawSettings = RenderingUtils.CreateDrawingSettings(shaderTagList, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
            drawSettings.perObjectData = PerObjectData.None;
            drawSettings.overrideMaterial = objectIdMaterial;
            drawSettings.overrideMaterialPassIndex = 0;
            drawSettings.overrideShader = null;
            drawSettings.overrideShaderPassIndex = 0;
            var filterSettings = new FilteringSettings(RenderQueueRange.all, ~0);
            
            var rendererListParameters = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
            
            var desc = new RenderTextureDescriptor(resolution.x, resolution.y, GraphicsFormat.R32_UInt, GraphicsFormat.D16_UNorm, 1)
            {
                msaaSamples = 1
            };

            RenderingUtils.ReAllocateHandleIfNeeded(ref ObjectPickerRenderTextureHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "Object Id Texture" );
            RenderingUtils.ReAllocateHandleIfNeeded(ref ObjectPickerTransparentRenderTextureHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "Object Id Transparent Texture" );

            var depth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "Object Picker Depth", true);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData)) {
                var rendererListHandle = renderGraph.CreateRendererList(rendererListParameters);
                passData.rendererListHandle = rendererListHandle;
                
                builder.SetRenderAttachment(renderGraph.ImportTexture(ObjectPickerRenderTextureHandle), 0);
                builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                builder.UseRendererList(passData.rendererListHandle);
                builder.SetRenderFunc<PassData>(ExecutePass);
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData)) {
                var rendererListHandle = renderGraph.CreateRendererList(rendererListParameters);
                passData.rendererListHandle = rendererListHandle;
                
                builder.SetRenderAttachment(renderGraph.ImportTexture(ObjectPickerTransparentRenderTextureHandle), 0);
                builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                builder.UseRendererList(passData.rendererListHandle);
                builder.SetRenderFunc<PassData>(ExecutePass);
            }
        }

        private static void ExecutePass(PassData data, RasterGraphContext context) {
            context.cmd.ClearRenderTarget(false, true, Color.black);
            context.cmd.DrawRendererList(data.rendererListHandle);
        }

        public void Dispose() {
            ObjectPickerRenderTextureHandle?.Release();
            ObjectPickerTransparentRenderTextureHandle?.Release();
        }
    }
}
