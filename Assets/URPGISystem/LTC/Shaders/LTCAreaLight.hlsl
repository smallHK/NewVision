/**
 * LTC区域光数据声明和访问函数
 * 
 * 功能说明：
 * 定义区域光相关的全局变量、数据结构和访问函数
 * 这些数据由C#端通过CommandBuffer设置
 * 
 * 数据流程：
 * 1. C#端收集场景中所有区域光数据
 * 2. 通过cmd.SetGlobalXXX设置到全局变量
 * 3. Shader端通过访问函数获取数据
 */

#ifndef LTC_AREA_LIGHT_INCLUDED
#define LTC_AREA_LIGHT_INCLUDED

// ==================== 常量定义 ====================
// 最大区域光数量限制
#define MAX_AREA_LIGHT_COUNT 16

// ==================== 全局变量声明 ====================
// 由C#端通过CommandBuffer设置

// 区域光数量
int      _AreaLightCount;

// 阴影渲染标志数组（0=不渲染阴影, 1=渲染阴影）
float    _AreaLightRenderShadow[MAX_AREA_LIGHT_COUNT];

// 纹理索引数组（用于纹理区域光）
float    _AreaLightTextureIndices[MAX_AREA_LIGHT_COUNT];

// 区域光颜色数组（RGB=颜色, A=强度）
float4   _AreaLightColors[MAX_AREA_LIGHT_COUNT];

// 区域光顶点位置数组（每列存储一个顶点的世界坐标）
float4x4 _AreaLightVertices[MAX_AREA_LIGHT_COUNT];

// 阴影参数数组（x=偏移, y=分辨率, z=1/分辨率, w=未使用）
float4   _AreaLightShadowParams[MAX_AREA_LIGHT_COUNT];

// 阴影近裁剪面距离数组
float    _AreaLightShadowNearClip[MAX_AREA_LIGHT_COUNT];

// 阴影远裁剪面距离数组
float    _AreaLightShadowFarClip[MAX_AREA_LIGHT_COUNT];

// 阴影投影矩阵数组
float4x4 _AreaLightShadowProjMatrix[MAX_AREA_LIGHT_COUNT];

// ==================== 阴影贴图纹理声明 ====================
// 区域光阴影贴图
TEXTURE2D(_AreaLightShadowMap);
SAMPLER(sampler_AreaLightShadowMap);

// 阴影贴图占位符（用于没有阴影的区域光）
TEXTURE2D(_AreaLightShadowMapDummy);
SAMPLER(sampler_AreaLightShadowMapDummy);

// ==================== 数据结构定义 ====================

/**
 * 区域光数据结构
 * 存储单个区域光的颜色和顶点信息
 */
struct AreaLight
{
    float4   color;    // 光源颜色（RGB）和强度（A）
    float4x4 vertices; // 光源四个顶点的世界坐标
};

// ==================== 访问函数 ====================

/**
 * 获取场景中区域光的数量
 * 
 * @return 区域光数量
 */
int GetAreaLightCount()
{
    return _AreaLightCount;
}

/**
 * 获取指定索引的区域光数据
 * 
 * @param index 区域光索引
 * @return AreaLight结构体，包含颜色和顶点信息
 */
AreaLight GetAreaLight(int index)
{
    AreaLight areaLight;
    areaLight.color        = _AreaLightColors[index];
    areaLight.vertices     = _AreaLightVertices[index];
    return areaLight;
}

/**
 * 获取指定区域光的顶点位置矩阵
 * 
 * 矩阵格式：
 * 每列存储一个顶点的齐次坐标
 * | v0.x v1.x v2.x v3.x |
 * | v0.y v1.y v2.y v3.y |
 * | v0.z v1.z v2.z v3.z |
 * | v0.w v1.w v2.w v3.w |
 * 
 * @param index 区域光索引
 * @return 4x4矩阵，每列为一个顶点坐标
 */
float4x4 GetAreaLightVertices(int index)
{
    return _AreaLightVertices[index];
}

/**
 * 获取指定区域光的颜色（RGB）
 * 
 * @param index 区域光索引
 * @return 光源颜色（RGB）
 */
float3 GetAreaLightColor(int index)
{
    return _AreaLightColors[index].rgb;
}

/**
 * 获取指定区域光的强度
 * 
 * @param index 区域光索引
 * @return 光源强度
 */
float GetAreaLightIntensity(int index)
{
    return _AreaLightColors[index].w;
}

/**
 * 获取指定区域光是否渲染阴影
 * 
 * @param index 区域光索引
 * @return 0=不渲染阴影, 1=渲染阴影
 */
float GetAreaLightRenderShadow(int index)
{
    return _AreaLightRenderShadow[index];
}

/**
 * 获取指定区域光的纹理索引
 * 
 * @param index 区域光索引
 * @return 纹理索引
 */
float GetAreaLightTextureIndex(int index)
{
    return _AreaLightTextureIndices[index];
}

/**
 * 获取指定区域光的阴影参数
 * 
 * 参数说明：
 * x = 阴影偏移（Shadow Bias）
 * y = 阴影贴图分辨率
 * z = 1 / 阴影贴图分辨率
 * w = 未使用
 * 
 * @param index 区域光索引
 * @return 阴影参数向量
 */
float4 GetAreaLightShadowParams(int index)
{
    return _AreaLightShadowParams[index];
}

/**
 * 获取指定区域光的阴影近裁剪面距离
 * 
 * @param index 区域光索引
 * @return 近裁剪面距离
 */
float GetAreaLightShadowNearClip(int index)
{
    return _AreaLightShadowNearClip[index];
}

/**
 * 获取指定区域光的阴影远裁剪面距离
 * 
 * @param index 区域光索引
 * @return 远裁剪面距离
 */
float GetAreaLightShadowFarClip(int index)
{
    return _AreaLightShadowFarClip[index];
}

/**
 * 获取指定区域光的阴影投影矩阵
 * 
 * @param index 区域光索引
 * @return 阴影投影矩阵
 */
float4x4 GetAreaLightShadowProjMatrix(int index)
{
    return _AreaLightShadowProjMatrix[index];
}

// ==================== 阴影贴图采样函数 ====================

/**
 * 采样区域光阴影贴图
 * 
 * @param uv 阴影贴图UV坐标
 * @return 阴影深度值
 */
float SampleAreaLightShadowMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_AreaLightShadowMap, sampler_AreaLightShadowMap, uv).r;
}

/**
 * 采样阴影贴图占位符
 * 用于没有阴影的区域光，返回1.0（无遮挡）
 * 
 * @param uv UV坐标
 * @return 1.0（表示无阴影遮挡）
 */
float SampleAreaLightShadowMapDummy(float2 uv)
{
    return SAMPLE_TEXTURE2D(_AreaLightShadowMapDummy, sampler_AreaLightShadowMapDummy, uv).r;
}

#endif
