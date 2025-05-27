#ifndef CUSTOM_META_PASS_INCLUDED
#define CUSTOM_META_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    float2 lightMapUV : TEXCOORD1;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
    float2 detailUV : VAR_DETAIL_UV;
};

// x = return albedo
// y = return normal
bool4 unity_MetaFragmentControl;
float unity_OneOverOutputBoost;
float unity_MaxOutputValue;

Varyings MetaPassVertex(Attributes input)
{
    Varyings output;
    input.positionOS.xy = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    input.positionOS.z = input.positionOS.z > 0.0 ? FLT_MIN : 0.0;
    output.positionCS = TransformWorldToHClip(input.positionOS);
    output.baseUV = TransformBaseUV(input.baseUV);
    output.detailUV = TransformDetailUV(input.baseUV);
    return output;
}

float4 MetaPassFragment(Varyings input) : SV_TARGET
{
    InputConfig config = GetInputConfig(input.baseUV, input.detailUV);
    float4 base = GetBase(config);

    Surface surface;
    ZERO_INITIALIZE(Surface, surface);
    surface.color = base.rgb;
    surface.metallic = GetMetallic(config);
    surface.smoothness = GetSmoothness(config);

    BRDF brdf = GetBRDF(surface);
    float4 indirectDiffuse = 0.0, emission = 0.0;
    if (unity_MetaFragmentControl.x)
    {
        indirectDiffuse += float4(brdf.diffuse, 1.0);
        indirectDiffuse.rgb += brdf.specular * brdf.roughness * 0.5;
        indirectDiffuse.rgb = min(PositivePow(indirectDiffuse.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue);
    }
    if (unity_MetaFragmentControl.y)
    {
        emission = float4(GetEmission(config), 1.0);
    }
    return indirectDiffuse + emission;
}

#endif // CUSTOM_META_PASS_INCLUDED