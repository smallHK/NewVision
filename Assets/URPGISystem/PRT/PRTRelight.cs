using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision.PRT
{
    /// <summary>
    /// PRT重光照渲染Feature
    /// 负责在每帧更新探针的球谐系数，计算间接光照
    /// 
    /// 渲染流程：
    /// 1. Create() - 创建渲染通道实例
    /// 2. AddRenderPasses() - 将渲染通道添加到渲染队列
    /// 3. Execute() - 执行重光照计算
    ///    - 交换当前帧和上一帧的系数体素
    ///    - 清空当前帧的系数体素
    ///    - 设置全局Shader参数
    ///    - 对每个Probe执行ReLight操作
    /// </summary>
    [ExecuteAlways]
    public class PRTRelight : ScriptableRendererFeature
    {
        /// <summary>
        /// Feature名称（用于Frame Debugger显示）
        /// </summary>
        private const string kFeatureName = "PRT Relight Feature";
        
        /// <summary>
        /// 渲染通道名称（用于Frame Debugger显示）
        /// </summary>
        private const string kPassName = "PRT Relight";
        
        /// <summary>
        /// PRT重光照渲染通道
        /// 负责执行每帧的间接光照计算
        /// </summary>
        class CustomRenderPass : ScriptableRenderPass
        {
            /// <summary>
            /// 性能分析采样器（用于Frame Debugger显示）
            /// </summary>
            private ProfilingSampler mProfilingSampler;
            
            /// <summary>
            /// 构造函数
            /// 步骤1: 创建ProfilingSampler用于Frame Debugger显示
            /// </summary>
            public CustomRenderPass(string passName)
            {
                mProfilingSampler = new ProfilingSampler(passName);
            }
            
            /// <summary>
            /// 相机设置回调
            /// 当前无需特殊设置
            /// </summary>
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
            }

            /// <summary>
            /// 执行渲染
            /// 步骤1: 获取命令缓冲区（使用kPassName命名，用于Frame Debugger显示）
            /// 步骤2: 开启性能分析采样范围（用于Frame Debugger显示）
            /// 步骤3: 查找场景中的ProbeVolume
            /// 步骤4: 如果存在ProbeVolume，设置全局参数：
            ///        - 交换当前帧和上一帧的系数体素（用于多次反弹）
            ///        - 清空当前帧的系数体素
            ///        - 设置体素网格参数（大小、角点、尺寸）
            ///        - 设置系数体素缓冲区
            ///        - 设置光照强度参数
            /// 步骤5: 遍历所有Probe，执行ReLight计算
            /// 步骤6: 结束性能分析采样范围
            /// 步骤7: 执行命令缓冲区并释放
            /// </summary>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(kPassName);
                
                using (new ProfilingScope(cmd, mProfilingSampler))
                {
                    ProbeVolume[] volumes = GameObject.FindObjectsOfType(typeof(ProbeVolume)) as ProbeVolume[];
                    ProbeVolume volume = volumes.Length == 0 ? null : volumes[0];
                    
                    if (volume != null)
                    {
                        volume.SwapLastFrameCoefficientVoxel();
                        volume.ClearCoefficientVoxel(cmd);

                        Vector3 corner = volume.GetVoxelMinCorner();
                        Vector4 voxelCorner = new Vector4(corner.x, corner.y, corner.z, 0);
                        Vector4 voxelSize = new Vector4(volume.probeSizeX, volume.probeSizeY, volume.probeSizeZ, 0);
                        cmd.SetGlobalFloat("_coefficientVoxelGridSize", volume.probeGridSize);
                        cmd.SetGlobalVector("_coefficientVoxelSize", voxelSize);
                        cmd.SetGlobalVector("_coefficientVoxelCorner", voxelCorner);
                        cmd.SetGlobalBuffer("_coefficientVoxel", volume.coefficientVoxel);
                        cmd.SetGlobalBuffer("_lastFrameCoefficientVoxel", volume.lastFrameCoefficientVoxel);
                        cmd.SetGlobalFloat("_skyLightIntensity", volume.skyLightIntensity);
                        cmd.SetGlobalFloat("_GIIntensity", volume.GIIntensity);
                    }

                    Probe[] probes = GameObject.FindObjectsOfType(typeof(Probe)) as Probe[];
                    foreach (var probe in probes)
                    {
                        if (probe == null) continue;
                        probe.TryInit();
                        probe.ReLight(cmd);
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            /// <summary>
            /// 相机清理回调
            /// 当前无需特殊清理
            /// </summary>
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
            }
        }

        /// <summary>
        /// 渲染通道实例
        /// </summary>
        CustomRenderPass m_ScriptablePass;

        /// <summary>
        /// 创建渲染通道
        /// 步骤1: 创建渲染通道实例
        /// 步骤2: 设置渲染通道执行时机（在不透明物体渲染后）
        ///        注意：对于延迟渲染，此时GBuffer已可用
        /// </summary>
        public override void Create()
        {
            m_ScriptablePass = new CustomRenderPass(kPassName);

            m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        /// <summary>
        /// 向渲染器添加渲染通道
        /// 步骤1: 将渲染通道加入渲染队列
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}
