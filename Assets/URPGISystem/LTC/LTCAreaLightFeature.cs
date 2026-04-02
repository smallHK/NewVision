using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NewVision.LTC
{
    /// <summary>
    /// LTC区域光Feature的设置类
    /// 包含区域光渲染的相关配置参数
    /// </summary>
    [System.Serializable]
    public class LTCAreaLightFeatureSettings
    {
        /// <summary>
        /// 渲染通道执行事件
        /// </summary>
        public RenderPassEvent m_RenderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
        
        /// <summary>
        /// LTC区域光渲染Shader
        /// </summary>
        public Shader          m_LTCPassShader;
        
        /// <summary>
        /// 最大区域光数量
        /// </summary>
        public int             m_MaxAreaLightCount = 16;
    }

    /// <summary>
    /// LTC区域光渲染Feature
    /// 负责创建和管理LTC区域光渲染通道
    /// 步骤1: Create() - 创建渲染通道实例
    /// 步骤2: AddRenderPasses() - 将渲染通道添加到渲染队列
    /// 步骤3: Dispose() - 释放资源
    /// </summary>
    public class LTCAreaLightFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// Feature名称（用于Frame Debugger显示）
        /// </summary>
        private const string kFeatureName = "LTC Area Light Feature";
        
        /// <summary>
        /// 渲染Feature的设置
        /// </summary>
        [SerializeField]
        private LTCAreaLightFeatureSettings m_Settings = new();
        
        /// <summary>
        /// LTC区域光渲染通道
        /// </summary>
        private LTCAreaLightRenderPass mPass;
        
        /// <summary>
        /// 创建渲染通道
        /// 在URP初始化时调用，用于创建渲染通道实例
        /// </summary>
        public override void Create()
        {
            if (m_Settings.m_LTCPassShader == null )
            {
                Debug.LogWarning("LTC Area Light RenderFeature: Missing shader");
                return;
            }
            mPass = new LTCAreaLightRenderPass(kFeatureName, m_Settings);
        }

        /// <summary>
        /// 向渲染器添加渲染通道
        /// 每帧渲染时调用，将渲染通道加入渲染队列
        /// </summary>
        /// <param name="renderer">渲染器</param>
        /// <param name="renderingData">渲染数据</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (mPass == null) return;
            renderer.EnqueuePass(mPass);
        }

        /// <summary>
        /// 释放资源
        /// 在Feature销毁时调用
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected override void Dispose(bool disposing)
        {
            if (mPass != null)
            {
                mPass.Dispose();
                mPass = null;
            }
        }
        
    }
}