#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

struct BRDF
{
    float3 diffuse; // diffuse color
    float3 specular; // specular color
    float roughness;
};

#define MIN_REFLECTIVITY 0.04

float OneMinusReflectivity(float metallic)
{
    float range = 1.0 - MIN_REFLECTIVITY;
    return range - metallic * range;
}

BRDF GetBRDF(Surface surface)
{
    BRDF brdf;
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    brdf.diffuse = surface.color * oneMinusReflectivity;
#if defined(_PREMULTIPLY_ALPHA)
    brdf.diffuse *= surface.alpha;
#endif
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
    brdf.roughness = PerceptualSmoothnessToRoughness(surface.smoothness);
    return brdf;
}

float SpecularStrength(Surface surface, BRDF brdf, Light light)
{
    float3 h = SafeNormalize(surface.viewDirection + light.direction);
    float NoH = saturate(dot(surface.normal, h));
    float LoH = saturate(dot(light.direction, h));
    float r2 = brdf.roughness * brdf.roughness;
    float d = NoH * NoH * (r2 - 1.0) + 1.00001f;
    float LoH2 = LoH * LoH;
    float normalizationTerm = brdf.roughness * 4.0 + 2.0;
    float specularTerm = r2 / ((d * d) * max(0.1, LoH2) * normalizationTerm);
    return specularTerm;
}

float3 DirectBRDF(Surface surface, BRDF brdf, Light light)
{
    return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

#endif