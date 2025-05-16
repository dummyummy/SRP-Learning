using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");

    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    CommandBuffer commandBuffer = new CommandBuffer
    {
        name = bufferName
    };
    CullingResults cullingResults;

    public void Render(ScriptableRenderContext context, Camera camera) // 每帧都会被调用
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull())
        {
            return;
        }

        Setup();
        DrawVisibleGeometry();
        DrawUnsupportedShaders();
        DrawGizmos();
        Submit();
    }

    private void Setup()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        commandBuffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        commandBuffer.BeginSample(SampleName);
        ExecuteBuffer();
        //context.SetupCameraProperties(camera);
    }

    private void DrawVisibleGeometry()
    {
        var sortSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque // 从前到后绘制
        };
        var drawSettings = new DrawingSettings(unlitShaderTagId, sortSettings);
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque); // 先绘制不透明物体
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

    private bool Cull()
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
}
