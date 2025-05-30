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
    CommandBuffer commandBuffer = new CommandBuffer
    {
        name = bufferName
    };
    CullingResults cullingResults;

    Lighting lighting = new Lighting();

    HiZRenderer cs = new HiZRenderer();

    ComputeShader computeShader;

    bool useDepthTexture = true;
    static int colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment");
    static int depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"); // must be copied to another texture

    public void Render(
        ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        ShadowSettings shadowSettings) // 每帧都会被调用
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.shadowDistance))
        {
            return;
        }

        commandBuffer.BeginSample(SampleName);
        ExecuteBuffer();
        // Do all other setup work here
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject); // set up lighting and shadows
        computeShader = Resources.Load<ComputeShader>("Compute/Zero");
        cs.Setup(context, cullingResults, new HiZRenderer.ComputeSettings
        {
            numThreads = new Vector2Int(8, 8),
            mipCount = 4
        });
        commandBuffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);
        DrawUnsupportedShaders();
        cs.Render(camera); // execute compute shader
        ResetRenderTarget(); // TODO: move to post processing
        DrawGizmos();
        Cleanup();
        Submit();
    }


    private void Setup()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;

        if (useDepthTexture) // TODO: move to post processing
        {
            commandBuffer.GetTemporaryRT(
                colorAttachmentId, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.Default
            );
            commandBuffer.GetTemporaryRT(
                depthAttachmentId, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Point, RenderTextureFormat.Depth
            );
            commandBuffer.SetRenderTarget(
                colorAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        }

        commandBuffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags <= CameraClearFlags.Color,
            flags <= CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        commandBuffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    private void Cleanup()
    {
        lighting.Cleanup();
        cs.Cleanup();
        if (useDepthTexture) // TODO: move to pose processing
        {
            commandBuffer.ReleaseTemporaryRT(colorAttachmentId);
            commandBuffer.ReleaseTemporaryRT(depthAttachmentId);
            ExecuteBuffer();
        }
    }

    private void ResetRenderTarget() // TODO: move to pose processing
    {
        if (useDepthTexture)
        {
            commandBuffer.Blit(colorAttachmentId, BuiltinRenderTextureType.CameraTarget);
            commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            ExecuteBuffer();
        }
    }

    private void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject)
    {
        var sortSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque // 从前到后绘制
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
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque); // 先绘制不透明物体
        drawSettings.SetShaderPassName(0, unlitShaderTagId);
        drawSettings.SetShaderPassName(1, litShaderTagId);
        drawSettings.SetShaderPassName(2, computeVisShaderTagId);
        context.DrawRenderers(cullingResults, ref drawSettings, ref filterSettings);

        context.DrawSkybox(camera); // 在Opaque之后绘制天空盒

        sortSettings.criteria = SortingCriteria.CommonTransparent;
        drawSettings.sortingSettings = sortSettings;
        filterSettings.renderQueueRange = RenderQueueRange.transparent; // 然后绘制透明物体
        context.DrawRenderers(cullingResults, ref drawSettings, ref filterSettings);
    }

    private void Submit()
    {
        commandBuffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    private void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();
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
