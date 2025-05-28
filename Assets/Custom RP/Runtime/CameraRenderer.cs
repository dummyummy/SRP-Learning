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

    ComputeDispatcher cs = new ComputeDispatcher();

    ComputeShader computeShader;

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
        cs.Setup(context, cullingResults, new ComputeDispatcher.ComputeSettings
        {
            computeShader = computeShader,
            kernelName = "CSMain",
            RTWidth = 1024,
            RTHeight = 1024,
            numThreads = new Vector2Int(8, 8)
        });
        cs.Render();
        commandBuffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);
        DrawUnsupportedShaders();
        DrawGizmos();
        lighting.Cleanup();
        cs.Cleanup();
        Submit();
    }

    private void Setup()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        commandBuffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags <= CameraClearFlags.Color,
            flags <= CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        commandBuffer.BeginSample(SampleName);
        ExecuteBuffer();
        //context.SetupCameraProperties(camera);
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
