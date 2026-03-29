# PCSS (Percentage Closer Soft Shadows) for URP

## 概述

PCSS 是一种高级阴影技术，可以产生更真实的软阴影效果，相比传统的 PCF (Percentage Closer Filtering) 阴影，PCSS 能够根据光源大小和距离自动调整阴影的柔和度。

## 功能特性

- 基于 NVIDIA 的 PCSS 算法实现
- 支持 URP (Universal Render Pipeline) + 延迟渲染
- 可调节的采样参数
- 支持级联阴影混合
- 支持正交投影
- 抗阴影 acne 技术

## 如何启用

1. 在 Unity 编辑器中，打开 "Project Settings" → "Graphics"
2. 在 "Scriptable Render Pipeline Settings" 中选择你的 URP 配置文件
3. 点击 "Add Renderer Feature" 按钮
4. 在弹出的菜单中选择 "MyPCSS"
5. 配置 PCSS 的参数

## 参数说明

### 采样设置
- **Blocker 采样数**: 用于搜索遮挡物的采样点数量，影响阴影的准确性
- **PCF 采样数**: 用于 PCF 过滤的采样点数量，影响阴影的平滑度

### 柔和度
- **柔和度**: 控制阴影边缘的柔和程度
- **柔和度衰减**: 控制阴影柔和度随距离的衰减速度

### 阴影偏移 (抗 Acne)
- **最大静态梯度偏移**: 静态深度偏移，用于减少阴影 acne
- **Blocker 梯度偏移**: Blocker 搜索阶段的梯度偏移
- **PCF 梯度偏移**: PCF 过滤阶段的梯度偏移

### 级联混合
- **级联混合距离**: 控制阴影级联之间的混合距离
- **支持正交投影**: 是否支持正交相机投影

### 资源
- **噪声纹理**: 用于采样模式的噪声纹理，可提高阴影质量
- **PCSS 着色器**: PCSS 算法的实现着色器

## 性能注意事项

- 增加采样数会提高阴影质量，但会降低性能
- 建议在不同设备上测试并调整参数以获得最佳平衡
- 对于移动设备，建议使用较低的采样数

## 技术细节

- 使用 ScriptableRendererFeature 和 ScriptablePass 体系集成到 URP
- 实现了完整的 PCSS 算法：
  1. Blocker 搜索：找到遮挡物
  2. 计算半影大小：根据遮挡物距离计算阴影边缘柔和度
  3. PCF 过滤：使用计算出的半影大小进行滤波

## 兼容性

- 要求 Unity 2019.4 或更高版本
- 要求 URP 7.0.0 或更高版本
- 要求显卡支持 Shader Model 3.0 或更高
