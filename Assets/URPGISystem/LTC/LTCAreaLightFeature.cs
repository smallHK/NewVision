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
    /// </summary>
    public class LTCAreaLightFeature : ScriptableRendererFeature
    {
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
        /// </summary>
        public override void Create()
        {
            if (m_Settings.m_LTCPassShader == null )
            {
                Debug.LogWarning("LTC Area Light RenderFeature: Missing shader");
                return;
            }
            mPass = new LTCAreaLightRenderPass(name, m_Settings);
        }

        /// <summary>
        /// 向渲染器添加渲染通道
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