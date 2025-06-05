using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    static ShaderTagId unlitShaderTagId = new ShaderTagId("CustomUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");
    static ShaderTagId computeVisShaderTagId = new ShaderTagId("ComputeVisualize");

    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    CullingResults cullingResults;

    Lighting lighting = new Lighting();

    //HiZRenderer cs = new HiZRenderer();

    PostFXStack postFXStack = new PostFXStack();

    //bool useDepthTexture = false;
    //static int colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment");
    //static int depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"); // must be copied to another texture
    static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");

    bool useHDR;

    public void Render(
        ScriptableRenderContext context, Camera camera, bool allowHDR,
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.shadowDistance))
        {
            return;
        }
        
        useHDR = allowHDR && camera.allowHDR;

        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        // Do all other setup work here
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject); // set up lighting and shadows
        postFXStack.Setup(context, camera, postFXSettings);
        //cs.Setup(context, cullingResults, new HiZRenderer.ComputeSettings
        //{
        //    numThreads = new Vector2Int(8, 8),
        //    mipCount = 4
        //});
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);
        DrawUnsupportedShaders();
        //cs.Render(camera); // execute compute shader
        //ResetRenderTarget(); // TODO: move to post processing
        DrawGizmosBeforeFX();
        if (postFXStack.IsActive)
        {
            postFXStack.Render(frameBufferId);
        }
        DrawGizmosAfterFX();
        Cleanup();
        Submit();
    }


    private void Setup()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;

        //if (useDepthTexture) // TODO: move to post processing
        //{
        //    commandBuffer.GetTemporaryRT(
        //        colorAttachmentId, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.Default
        //    );
        //    commandBuffer.GetTemporaryRT(
        //        depthAttachmentId, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Point, RenderTextureFormat.Depth
        //    );
        //    commandBuffer.SetRenderTarget(
        //        colorAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
        //        depthAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        //    );
        //}

        if (postFXStack.IsActive)
        {
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }
            buffer.GetTemporaryRT(
                frameBufferId, camera.pixelWidth, camera.pixelHeight,
                32, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
            );
            buffer.SetRenderTarget(frameBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags <= CameraClearFlags.Color,
            flags <= CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    private void Cleanup()
    {
        lighting.Cleanup();
        //cs.Cleanup();
        //if (usedepthtexture) // todo: move to pose processing
        //{
        //    commandbuffer.releasetemporaryrt(colorattachmentid);
        //    commandbuffer.releasetemporaryrt(depthattachmentid);
        //    executebuffer();
        //}
        if (postFXStack.IsActive)
        {
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
    }

    private void ResetRenderTarget() // TODO: move to pose processing
    {
        //if (useDepthTexture)
        //{
        //    commandBuffer.Blit(colorAttachmentId, BuiltinRenderTextureType.CameraTarget);
        //    commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        //    ExecuteBuffer();
        //}
    }

    private void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject)
    {
        var sortSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        PerObjectData lightsPerObjectDataFlags = useLightsPerObject ?
            PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        var drawSettings = new DrawingSettings(unlitShaderTagId, sortSettings)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.Lightmaps | PerObjectData.LightProbe |
                            PerObjectData.LightProbeProxyVolume | PerObjectData.ShadowMask | 
                            PerObjectData.OcclusionProbe | PerObjectData.OcclusionProbeProxyVolume |
                            PerObjectData.ReflectionProbes | lightsPerObjectDataFlags
        };
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque);
        drawSettings.SetShaderPassName(0, unlitShaderTagId);
        drawSettings.SetShaderPassName(1, litShaderTagId);
        drawSettings.SetShaderPassName(2, computeVisShaderTagId);
        context.DrawRenderers(cullingResults, ref drawSettings, ref filterSettings);

        context.DrawSkybox(camera); // draw after opaque geometry to avoid overdraw

        sortSettings.criteria = SortingCriteria.CommonTransparent;
        drawSettings.sortingSettings = sortSettings;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawSettings, ref filterSettings);
    }

    private void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    private void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    private bool Cull(float maxShadowDistance)
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
}
