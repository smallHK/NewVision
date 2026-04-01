/**
 * brdf.hlsl
 * 实现IBL所需的BRDF函数库
 * 基于Cook-Torrance微表面模型，包含法线分布函数、几何遮蔽函数、菲涅尔方程
 */

#ifndef UNITY_BRDF
#define UNITY_BRDF

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

/// <summary>
/// GGX/Trowbridge-Reitz法线分布函数(NDF)
/// 描述微表面法线与半程向量对齐的概率密度
/// </summary>
/// <param name="N">法线方向</param>
/// <param name="H">半程向量（光线与视线方向的中间向量）</param>
/// <param name="roughness">表面粗糙度</param>
/// <returns>法线分布值</returns>
float DistributionGGX(float3 N, float3 H, float roughness)
{
    const float a = roughness * roughness;
    const float a2 = a * a;
    const float NdotH = max(dot(N, H), 0.0);
    const float NdotH2 = NdotH * NdotH;

    const float nom = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
    return nom / denom;
}

// ============================================================================
// Hammersley采样序列生成
// 用于重要性采样的低差异序列
// ============================================================================

/// <summary>
/// Van der Corput序列的位反转实现
/// 高效计算径向逆函数
/// </summary>
/// <param name="bits">输入整数</param>
/// <returns>[0,1)范围的浮点数</returns>
float RadicalInverse_VdC(uint bits)
{
    bits = (bits << 16u) | (bits >> 16u);
    bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
    bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
    bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
    bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
    return float(bits) * 2.3283064365386963e-10;
}

/// <summary>
/// Hammersley序列生成
/// 生成均匀分布在单位正方形上的低差异点
/// </summary>
/// <param name="i">当前样本索引</param>
/// <param name="N">总样本数</param>
/// <returns>二维坐标，用于球面采样</returns>
float2 Hammersley(uint i, uint N)
{
    return float2(float(i) / float(N), RadicalInverse_VdC(i));
}

/// <summary>
/// GGX重要性采样
/// 根据GGX分布生成采样方向，使采样集中在重要区域
/// </summary>
/// <param name="Xi">Hammersley序列生成的随机数</param>
/// <param name="N">表面法线</param>
/// <param name="roughness">表面粗糙度</param>
/// <returns>采样方向（世界空间）</returns>
float3 ImportanceSampleGGX(float2 Xi, float3 N, float roughness)
{
    const float a = roughness * roughness;

    const float phi = 2.0 * PI * Xi.x;
    const float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a * a - 1.0) * Xi.y));
    const float sinTheta = sqrt(1.0 - cosTheta * cosTheta);

    // 从球面坐标转换到笛卡尔坐标 - 半程向量
    float3 H;
    H.x = cos(phi) * sinTheta;
    H.y = sin(phi) * sinTheta;
    H.z = cosTheta;

    // 从切线空间转换到世界空间
    const float3 up = abs(N.z) < 0.999 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
    const float3 tangent = normalize(cross(up, N));
    const float3 bitangent = cross(N, tangent);

    const float3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
    return normalize(sampleVec);
}

// ============================================================================
// 几何遮蔽函数
// 描述微表面自遮挡效应
// ============================================================================

/// <summary>
/// Schlick-GGX几何遮蔽函数（IBL版本）
/// 用于间接光照计算，使用不同的k值
/// </summary>
/// <param name="NdotV">法线与视线方向的点积</param>
/// <param name="roughness">表面粗糙度</param>
/// <returns>几何遮蔽系数</returns>
float GeometrySchlickGGX(float NdotV, float roughness)
{
    const float a = roughness;
    const float k = (a * a) / 2.0;

    const float nom = NdotV;
    const float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

/// <summary>
/// Schlick-GGX几何遮蔽函数（直接光照版本）
/// 用于直接光照计算，使用不同的k值
/// </summary>
float GeometrySchlickGGX2(float NdotV, float roughness)
{
    const float r = (roughness + 1.0);
    const float k = (r * r) / 8.0;

    const float nom = NdotV;
    const float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

/// <summary>
/// Smith几何遮蔽函数（IBL版本）
/// 分别计算入射和出射方向的遮蔽，然后相乘
/// </summary>
/// <param name="N">法线方向</param>
/// <param name="V">视线方向</param>
/// <param name="L">光线方向</param>
/// <param name="roughness">表面粗糙度</param>
/// <returns>几何遮蔽系数</returns>
float GeometrySmith(float3 N, float3 V, float3 L, float roughness)
{
    const float NdotV = max(dot(N, V), 0.0);
    const float NdotL = max(dot(N, L), 0.0);
    const float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    const float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

/// <summary>
/// Smith几何遮蔽函数（直接光照版本）
/// </summary>
float GeometrySmith2(float3 N, float3 V, float3 L, float roughness)
{
    const float NdotV = max(dot(N, V), 0.0);
    const float NdotL = max(dot(N, L), 0.0);
    const float ggx2 = GeometrySchlickGGX2(NdotV, roughness);
    const float ggx1 = GeometrySchlickGGX2(NdotL, roughness);

    return ggx1 * ggx2;
}

// ============================================================================
// 菲涅尔方程
// 描述不同入射角下的反射率变化
// ============================================================================

/// <summary>
/// Schlick近似菲涅尔方程
/// 计算表面反射率随观察角度的变化
/// </summary>
/// <param name="cosTheta">视线与法线的余弦值</param>
/// <param name="F0">基础反射率（0度角时的反射率）</param>
/// <returns>菲涅尔反射率</returns>
float3 fresnelSchlick(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

/// <summary>
/// 带粗糙度修正的菲涅尔方程
/// 用于IBL计算，考虑粗糙度对边缘反射的影响
/// </summary>
/// <param name="cosTheta">视线与法线的余弦值</param>
/// <param name="F0">基础反射率</param>
/// <param name="roughness">表面粗糙度</param>
/// <returns>修正后的菲涅尔反射率</returns>
float3 fresnelSchlickRoughness(float cosTheta, float3 F0, float roughness)
{
    const float gloss = 1 - roughness;
    return F0 + (max(float3(gloss, gloss, gloss), F0) - F0) * pow(1.0 - cosTheta, 5.0);
}

// ============================================================================
// BRDF积分
// 用于预计算BRDF LUT
// ============================================================================

/// <summary>
/// BRDF积分函数
/// 通过蒙特卡洛积分预计算BRDF的缩放和偏移值
/// 用于生成BRDF LUT纹理
/// </summary>
/// <param name="NdotV">法线与视线的余弦值</param>
/// <param name="roughness">表面粗糙度</param>
/// <returns>float2.x = 缩放因子, float2.y = 偏移因子</returns>
float2 IntegrateBRDF(float NdotV, float roughness)
{
    float3 V;
    V.x = sqrt(1.0 - NdotV * NdotV);
    V.y = 0.0;
    V.z = NdotV;

    float A = 0.0;
    float B = 0.0;

    float3 N = float3(0.0, 0.0, 1.0);

    const uint SAMPLE_COUNT = 512U;
    for (uint i = 0u; i < SAMPLE_COUNT; ++i)
    {
        // 使用重要性采样生成偏向首选对齐方向的采样向量
        const float2 Xi = Hammersley(i, SAMPLE_COUNT);
        const float3 H = ImportanceSampleGGX(Xi, N, roughness);
        const float3 L = normalize(2.0 * dot(V, H) * H - V);

        const float NdotL = max(L.z, 0.0);
        const float NdotH = max(H.z, 0.0);
        const float VdotH = max(dot(V, H), 0.0);

        if (NdotL > 0.0)
        {
            const float G = GeometrySmith(N, V, L, roughness);
            const float G_Vis = (G * VdotH) / (NdotH * NdotV);
            const float Fc = pow(1.0 - VdotH, 5.0);

            A += (1.0 - Fc) * G_Vis;
            B += Fc * G_Vis;
        }
    }
    A /= float(SAMPLE_COUNT);
    B /= float(SAMPLE_COUNT);
    return float2(A, B);
}

#endif //UNITY_BRDF
