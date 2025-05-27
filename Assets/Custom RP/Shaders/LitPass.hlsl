#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 baseUV : TEXCOORD0;
    float4 tangentOS : TANGENT;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float3 normalWS : VAR_NORMAL;
    float2 baseUV : VAR_BASE_UV;
    float2 detailUV : VAR_DETAIL_UV;
#if defined(_NORMAL_MAP)
    float4 tangentWS : VAR_TANGENT;
#endif
    GI_VARYINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.baseUV = TransformBaseUV(input.baseUV);
    output.detailUV = TransformDetailUV(input.baseUV);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS); // normalized
#if defined(_NORMAL_MAP)
    output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS), input.tangentOS.w);
#endif
    TRANSFER_GI_DATA(input, output)
    return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipLOD(input.positionCS.xy, unity_LODFade.x);
    
    InputConfig config = GetInputConfig(input.baseUV, input.detailUV);
    float4 base = GetBase(config);

#if defined(_CLIPPING)
    clip(base.a - GetCutoff(input.baseUV));
#endif

    Surface surface;
    surface.position = input.positionWS;
#if defined(_NORMAL_MAP)
    surface.normal = NormalTangentToWorld(GetNormalTS(config), input.normalWS, input.tangentWS, true);
    surface.interpolatedNormal = normalize(input.normalWS);
#else
    surface.normal = normalize(input.normalWS);
    surface.interpolatedNormal = surface.normal;
#endif
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(config);
    surface.occlusion = GetOcclusion(config);
    surface.smoothness = GetSmoothness(config);
    surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    surface.fresnelStrength = GetFresnel(config);
    // base.rgb = surface.normal * 0.5 + 0.5; // Debug normal
    // base.rgb = abs(length(input.normalWS) - 1.0) * 10.0; // Visualize normal length bias

    BRDF brdf = GetBRDF(surface);
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
    float3 color = GetLighting(surface, brdf, gi);
    color += GetEmission(config);
    return float4(color, surface.alpha);
}

#endif // CUSTOM_LIT_PASS_INCLUDED