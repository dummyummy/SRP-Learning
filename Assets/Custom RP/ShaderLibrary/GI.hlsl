#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#if defined(LIGHTMAP_ON)
    #define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;
    #define GI_VARYINGS_DATA float2 lightMapUV : VAR_LIGHT_MAP_UV;
    #define TRANSFER_GI_DATA(input, output) \
        output.lightMapUV = input.lightMapUV * \
        unity_LightmapST.xy + unity_LightmapST.zw;
    #define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
    #define GI_ATTRIBUTE_DATA
    #define GI_VARYINGS_DATA
    #define TRANSFER_GI_DATA(input, output)
    #define GI_FRAGMENT_DATA(input) float2(0.0, 0.0)
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

#define LIGHTMAP_NAME unity_Lightmap
#define LIGHTMAP_SAMPLER_NAME samplerunity_Lightmap
#define SHADOWMASK_NAME unity_ShadowMask
#define SHADOWMASK_SAMPLER_NAME samplerunity_ShadowMask
#define LPPV_NAME unity_ProbeVolumeSH
#define LPPV_SAMPLER_NAME samplerunity_ProbeVolumeSH

TEXTURE2D(LIGHTMAP_NAME);
SAMPLER(LIGHTMAP_SAMPLER_NAME);
TEXTURE3D_FLOAT(LPPV_NAME);
SAMPLER(LPPV_SAMPLER_NAME);
TEXTURE2D(SHADOWMASK_NAME);
SAMPLER(SHADOWMASK_SAMPLER_NAME);
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

struct GI
{
    float3 diffuse;
    float3 specular;
    ShadowMask shadowMask;
};

float3 SampleEnvironment(Surface surfaceWS, BRDF brdf)
{
    float3 uvw = reflect(-surfaceWS.viewDirection, surfaceWS.normal);
    float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
    float4 environment = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, uvw, mip);
    return DecodeHDREnvironment(environment, unity_SpecCube0_HDR);
}

float4 SampleBakedShadows(float2 lightMapUV, Surface surfaceWS)
{
#if defined(LIGHTMAP_ON)
    return SAMPLE_TEXTURE2D(SHADOWMASK_NAME, SHADOWMASK_SAMPLER_NAME, lightMapUV);
#else
    if (unity_ProbeVolumeParams.x)
    {
        return SampleProbeOcclusion(TEXTURE3D_ARGS(LPPV_NAME, LPPV_SAMPLER_NAME),
            surfaceWS.position, unity_ProbeVolumeWorldToObject,
            unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z, 
            unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
        );
    }
    else
    {
        return unity_ProbesOcclusion;
    }
#endif
}

// Wrapper of SampleSingleLightMap in EntityLighting.hlsl
float3 SampleSingleLightmap(float2 lightMapUV)
{
#ifdef UNITY_LIGHTMAP_FULL_HDR
    bool encodedLightmap = false;
#else
    bool encodedLightmap = true;
#endif
    return SampleSingleLightmap(
        TEXTURE2D_ARGS(LIGHTMAP_NAME, LIGHTMAP_SAMPLER_NAME), lightMapUV,
        float4(1.0, 1.0, 0.0, 0.0), encodedLightmap,
        float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)
    );
}

float3 SampleLightMap(float2 lightMapUV)
{
#if defined(LIGHTMAP_ON)
    return SampleSingleLightmap(lightMapUV);
#else
    return 0.0;
#endif
}

float3 SampleLightProbe(Surface surfaceWS)
{
#if defined(LIGHTMAP_ON)
    return 0.0;
#else
    if (unity_ProbeVolumeParams.x)
    {
        return SampleProbeVolumeSH4(TEXTURE3D_ARGS(LPPV_NAME, LPPV_SAMPLER_NAME),
            surfaceWS.position, surfaceWS.normal, unity_ProbeVolumeWorldToObject,
            unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z, 
            unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
        );
    }
    else
    {
        float4 coefficients[7];
        coefficients[0] = unity_SHAr;
        coefficients[1] = unity_SHAg;
        coefficients[2] = unity_SHAb;
        coefficients[3] = unity_SHBr;
        coefficients[4] = unity_SHBg;
        coefficients[5] = unity_SHBb;
        coefficients[6] = unity_SHC;
        return max(0.0, SampleSH9(coefficients, surfaceWS.normal));
    }
#endif
}

GI GetGI(float2 lightMapUV, Surface surfaceWS, BRDF brdf)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
    gi.specular = SampleEnvironment(surfaceWS, brdf);
    gi.shadowMask.always = false;
    gi.shadowMask.distance = false;
    gi.shadowMask.shadows = 1.0;

    #if defined(_SHADOW_MASK_ALWAYS)
        gi.shadowMask.always = true;
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
    #elif defined(_SHADOW_MASK_DISTANCE)
        gi.shadowMask.distance = true;
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
    #endif

    return gi;
}

#endif