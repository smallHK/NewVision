# IBL模块启用指南

本指南将详细说明如何在Unity项目中启用和配置IBL（Image-Based Lighting）模块。

## 目录

1. [前提条件](#前提条件)
2. [步骤1：添加RenderFeature](#步骤1添加renderfeature)
3. [步骤2：准备IBL资源](#步骤2准备ibl资源)
4. [步骤3：配置材质](#步骤3配置材质)
5. [步骤4：测试IBL效果](#步骤4测试ibl效果)
6. [常见问题与解决方案](#常见问题与解决方案)

## 前提条件

- Unity 2021.3.16或更高版本
- 项目已配置为使用URP（Universal Render Pipeline）
- 已导入IBL模块到项目中

## 步骤1：添加RenderFeature

1. **打开URP渲染器配置**：
   - 在Project Settings → Graphics中，选择当前使用的Universal Render Pipeline Asset
   - 找到"Renderer List"，选择你使用的Renderer（通常是"UniversalRenderer"）

2. **添加IBLComposite RenderFeature**：
   - 点击"Add Renderer Feature"按钮
   - 从列表中选择"IBLComposite"
   - 重命名为"IBL Composite"（可选）

3. **配置IBLComposite**：
   - 在Inspector面板中，找到"Composite Material"属性
   - 点击右侧的小圆圈，选择或创建一个使用"NewVision/IBL/Composite"着色器的材质

## 步骤2：准备IBL资源

IBL模块需要三种关键资源：

### 1. 环境立方体贴图（Irradiance Map）

- **创建方法**：
  1. 导入一张HDR环境贴图
  2. 在Inspector面板中，将Texture Type设置为"Default"
  3. 将Shape设置为"Cube"
  4. 点击"Apply"按钮

### 2. 预过滤贴图（Prefilter Map）

- **创建方法**：
  1. 使用Unity的Cubemap烘焙工具
  2. 或使用外部工具（如HDRI Haven）生成预过滤的立方体贴图
  3. 确保贴图有多个mipmap级别（至少5级）

### 3. BRDF查找表（BRDF LUT）

- **创建方法**：
  1. 创建一个新的RenderTexture
  2. 设置宽度和高度为512x512
  3. 使用BRDF生成着色器渲染到该纹理
  4. 或使用现成的BRDF LUT纹理

## 步骤3：配置材质

1. **配置IBL合成材质**：
   - 选择之前创建的IBL合成材质
   - 在Inspector面板中：
     - 设置"Irradiance Map"为你的环境立方体贴图
     - 设置"Prefilter Map"为你的预过滤贴图
     - 设置"BRDF Lut"为你的BRDF查找表

2. **配置场景材质**：
   - 确保场景中的材质使用支持PBR的着色器（如"Universal Render Pipeline/Lit"）
   - 为材质设置合适的金属度（Metallic）和粗糙度（Roughness）值
   - 确保材质启用了"Receive Global Illumination"选项

## 步骤4：测试IBL效果

1. **设置场景**：
   - 创建一个包含多个不同材质物体的场景
   - 添加一个天空盒或环境光

2. **运行场景**：
   - 进入Play模式
   - 观察物体表面的光照效果
   - 注意物体如何反射环境

3. **调整参数**：
   - 尝试不同的环境贴图
   - 调整材质的金属度和粗糙度
   - 观察IBL效果的变化

## 常见问题与解决方案

### 问题1：IBL效果不明显

**解决方案**：
- 检查环境贴图是否正确设置
- 确保材质的金属度和粗糙度值合适
- 检查RenderFeature是否正确添加到渲染器

### 问题2：着色器编译错误

**解决方案**：
- 确保使用的是URP兼容的着色器
- 检查材质属性是否正确设置
- 验证Unity版本是否符合要求

### 问题3：性能问题

**解决方案**：
- 降低环境贴图的分辨率
- 减少预过滤贴图的mipmap级别
- 优化场景复杂度

### 问题4：反射效果不正确

**解决方案**：
- 确保预过滤贴图生成正确
- 检查BRDF查找表是否合适
- 调整材质的反射强度

## 高级配置

### 自定义IBL效果

- **调整环境强度**：在IBL合成材质中添加强度参数
- **添加环境遮挡**：结合AO贴图增强真实感
- **实现动态环境**：使用动态生成的环境贴图

### 性能优化

- **使用较低分辨率的环境贴图**：对于移动设备
- **实现IBL的LOD系统**：根据距离调整IBL精度
- **使用异步加载**：避免IBL资源加载影响帧率

## 总结

IBL模块为你的场景提供了高质量的基于图像的光照效果，使物体能够真实地反射周围环境。通过正确配置和优化，你可以在保持性能的同时获得出色的视觉效果。

如果遇到任何问题，请参考本指南的常见问题部分，或查阅Unity官方文档了解更多关于IBL和URP的信息。
