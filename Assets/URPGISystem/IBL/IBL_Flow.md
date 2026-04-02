# IBL 模块代码调用流程文档

## 模块概述

IBL (Image-Based Lighting) 模块实现了基于图像的光照系统，包含两个主要功能：

1. **IBL 贴图生成** - 编辑器扩展，从 HDR 环境贴图生成 IBL 所需的预计算贴图
2. **IBL 运行时渲染** - URP Render Feature，在运行时将 IBL 效果合成到场景

---

## 文件结构

```
Assets/URPGISystem/IBL/
├── IBLComposite.cs              # URP Render Feature 定义
├── Editor/
│   ├── IBLGenerator.cs          # IBL 贴图生成核心逻辑
│   └── IBLContextMenu.cs        # 右键菜单扩展
└── Shaders/
    ├── IBLComposite.shader      # IBL 合成着色器
    ├── brdf.hlsl                # BRDF 函数库
    ├── IrradianceConvolution.shader    # Irradiance Map 生成着色器
    ├── PrefilterConvolution.shader     # Prefilter Map 生成着色器
    ├── BRDFIntegration.shader          # BRDF LUT 生成着色器
    └── EquirectangularToCubemap.shader # HDR 转 Cubemap 着色器
```

---

## 调用流程图

### 1. IBL 贴图生成流程（编辑器）

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          IBL 贴图生成流程                                    │
└─────────────────────────────────────────────────────────────────────────────┘

用户操作：选中 HDR 文件 → 右键 → IBL → Generate All IBL Maps
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ IBLContextMenu.cs                                                           │
│                                                                             │
│  [MenuItem] ValidateHDRTexture() ──► 验证是否为 HDR 文件                    │
│              │                                                              │
│              ▼                                                              │
│  [MenuItem] GenerateAllIBLMaps()                                           │
│              │                                                              │
│              ├── 获取 HDR 纹理                                              │
│              ├── 构建输出目录                                                │
│              │                                                              │
│              ▼                                                              │
│  IBLGenerator.GenerateAllFromHDR(hdrTexture, directory)                    │
└─────────────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ IBLGenerator.cs                                                             │
│                                                                             │
│  GenerateAllFromHDR()                                                       │
│      │                                                                      │
│      ├──► Step 1: ConvertHDRToCubemap()                                    │
│      │        │                                                             │
│      │        ├── 创建 Cubemap (512x512)                                   │
│      │        ├── 使用 EquirectangularToCubemap.shader                     │
│      │        └── 渲染 6 个面                                               │
│      │                                                                      │
│      ├──► Step 2: GenerateIrradianceMap()                                  │
│      │        │                                                             │
│      │        ├── 创建 Cubemap (32x32)                                     │
│      │        ├── 使用 IrradianceConvolution.shader                        │
│      │        ├── RenderCubemapFaceWithCube() 渲染 6 个面                  │
│      │        └── 保存为 .asset 文件                                        │
│      │                                                                      │
│      ├──► Step 3: GeneratePrefilterMap()                                   │
│      │        │                                                             │
│      │        ├── 创建 Cubemap (128x128, 5级 mipmap)                       │
│      │        ├── 遍历 5 级 mipmap                                          │
│      │        │   ├── 计算粗糙度 (0.0 ~ 1.0)                               │
│      │        │   ├── 使用 PrefilterConvolution.shader                     │
│      │        │   └── RenderCubemapFaceWithCube() 渲染 6 个面              │
│      │        └── 保存为 .asset 文件                                        │
│      │                                                                      │
│      └──► Step 4: GenerateBRDFLut()                                        │
│               │                                                             │
│               ├── 创建 Texture2D (512x512)                                 │
│               ├── 使用 BRDFIntegration.shader                              │
│               └── 保存为 .asset 文件                                        │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2. IBL 运行时渲染流程

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          IBL 运行时渲染流程                                  │
└─────────────────────────────────────────────────────────────────────────────┘

URP 渲染管线初始化
        │
        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ IBLComposite.cs                                                             │
│                                                                             │
│  Create() ──────────────────────────────────────────────────────────────►  │
│      │                                                                      │
│      ├── 创建 CustomRenderPass                                             │
│      └── 设置 renderPassEvent = BeforeRenderingPostProcessing              │
│                                                                             │
│  AddRenderPasses() ◄──────────────── 每帧每相机调用一次                     │
│      │                                                                      │
│      ├── 检测渲染路径 (Deferred/Forward)                                   │
│      ├── Setup(deferred)                                                   │
│      └── renderer.EnqueuePass(m_ScriptablePass)                            │
└─────────────────────────────────────────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ CustomRenderPass (ScriptableRenderPass)                                     │
│                                                                             │
│  OnCameraSetup()                                                           │
│      │                                                                      │
│      ├── 创建临时 RenderTexture                                            │
│      ├── 获取相机颜色目标                                                   │
│      └── 配置 GBuffer 输入                                                  │
│                                                                             │
│  Execute()                                                                 │
│      │                                                                      │
│      ├── cmd.Blit(src, temp, material)  ──► 应用 IBL 材质                  │
│      └── cmd.Blit(temp, src)            ──► 写回颜色缓冲                   │
│                                                                             │
│  OnCameraCleanup()                                                         │
│      │                                                                      │
│      └── 释放临时 RenderTexture                                            │
└─────────────────────────────────────────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ IBLComposite.shader                                                         │
│                                                                             │
│  frag() 片段着色器                                                          │
│      │                                                                      │
│      ├── Step 1: 从 GBuffer 获取几何/材质信息                              │
│      │        ├── GetFragmentWorldPos()     ──► 世界坐标                   │
│      │        ├── GBuffer0                  ──► Albedo                     │
│      │        ├── GBuffer1                  ──► Metallic/Smoothness        │
│      │        └── GBuffer2 + UnpackNormalFromGBuffer() ──► Normal          │
│      │                                                                      │
│      ├── Step 2: 计算视线和反射方向                                        │
│      │        ├── viewDir = normalize(cameraPos - worldPos)                │
│      │        └── reflectDir = reflect(-viewDir, normal)                   │
│      │                                                                      │
│      ├── Step 3: 计算菲涅尔基础反射率 F0                                   │
│      │        └── F0 = lerp(0.04, albedo, metallic)                        │
│      │                                                                      │
│      ├── Step 4: 计算菲涅尔项                                              │
│      │        └── F = fresnelSchlickRoughness(NdotV, F0, roughness)        │
│      │                                                                      │
│      ├── Step 5: 计算漫反射系数                                            │
│      │        ├── kS = F                                                   │
│      │        ├── kD = (1 - kS) * (1 - metallic)                           │
│      │                                                                      │
│      ├── Step 6: 漫反射 IBL                                                │
│      │        ├── irradiance = SAMPLE_TEXTURECUBE(IrradianceMap, normal)   │
│      │        └── diffuse = irradiance * albedo                            │
│      │                                                                      │
│      ├── Step 7: 镜面反射 IBL                                              │
│      │        ├── prefilteredColor = SAMPLE_TEXTURECUBE_LOD(               │
│      │        │                           PrefilterMap, reflectDir, lod)    │
│      │        ├── brdf = SAMPLE_TEXTURE2D(BRDFLut, NdotV, roughness)       │
│      │        └── specular = prefilteredColor * (F * brdf.x + brdf.y)      │
│      │                                                                      │
│      └── Step 8: 合并结果                                                  │
│               └── color.rgb += kD * diffuse + specular                     │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 核心函数说明

### IBLGenerator.cs

| 函数 | 功能 | 输入 | 输出 |
|------|------|------|------|
| `GenerateAllFromHDR` | 从 HDR 生成所有 IBL 贴图 | HDR 纹理, 输出目录 | 无 |
| `ConvertHDRToCubemap` | 等距圆柱投影转 Cubemap | HDR 纹理 | Cubemap |
| `GenerateIrradianceMap` | 生成漫反射辐照度贴图 | Cubemap, 路径 | Cubemap (32x32) |
| `GeneratePrefilterMap` | 生成镜面反射预过滤贴图 | Cubemap, 路径 | Cubemap (128x128, 5 mip) |
| `GenerateBRDFLut` | 生成 BRDF 查找表 | 路径 | Texture2D (512x512) |
| `RenderCubemapFaceWithCube` | 渲染 Cubemap 单个面 | 材质, RT, 面索引 | 无 |

### IBLContextMenu.cs

| 函数 | 功能 | 触发方式 |
|------|------|----------|
| `ValidateHDRTexture` | 验证是否为 HDR 文件 | 菜单验证 |
| `GenerateIrradianceMap` | 生成 Irradiance Map | 右键菜单 |
| `GeneratePrefilterMap` | 生成 Prefilter Map | 右键菜单 |
| `GenerateAllIBLMaps` | 生成所有 IBL 贴图 | 右键菜单 |
| `CreateTempCubemapFromHDR` | 创建临时 Cubemap | 内部调用 |

### IBLComposite.cs

| 函数 | 功能 | 调用时机 |
|------|------|----------|
| `Create` | 初始化 Render Feature | Render Feature 创建时 |
| `AddRenderPasses` | 添加渲染 Pass | 每帧每相机 |
| `OnCameraSetup` | 创建临时 RT | Pass 执行前 |
| `Execute` | 执行 Blit 操作 | Pass 执行时 |
| `OnCameraCleanup` | 释放临时 RT | Pass 执行后 |

### IBLComposite.shader

| 函数 | 功能 |
|------|------|
| `vert` | 顶点着色器，变换坐标 |
| `GetFragmentWorldPos` | 从深度重建世界坐标 |
| `UnpackNormalFromGBuffer` | 解包 GBuffer 法线 |
| `SampleBRDFLut` | 采样 BRDF LUT |
| `frag` | 片段着色器，IBL 计算核心 |

---

## 着色器依赖关系

```
IBLComposite.shader
    │
    ├── Core.hlsl (URP 核心)
    ├── DeclareDepthTexture.hlsl (深度纹理)
    └── brdf.hlsl (自定义 BRDF 函数)
            │
            ├── DistributionGGX (GGX 法线分布)
            ├── GeometrySmith (几何遮蔽)
            ├── fresnelSchlick (菲涅尔)
            ├── fresnelSchlickRoughness (带粗糙度的菲涅尔)
            ├── Hammersley (低差异序列)
            ├── ImportanceSampleGGX (重要性采样)
            └── IntegrateBRDF (BRDF 积分)

IrradianceConvolution.shader
    │
    ├── Core.hlsl
    └── brdf.hlsl (Hammersley, SampleHemisphere)

PrefilterConvolution.shader
    │
    ├── Core.hlsl
    └── brdf.hlsl (Hammersley, ImportanceSampleGGX, DistributionGGX)

BRDFIntegration.shader
    │
    ├── Core.hlsl
    └── brdf.hlsl (IntegrateBRDF)
```

---

## 使用流程

### 1. 生成 IBL 贴图

1. 在 Project 窗口选中 HDR 文件（.hdr/.exr/.hdri）
2. 右键 → IBL → Generate All IBL Maps
3. 自动在 HDR 所在目录生成：
   - `{name}_Cubemap.asset`
   - `{name}_Irradiance.asset`
   - `{name}_Prefilter.asset`
   - `BRDF_LUT.asset`

### 2. 配置 IBL 渲染

1. 创建材质，使用 `NewVision/IBL/Composite` shader
2. 设置材质属性：
   - Irradiance Map → `{name}_Irradiance`
   - Prefilter Map → `{name}_Prefilter`
   - BRDF Lut → `BRDF_LUT`
3. 在 URP Renderer Data 中添加 IBLComposite Render Feature
4. 将材质赋给 Render Feature 的 Composite Material

---

## IBL 计算公式

### 漫反射 IBL

```
diffuse = irradiance(normal) × albedo
kD = (1 - F) × (1 - metallic)
result_diffuse = kD × diffuse
```

### 镜面反射 IBL

```
prefilteredColor = prefilterMap(reflectDir, roughness × maxLOD)
brdf = brdfLUT(NdotV, roughness)
result_specular = prefilteredColor × (F × brdf.x + brdf.y)
```

### 最终 IBL

```
IBL = result_diffuse + result_specular
```
