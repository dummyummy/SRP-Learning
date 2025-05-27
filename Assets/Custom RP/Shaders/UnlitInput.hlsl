#ifndef CUSTOM_UNLIT_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_DetailMap);
SAMPLER(sampler_DetailMap);

// CBUFFER_START(UnityPerMaterial)
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    // float4 _BaseColor;
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
// CBUFFER_END

struct InputConfig
{
    float2 baseUV;
    float2 detailUV;
};

InputConfig GetInputConfig(float2 baseUV, float2 detailUV = 0.0)
{
    InputConfig config;
    config.baseUV = baseUV;
    config.detailUV = detailUV;
    return config;
}

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float2 TransformDetailUV(float2 detailUV)
{
    float4 detailST = INPUT_PROP(_DetailMap_ST);
    return detailUV * detailST.xy + detailST.zw;
}

float4 GetBase(InputConfig c)
{
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
    float4 color = INPUT_PROP(_BaseColor);
    return map * color;
}

float GetCutoff(InputConfig c)
{
    return INPUT_PROP(_Cutoff);
}

float GetMetallic(InputConfig c)
{
    return 0.0;
}

float GetSmoothness(InputConfig c)
{
    return 0.0;
}

float GetFresnel(InputConfig c)
{
    return 0.0;
}

float3 GetEmission(InputConfig c)
{
    return GetBase(c).rgb;
}

#endif // CUSTOM_LIT_INPUT_INCLUDED