# SSR (Screen Space Reflections) 模块启用指南

## 概述
SSR (Screen Space Reflections) 模块是一个基于屏幕空间的反射效果实现，为场景中的物体添加真实的反射效果，提升视觉质量。

## 启用步骤

### 1. 导入模块
确保以下文件已存在于 `Assets/URPGISystem/SSR` 目录中：

- `MySSR.cs` - SSR 渲染功能的主要实现
- `DepthPyramid.cs` - 深度金字塔生成功能
- `Shaders/` 目录包含以下文件：
  - `HiZ_Shader.compute` - 深度金字塔计算着色器
  - `SSR_Shader.shader` - SSR 主着色器
  - `Common.hlsl` - 通用工具函数
  - `NormalSample.hlsl` - 法线采样工具

### 2. 在 URP 渲染管线中添加 SSR 功能

#### 步骤 1: 打开渲染管线设置
1. 在 Unity 编辑器中，导航到 `Project` 窗口
2. 找到您的 URP 渲染管线资源（通常命名为 `URP Asset` 或类似名称）
3. 双击打开它

#### 步骤 2: 添加 SSR Render Feature
1. 在渲染管线设置窗口中，找到 "Renderer List" 部分
2. 选择您正在使用的渲染器（通常是 "Forward Renderer" 或 "Universal Renderer"）
3. 点击 "Add Renderer Feature" 按钮
4. 从下拉菜单中选择 "MySSR"

#### 步骤 3: 配置 SSR 参数
1. 选择刚刚添加的 "MySSR" 渲染功能
2. 在 Inspector 窗口中，您可以调整以下参数：
   - `stepStrideLength` - 光线步进的步长，较大的值会提高性能但降低精度
   - `maxSteps` - 光线步进的最大步数，较大的值会提高效果但降低性能
   - `downSample` - 下采样级别，较大的值会提高性能但降低效果质量
   - `minSmoothness` - 最小平滑度，只有平滑度高于此值的物体才会产生反射
   - `reflectSky` - 是否反射天空
   - `ditherType` - 抖动类型，用于减少 artifacts
   - `tracingMode` - 追踪模式，选择 "HiZTracing" 以获得更好的性能

### 3. 配置场景设置

#### 步骤 1: 确保相机启用深度纹理
1. 选择场景中的主相机
2. 在 Inspector 窗口中，确保 "Depth Texture" 选项已启用

#### 步骤 2: 确保材质支持反射
1. 选择需要产生反射的材质
2. 确保材质使用的着色器支持平滑度（smoothness）参数
3. 调整材质的平滑度值，值越高，反射效果越明显

### 4. 测试 SSR 效果

1. 运行场景
2. 观察具有高平滑度的物体是否产生了反射效果
3. 调整 SSR 参数以获得最佳视觉效果和性能平衡

## 性能优化建议

- **使用 HiZ Tracing** - 这是默认的追踪模式，比线性追踪更快
- **适当调整 downSample** - 对于较低端设备，增加下采样级别可以显著提高性能
- **调整 stepStrideLength 和 maxSteps** - 找到适合您场景的平衡点
- **限制反射对象** - 只对需要反射的物体设置较高的平滑度

## 故障排除

### 常见问题

1. **没有反射效果**
   - 检查相机是否启用了深度纹理
   - 检查材质的平滑度是否足够高
   - 检查 SSR Render Feature 是否已添加到渲染管线

2. **反射质量差**
   - 减小 stepStrideLength
   - 增加 maxSteps
   - 减小 downSample

3. **性能问题**
   - 增加 stepStrideLength
   - 减小 maxSteps
   - 增加 downSample
   - 确保使用 HiZ Tracing 模式

### 错误信息

- **Shader error in 'HiZ_Shader'** - 确保所有着色器文件都已正确导入
- **Shader error in 'Hidden/SSR_Shader'** - 确保所有着色器文件都已正确导入

## 技术细节

- SSR 模块使用屏幕空间的深度和法线信息计算反射
- 深度金字塔用于加速光线追踪过程
- 支持线性追踪和 HiZ 追踪两种模式
- 可以与 URP 的其他功能无缝集成

## 版本兼容性

- Unity 2021.3.16 及以上版本
- URP 12.1.7 及以上版本
- 支持 DirectX 11 及以上图形 API

---

通过以上步骤，您应该能够成功启用和配置 SSR 模块，为您的场景添加真实的反射效果。