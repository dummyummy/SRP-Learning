using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class HiZRenderer
{
    public struct ComputeSettings
    {
        public Vector2Int numThreads;
        public int mipCount;
    }
    const string bufferName = "Compute Shader";

    CommandBuffer buffer = new CommandBuffer { name = bufferName };

    ScriptableRenderContext context;

    CullingResults cullingResults;

    // Compute shader properties
    ComputeShader BlitDepthCS, HiZCS;
    int BlitDepthCSKernel, HiZCSKernel;
    int RTWidth, RTHeight;
    Vector2Int numThreads;
    int mipCount;

    static int depthTextureId = Shader.PropertyToID("_CameraDepthTexture");
    static int depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"); // must be copied to another texture

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ComputeSettings computeSettings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        BlitDepthCS = Resources.Load<ComputeShader>("Compute/BlitDepth");
        HiZCS = Resources.Load<ComputeShader>("Compute/HiZ");
        BlitDepthCSKernel = BlitDepthCS.FindKernel("CSMain");
        HiZCSKernel = HiZCS.FindKernel("CSMain");
        numThreads = computeSettings.numThreads;
        mipCount = computeSettings.mipCount;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Render(Camera camera)
    {
        RTWidth = camera.pixelWidth;
        RTHeight = camera.pixelHeight;

        var RTSettings = new RenderTextureDescriptor(
            RTWidth, RTHeight, RenderTextureFormat.RFloat, 0, mipCount
        ) {
            useMipMap = true,
            autoGenerateMips = false,
            msaaSamples = 1, // no anti-aliasing
            enableRandomWrite = true,
            dimension = TextureDimension.Tex2D
        };
        buffer.GetTemporaryRT(
            depthTextureId,
            RTSettings,
            FilterMode.Point
        );

        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        // TODO: Compute Z-buffer
        // First Step: Copy depth attachment to a texture at miplevel 0
        buffer.BeginSample(bufferName + " Blit Depth");
        buffer.SetComputeTextureParam(BlitDepthCS, BlitDepthCSKernel, "_DepthTex", depthAttachmentId);
        buffer.SetComputeTextureParam(BlitDepthCS, BlitDepthCSKernel, "_HiZMip0", depthTextureId);
        buffer.DispatchCompute(BlitDepthCS, BlitDepthCSKernel,
            Mathf.CeilToInt(RTWidth / (float)numThreads.x), Mathf.CeilToInt(RTHeight / (float)numThreads.y), 1);
        buffer.EndSample(bufferName + " Blit Depth");
        ExecuteBuffer();
        // Second Step: Dispatch compute shader to generate Hi-Z
        buffer.BeginSample(bufferName + " Hi Z");
        ExecuteBuffer();
        int mipWidth = RTWidth / 2, mipHeight = RTHeight / 2;
        for (int i = 1; i < mipCount; i++)
        {
            buffer.SetComputeTextureParam(HiZCS, HiZCSKernel, "_HiZSrc", depthTextureId, i - 1);
            buffer.SetComputeTextureParam(HiZCS, HiZCSKernel, "_HiZDst", depthTextureId, i);
            buffer.SetComputeIntParam(HiZCS, "prevMipLevel", i - 1);
            buffer.DispatchCompute(HiZCS, HiZCSKernel,
                Mathf.CeilToInt(mipWidth / (float)numThreads.x), Mathf.CeilToInt(mipHeight / (float)numThreads.y), 1);
            ExecuteBuffer();
            mipWidth /= 2;
            mipHeight /= 2;
        }
        buffer.EndSample(bufferName + " Hi Z");
        ExecuteBuffer();
        // TODO END
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(depthTextureId);
        ExecuteBuffer();
    }
}
