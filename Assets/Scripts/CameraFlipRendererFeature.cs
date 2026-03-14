using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CameraFlipRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public Settings settings = new Settings();
    CameraFlipRenderPass pass;

    public override void Create()
    {
        pass = new CameraFlipRenderPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null)
            return;

        var flipComponent = renderingData.cameraData.camera.GetComponent<CameraFlip>();
        if (flipComponent == null || !flipComponent.enabled)
            return;

        settings.material.SetFloat("_FlipX", flipComponent.flipX ? 1f : 0f);
        settings.material.SetFloat("_FlipY", flipComponent.flipY ? 1f : 0f);

        if (!flipComponent.flipX && !flipComponent.flipY)
            return;

        renderer.EnqueuePass(pass);
    }

    class CameraFlipRenderPass : ScriptableRenderPass
    {
        Settings settings;

        public CameraFlipRenderPass(Settings settings)
        {
            this.settings = settings;
            renderPassEvent = settings.renderPassEvent;
        }

        RTHandle tempRTHandle;

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateHandleIfNeeded(ref tempRTHandle, descriptor, name: "_CameraFlipTempRT");
        }

        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("CameraFlip");
            var cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

            Blitter.BlitCameraTexture(cmd, cameraColorTarget, tempRTHandle, settings.material, 0);
            Blitter.BlitCameraTexture(cmd, tempRTHandle, cameraColorTarget);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        public void Dispose()
        {
            tempRTHandle?.Release();
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraColorHandle = resourceData.activeColorTexture;

            if (!cameraColorHandle.IsValid())
                return;

            var descriptor = renderGraph.GetTextureDesc(cameraColorHandle);
            descriptor.name = "_CameraFlipTempRT";
            descriptor.clearBuffer = false;

            var tempTexture = renderGraph.CreateTexture(descriptor);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("CameraFlip_Copy", out var passData))
            {
                passData.source = cameraColorHandle;
                passData.material = settings.material;

                builder.UseTexture(cameraColorHandle, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    data.material.SetTexture("_BlitTexture", data.source);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("CameraFlip_Blit", out var passData))
            {
                passData.source = tempTexture;

                builder.UseTexture(tempTexture, AccessFlags.Read);
                builder.SetRenderAttachment(cameraColorHandle, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        class PassData
        {
            public TextureHandle source;
            public Material material;
        }
    }
}
