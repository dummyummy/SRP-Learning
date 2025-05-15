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

    public void Render(ScriptableRenderContext context, Camera camera) // ÿ֡���ᱻ����
    {
        this.context = context;
        this.camera = camera;

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
        commandBuffer.ClearRenderTarget(true, true, Color.clear);
        commandBuffer.BeginSample(bufferName);
        ExecuteBuffer();
        //context.SetupCameraProperties(camera);
    }

    private void DrawVisibleGeometry()
    {
        var sortSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque // ��ǰ�������
        };
        var drawSettings = new DrawingSettings(unlitShaderTagId, sortSettings);
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque); // �Ȼ��Ʋ�͸������
        context.DrawRenderers(cullingResults, ref drawSettings, ref filterSettings);

        context.DrawSkybox(camera); // ��Opaque֮�������պ�

        sortSettings.criteria = SortingCriteria.CommonTransparent;
        drawSettings.sortingSettings = sortSettings;
        filterSettings.renderQueueRange = RenderQueueRange.transparent; // Ȼ�����͸������
        context.DrawRenderers(cullingResults, ref drawSettings, ref filterSettings);
    }

    private void Submit()
    {
        commandBuffer.EndSample(bufferName);
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
