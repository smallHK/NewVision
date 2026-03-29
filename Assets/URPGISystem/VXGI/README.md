# VXGI 模块使用指南

## 模块介绍

VXGI (Voxel-based Global Illumination) 是一种基于体素的全局光照技术，能够为场景提供真实的间接光照效果。本实现基于 URP (Universal Render Pipeline) 和延迟渲染模式，提供了高质量的全局光照解决方案。

## 安装方法

1. 确保项目已经使用 URP 渲染管线
2. 将 VXGI 目录复制到项目的 Assets 文件夹中
3. 确保所有 shader 文件和 compute shader 文件都已正确导入

## 启用方法

### 1. 配置 URP 渲染管线

1. 在 Unity 编辑器中，选择 `Project Settings > Graphics`
2. 确保 `Scriptable Render Pipeline Settings` 已设置为 URP 资产
3. 选择 URP 资产，确保 `Rendering Mode` 设置为 `Deferred`

### 2. 添加 VXGI 渲染功能

1. 在 Project 窗口中，找到 `Assets/Settings/UniversalRenderer.asset` 文件并双击打开
2. 在 Inspector 窗口中，点击 `Add Renderer Feature` 按钮
3. 从下拉菜单中选择 `MyVXGI`
4. 分配相应的 shader 和 compute shader：
   - Voxelization Shader: `Voxelization.shader`
   - Lighting Shader: `VXGILighting.shader`
   - Voxel Mipmap CS: `VXGIMipmap.compute`

## 使用方法

### 调整参数

在 `MyVXGI` 渲染功能的 Inspector 窗口中，可以调整以下参数：

#### Voxel Volume Settings
- **Voxel Bound**: 体素体积的大小（世界空间单位）
- **Voxel Resolution**: 体素体积的分辨率（32^3, 64^3, 128^3 等）
- **Follow Camera**: 是否让体素体积中心跟随相机位置

#### VXGI Rendering Settings
- **Indirect Diffuse Intensity**: 间接漫反射强度
- **Indirect Specular Intensity**: 间接高光强度
- **Cone Trace Steps**: 锥追踪的步数
- **Cone Aperture**: 锥追踪的 aperture 大小

### 场景设置

1. 确保场景中有光源（方向光、点光源或聚光灯）
2. 确保场景中的物体使用支持延迟渲染的材质
3. 调整 VXGI 参数以获得最佳效果

## 技术原理

1. **体素化**：使用几何着色器将场景物体转换为体素
2. **Mipmap 生成**：使用 compute shader 生成体素 mipmap
3. **锥追踪**：在光照阶段使用锥追踪算法计算间接光照
4. **光照集成**：将 VXGI 间接光照与 URP 原生光照系统集成

## 性能考虑

- 体素分辨率越高，效果越好，但性能消耗也越大
- 锥追踪步数越多，效果越好，但性能消耗也越大
- 建议在高端设备上使用较高的体素分辨率和锥追踪步数
- 在移动设备上，建议使用较低的体素分辨率和锥追踪步数

## 注意事项

1. VXGI 仅在延迟渲染模式下工作
2. 体素体积大小应根据场景大小进行调整
3. 过大的体素体积或过高的分辨率可能导致性能问题
4. 某些复杂的材质可能无法正确体素化

## 故障排除

- **Shader 编译错误**：确保所有 shader 文件都已正确导入，并且 URP 版本与 shader 兼容
- **光照效果不明显**：尝试增加间接光照强度或调整锥追踪参数
- **性能问题**：降低体素分辨率或锥追踪步数

## 示例场景

在设置完成后，可以创建一个简单的测试场景：
1. 创建一个包含多个物体的场景
2. 添加一个方向光
3. 调整 VXGI 参数，观察间接光照效果
4. 对比启用和禁用 VXGI 时的光照效果差异
