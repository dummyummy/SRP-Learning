using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ComputeDispatcher
{
    public struct ComputeSettings
    {
        public ComputeShader computeShader;
        public string kernelName;
        public int RTWidth;
        public int RTHeight;
        public Vector2Int numThreads;
    }
    const string bufferName = "Compute";

    CommandBuffer buffer = new CommandBuffer { name = bufferName };

    ScriptableRenderContext context;

    CullingResults cullingResults;

    // Compute shader properties
    ComputeShader computeShader;
    int kernelIndex;
    int RTWidth, RTHeight;
    Vector2Int numThreads;

    static int computeResultId = Shader.PropertyToID("_ComputeResult");

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ComputeSettings computeSettings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        computeShader = computeSettings.computeShader;
        kernelIndex = computeShader.FindKernel(computeSettings.kernelName);
        RTWidth = computeSettings.RTWidth;
        RTHeight = computeSettings.RTHeight;
        numThreads = computeSettings.numThreads;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Render()
    {
        buffer.GetTemporaryRT(
            computeResultId,
            RTWidth, RTHeight,
            32,
            FilterMode.Bilinear,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default,
            1, // no anti-aliasing
            true // enable random read/write access
        );
        buffer.BeginSample(bufferName);
        buffer.SetComputeTextureParam(computeShader, kernelIndex, "Result", computeResultId);
        buffer.DispatchCompute(computeShader, kernelIndex, 
            Mathf.CeilToInt(RTWidth / (float)numThreads.x), Mathf.CeilToInt(RTHeight / (float)numThreads.y), 1);
        buffer.SetGlobalTexture("_ComputeResult", computeResultId);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(computeResultId);
        ExecuteBuffer();
    }
}
