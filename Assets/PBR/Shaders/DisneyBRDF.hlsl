#ifndef DISNEY_BRDF_INCLUDED
#define DISNEY_BRDF_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#define PI 3.14159265359
#define INV_PI 0.31830988618

float3 SchlickFresnel(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(saturate(1.0 - cosTheta), 5.0);
}

float SchlickFresnel(float cosTheta, float F0)
{
    return F0 + (1.0 - F0) * pow(saturate(1.0 - cosTheta), 5.0);
}

float GTR1(float NdotH, float a)
{
    if (a >= 1.0) return INV_PI;
    float a2 = a * a;
    float t = 1.0 + (a2 - 1.0) * NdotH * NdotH;
    return (a2 - 1.0) / (PI * log(a2) * t);
}

float GTR2(float NdotH, float a)
{
    float a2 = a * a;
    float t = 1.0 + (a2 - 1.0) * NdotH * NdotH;
    return a2 / (PI * t * t);
}

float GTR2Aniso(float NdotH, float HdotX, float HdotY, float ax, float ay)
{
    float ax2 = ax * ax;
    float ay2 = ay * ay;
    float denom = PI * ax * ay * (HdotX * HdotX / ax2 + HdotY * HdotY / ay2 + NdotH * NdotH);
    return 1.0 / denom;
}

float SmithGGX(float NdotV, float alphaG)
{
    float a = alphaG * alphaG;
    float b = NdotV * NdotV;
    return 1.0 / (NdotV + sqrt(a + b - a * b));
}

float SmithGGXAniso(float NdotV, float VdotX, float VdotY, float ax, float ay)
{
    float denom = VdotX * ax * VdotX * ax + VdotY * ay * VdotY * ay + NdotV * NdotV;
    return 1.0 / (NdotV + sqrt(denom));
}

float3 DisneyDiffuse(float NdotL, float NdotV, float LdotH, float roughness, float3 baseColor)
{
    float FL = SchlickFresnel(NdotL, 0.5);
    float FV = SchlickFresnel(NdotV, 0.5);
    float RR = 2.0 * roughness * LdotH * LdotH;
    float3 diffuse = baseColor * INV_PI * (1.0 - FL) * (1.0 - FV) * 
                     (1.0 / (1.0 + RR) * (1.0 + RR * 10.0));
    return diffuse;
}

float3 DisneySpecular(float NdotL, float NdotV, float NdotH, float LdotH, 
                      float3 baseColor, float metallic, float roughness, 
                      float specular, float specularTint)
{
    float3 Cdlin = baseColor;
    float Cdlum = 0.3 * Cdlin.r + 0.6 * Cdlin.g + 0.1 * Cdlin.b;
    float3 Ctint = Cdlum > 0.0 ? Cdlin / Cdlum : float3(1, 1, 1);
    float3 Cspec0 = lerp(specular * 0.08 * lerp(float3(1, 1, 1), Ctint, specularTint), 
                         Cdlin, metallic);
    
    float3 F = SchlickFresnel(LdotH, Cspec0);
    float D = GTR2(NdotH, max(0.001, roughness));
    float G = SmithGGX(NdotL, max(0.001, roughness)) * 
              SmithGGX(NdotV, max(0.001, roughness));
    
    return F * D * G;
}

float3 DisneyClearcoat(float NdotL, float NdotV, float NdotH, float LdotH,
                       float clearcoat, float clearcoatGloss)
{
    float F = SchlickFresnel(LdotH, 0.04);
    float D = GTR1(NdotH, lerp(0.1, 0.001, clearcoatGloss));
    float G = SmithGGX(NdotL, 0.25) * SmithGGX(NdotV, 0.25);
    
    return 0.25 * clearcoat * F * D * G * float3(1, 1, 1);
}

float3 DisneySheen(float NdotL, float3 baseColor, float sheen, float sheenTint, float metallic)
{
    float Cdlum = 0.3 * baseColor.r + 0.6 * baseColor.g + 0.1 * baseColor.b;
    float3 Ctint = Cdlum > 0.0 ? baseColor / Cdlum : float3(1, 1, 1);
    float3 Csheen = lerp(float3(1, 1, 1), Ctint, sheenTint);
    float FL = SchlickFresnel(NdotL, 0.5);
    
    return sheen * Csheen * FL * (1.0 - metallic);
}

float3 DisneyBRDF(float3 N, float3 V, float3 L, float3 X, float3 Y,
                  float3 baseColor, float metallic, float roughness,
                  float specular, float specularTint, float anisotropic,
                  float sheen, float sheenTint, float clearcoat, float clearcoatGloss)
{
    float3 H = normalize(L + V);
    
    float NdotL = max(0.001, dot(N, L));
    float NdotV = max(0.001, dot(N, V));
    float NdotH = max(0.001, dot(N, H));
    float LdotH = max(0.001, dot(L, H));
    float VdotH = max(0.001, dot(V, H));
    
    float HdotX = dot(H, X);
    float HdotY = dot(H, Y);
    float VdotX = dot(V, X);
    float VdotY = dot(V, Y);
    float LdotX = dot(L, X);
    float LdotY = dot(L, Y);
    
    float aspect = sqrt(1.0 - anisotropic * 0.9);
    float ax = max(0.001, roughness * roughness / aspect);
    float ay = max(0.001, roughness * roughness * aspect);
    
    float3 diffuse = DisneyDiffuse(NdotL, NdotV, LdotH, roughness, baseColor);
    
    float3 specularBRDF = DisneySpecular(NdotL, NdotV, NdotH, LdotH,
                                          baseColor, metallic, roughness,
                                          specular, specularTint);
    
    float3 clearcoatBRDF = DisneyClearcoat(NdotL, NdotV, NdotH, LdotH,
                                            clearcoat, clearcoatGloss);
    
    float3 sheenBRDF = DisneySheen(NdotL, baseColor, sheen, sheenTint, metallic);
    
    float3 f = (1.0 - metallic) * diffuse + specularBRDF + clearcoatBRDF + sheenBRDF;
    
    return f * NdotL;
}

float3 EvaluateDisneyDirectLight(float3 N, float3 V, float3 L,
                                 float3 baseColor, float metallic, float roughness,
                                 float specular, float specularTint,
                                 float sheen, float sheenTint,
                                 float clearcoat, float clearcoatGloss,
                                 float3 lightColor, float3 lightDir)
{
    float3 X = float3(1, 0, 0);
    float3 Y = float3(0, 1, 0);
    
    float3 tangent = normalize(cross(N, Y));
    if (length(tangent) < 0.01)
    {
        tangent = normalize(cross(N, X));
    }
    float3 bitangent = normalize(cross(N, tangent));
    
    return DisneyBRDF(N, V, L, tangent, bitangent,
                      baseColor, metallic, roughness,
                      specular, specularTint, 0.0,
                      sheen, sheenTint, clearcoat, clearcoatGloss) * lightColor;
}

float3 DisneyIBL(float3 N, float3 V, float3 baseColor, float metallic, float roughness,
                 float specular, float specularTint, float clearcoat, float clearcoatGloss,
                 TEXTURECUBE(envMap), SAMPLER(sampler_envMap))
{
    float3 R = reflect(-V, N);
    float NdotV = max(0.001, dot(N, V));
    
    float3 Cdlin = baseColor;
    float Cdlum = 0.3 * Cdlin.r + 0.6 * Cdlin.g + 0.1 * Cdlin.b;
    float3 Ctint = Cdlum > 0.0 ? Cdlin / Cdlum : float3(1, 1, 1);
    float3 Cspec0 = lerp(specular * 0.08 * lerp(float3(1, 1, 1), Ctint, specularTint), 
                         Cdlin, metallic);
    
    float3 F = SchlickFresnel(NdotV, Cspec0);
    
    float3 kd = (1.0 - F) * (1.0 - metallic);
    
    float3 diffuseIBL = baseColor * SAMPLE_TEXTURECUBE(envMap, sampler_envMap, N).rgb;
    
    float3 specularIBL = SAMPLE_TEXTURECUBE(envMap, sampler_envMap, R).rgb;
    
    float3 clearcoatR = reflect(-V, N);
    float3 clearcoatIBL = SAMPLE_TEXTURECUBE(envMap, sampler_envMap, clearcoatR).rgb;
    float Fc = SchlickFresnel(NdotV, 0.04);
    
    return kd * diffuseIBL + F * specularIBL + 0.25 * clearcoat * Fc * clearcoatIBL;
}

#endif
