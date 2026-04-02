# PRT模块启用指南

## 模块概述

PRT（Precomputed Radiance Transfer）模块是一个基于球谐函数（Spherical Harmonics）的实时全局光照解决方案。该模块通过预计算场景中的光照传输信息，在运行时以较低的性能开销实现间接光照效果。

### 核心功能
- 基于Surfel的光照采样
- 球谐函数（SH9）光照编码
- 实时间接光照计算
- 支持多次反弹的全局光照

## 启用步骤

### 步骤1：确保URP使用延迟渲染

1. 打开 `Project Settings` -> `Graphics`
2. 找到当前的URP Asset（或创建一个新的）
3. 确保 `Depth Priming Mode` 设置为 `Auto` 或 `Forced`
4. 确保URP Renderer Data中使用延迟渲染（Deferred渲染路径）

### 步骤2：添加Render Features

在URP Renderer Data中添加以下两个Render Features：

#### 2.1 添加PRTRelight Feature
1. 选择URP Renderer Data资产
2. 点击 `Add Renderer Feature` -> `PRT Relight`
3. 该Feature会在不透明物体渲染后执行光照计算

#### 2.2 添加PRTComposite Feature
1. 选择URP Renderer Data资产
2. 点击 `Add Renderer Feature` -> `PRT Composite`
3. 设置 `Composite Material`：
   - 创建一个新材质
   - 使用 `NewVision/PRT/Composite` Shader
   - 将材质赋值给 `Composite Material` 字段

### 步骤3：创建ProbeVolume

1. 在场景中创建一个空GameObject，命名为 `ProbeVolume`
2. 添加 `ProbeVolume` 组件
3. 配置探针参数：
   - `Probe Size X/Y/Z`：探针网格的尺寸（如 8x4x8）
   - `Probe Grid Size`：探针之间的间距（如 2.0）
   - `Sky Light Intensity`：天空光强度
   - `GI Intensity`：全局光照强度

### 步骤4：创建Probe预制体

1. 创建一个Sphere GameObject
2. 添加 `Probe` 组件
3. 配置Probe组件：
   - 创建3个Cubemap RenderTexture（128x128）：
     - `RT_WorldPos`：存储世界位置
     - `RT_Normal`：存储法线
     - `RT_Albedo`：存储反照率
   - 设置Compute Shader：
     - `Surfel Sample CS`：`SurfelSampleCS.compute`
     - `Surfel ReLight CS`：`SurfelReLightCS.compute`
4. 将GameObject保存为Prefab
5. 将Prefab赋值给ProbeVolume的 `Probe Prefab` 字段

### 步骤5：创建ProbeVolumeData

1. 右键 `Project` 窗口
2. 选择 `Create` -> `NewVision` -> `PRT` -> `ProbeVolumeData`
3. 将创建的资产赋值给ProbeVolume的 `Data` 字段

### 步骤6：生成探针

1. 选中ProbeVolume GameObject
2. 在Inspector中点击 `Generate Probes` 按钮（如有自定义Editor）
3. 或者在运行时自动生成

### 步骤7：捕获探针数据

1. 进入Play模式
2. 调用 `ProbeVolume.ProbeCapture()` 方法
3. 或者在自定义Editor脚本中调用

## Frame Debugger验证

启用模块后，可以在Frame Debugger中看到以下专门的命名项：

1. **PRT Relight** - 间接光照计算Pass
   - 显示Surfel重新光照计算
   - 显示SH系数更新

2. **PRT Composite** - 间接光照合成Pass
   - 显示GI合成到场景的过程

### 如何使用Frame Debugger
1. 打开 `Window` -> `Analysis` -> `Frame Debugger`
2. 点击 `Enable`
3. 在渲染事件列表中查找 `PRT Relight` 和 `PRT Composite`

## 参数说明

### ProbeVolume 参数
| 参数 | 说明 | 默认值 |
|------|------|--------|
| Probe Size X/Y/Z | 探针网格尺寸 | 8/4/8 |
| Probe Grid Size | 探针间距 | 2.0 |
| Sky Light Intensity | 天空光强度 | 1.0 |
| GI Intensity | 全局光照强度 | 1.0 |

### Probe 参数
| 参数 | 说明 |
|------|------|
| RT_WorldPos | 世界位置Cubemap |
| RT_Normal | 法线Cubemap |
| RT_Albedo | 反照率Cubemap |
| Surfel Sample CS | Surfel采样Compute Shader |
| Surfel ReLight CS | Surfel重光照Compute Shader |

## 调试模式

### Probe Debug Mode
- `None`：不显示调试信息
- `Sphere Distribution`：显示球面采样分布
- `Sample Direction`：显示采样方向
- `Surfel`：显示Surfel位置和法线
- `Surfel Radiance`：显示Surfel辐射度

### ProbeVolume Debug Mode
- `None`：不显示调试信息
- `Probe Grid`：显示探针网格
- `Probe Radiance`：显示探针辐射度

## 注意事项

1. **性能考虑**
   - 探针数量越多，计算开销越大
   - 建议在PC平台使用中等密度探针网格（如8x4x8）

2. **场景变化**
   - 当场景几何体发生变化时，需要重新捕获探针数据
   - 动态物体不会影响预计算的GI

3. **光照要求**
   - 场景中需要有方向光或其他光源
   - 天空光通过SkyMask自动处理

4. **兼容性**
   - 仅支持URP延迟渲染路径
   - 需要Shader Model 4.5以上支持Compute Shader

## 常见问题

### Q: 为什么看不到GI效果？
A: 检查以下几点：
1. 确保URP使用延迟渲染
2. 确保PRTComposite材质正确设置
3. 确保探针数据已捕获
4. 检查GI Intensity是否为0

### Q: Frame Debugger中看不到PRT相关项？
A: 确保Render Features已正确添加到URP Renderer Data中

### Q: 探针捕获失败？
A: 检查以下几点：
1. 确保Probe预制体正确配置
2. 确保Compute Shader文件存在
3. 检查RenderTexture是否正确创建
