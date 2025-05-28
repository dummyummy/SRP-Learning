using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    static ShaderTagId unlitShaderTagId = new ShaderTagId("CustomUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    CommandBuffer commandBuffer = new CommandBuffer
    {
        name = bufferName
    };
    CullingResults cullingResults;

    Lighting lighting = new Lighting();

    public void Render(
        ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        ShadowSettings shadowSettings) // ÿ֡���ᱻ����
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
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject); // set up lighting and shadows
        commandBuffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);
        DrawUnsupportedShaders();
        DrawGizmos();
        lighting.Cleanup();
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
            criteria = SortingCriteria.CommonOpaque // ��ǰ�������
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
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque); // �Ȼ��Ʋ�͸������
        drawSettings.SetShaderPassName(0, unlitShaderTagId);
        drawSettings.SetShaderPassName(1, litShaderTagId);
        context.DrawRenderers(cullingResults, ref drawSettings, ref filterSettings);

        context.DrawSkybox(camera); // ��Opaque֮�������պ�

        sortSettings.criteria = SortingCriteria.CommonTransparent;
        drawSettings.sortingSettings = sortSettings;
        filterSettings.renderQueueRange = RenderQueueRange.transparent; // Ȼ�����͸������
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
