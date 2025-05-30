#ifndef CUSTOM_COMPUTE_VIS_PASS_INCLUDED
#define CUSTOM_COMPUTE_VIS_PASS_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Macros.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

TEXTURE2D(_ComputeResult); // from compute shader
SAMPLER(sampler_ComputeResult);
TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

// UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
//     UNITY_DEFINE_INSTANCED_PROP(float4, _ComputeResult_ST)
// UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : VAR_NORMAL;
    float2 baseUV : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ComputeVisualizeVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionCS = TransformObjectToHClip(input.positionOS);
    // float4 baseST = INPUT_PROP(_ComputeResult_ST);
    // output.baseUV = input.baseUV * baseST.xy + baseST.zw;
    output.baseUV = input.baseUV;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS); // normalized
    return output;
}

float4 ComputeVisualizeFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 color = float3(0.0, 0.0, 0.0);
#if defined(_VIS_DEPTH)
    float rawDepth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, input.baseUV, 0).r;
    float linearDepth = Linear01Depth(rawDepth, _ZBufferParams);
    color = float3(linearDepth, linearDepth, linearDepth); // visualize depth as grayscale
#else
    color = SAMPLE_TEXTURE2D(_ComputeResult, sampler_ComputeResult, input.baseUV).xyz;
#endif
    return float4(color, 1.0);
}

#endif // CUSTOM_COMPUTE_VIS_PASS_INCLUDED
