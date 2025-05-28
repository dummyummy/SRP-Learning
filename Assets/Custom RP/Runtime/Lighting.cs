using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    const string bufferName = "Lighting";

    const int maxDirLightCount = 4, maxOtherLightCount = 64;

    // directional light properties
    static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
    static int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
    static int dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
    static int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
    static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static Vector4[] dirLightDirections = new Vector4[maxDirLightCount];
    static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount]; // [shadow strength, first atlas tile index, normalBias weight, lightmap channel]

    // other light properties
    static int otherLightCountId = Shader.PropertyToID("_OtherLightCount");
    static int otherLightColorsId = Shader.PropertyToID("_OtherLightColors");
    static int otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");
    static int otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections");
    static int otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
    static int otherLightsShadowDataId = Shader.PropertyToID("_OtherLightShadowData");
    static Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightDirections = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount]; // [1 / (cos i - cos o), -cos o / (cos i - cos o), not used, not used]
    static Vector4[] otherLightShadowData = new Vector4[maxOtherLightCount];


    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    CullingResults cullingResults;

    Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        shadows.Setup(context, cullingResults, shadowSettings);
        SetupLights();
        shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    private void SetupLights()
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        int dirLightCount = 0, otherLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight light = visibleLights[i];
            switch (light.lightType)
            {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount)
                    {
                        SetupDirectionalLight(dirLightCount++, ref light);
                    }
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        SetupPointLight(otherLightCount++, ref light);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        SetupSpotLight(otherLightCount++, ref light);
                    }
                    break;
            }
        }

        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        if (dirLightCount > 0)
        {
            buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
            buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
        }

        buffer.SetGlobalInt(otherLightCountId, otherLightCount);
        if (otherLightCount > 0)
        {
            buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
            buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
        }
    }

    private void SetupDirectionalLight (int index, ref VisibleLight light)
    {
        dirLightColors[index] = light.finalColor;
        dirLightDirections[index] = -light.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(light.light, index);
    }

    private void SetupPointLight(int index, ref VisibleLight light)
    {
        otherLightColors[index] = light.finalColor;
        Vector4 position = light.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(light.range * light.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightSpotAngles[index] = new Vector4(0f, 1f, 0f, 0f); // Point lights do not have spot angles
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light.light, index);
    }

    private void SetupSpotLight(int index, ref VisibleLight light)
    {
        otherLightColors[index] = light.finalColor;
        Vector4 position = light.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(light.range * light.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightDirections[index] = -light.localToWorldMatrix.GetColumn(2);
        float innerCos = Mathf.Cos(light.light.innerSpotAngle * Mathf.Deg2Rad * 0.5f);
        float outerCos = Mathf.Cos(light.spotAngle * Mathf.Deg2Rad * 0.5f);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpotAngles[index] = new Vector4(
            angleRangeInv, -outerCos * angleRangeInv, 0f, 0f
        );
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light.light, index);
    }

    public void Cleanup()
    {
        shadows.Cleanup();
    }
}
