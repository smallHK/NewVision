using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision.Integration
{
    public class GIIntegration : ScriptableRendererFeature
    {
        [System.Serializable]
        public class GISettings
        {
            [Header("直接光照")]
            public bool enablePCSS = true; // 软阴影
            public NewVision.PCSS.MyPCSS.PCSSSettings pcssSettings = new NewVision.PCSS.MyPCSS.PCSSSettings();
            public bool enableLTC = true; // 面光源
            public NewVision.LTC.LTCAreaLightFeatureSettings ltcSettings = new NewVision.LTC.LTCAreaLightFeatureSettings();

            [Header("环境光")]
            public bool enableIBL = true; // 环境光

            [Header("间接光照")]
            public bool enablePRT = false; // 间接光
            public bool enableVXGI = false; // 间接光

            [Header("后处理")]
            public bool enableSSAO = true; // GTAO
            public bool enableSSR = true; // 屏幕空间反射
            public NewVision.SSR.SSRSettings ssrSettings = new NewVision.SSR.SSRSettings();
        }

        public GISettings settings = new GISettings();

        // 各个模块的实例
        private NewVision.LTC.LTCAreaLightFeature _ltcFeature;
        private NewVision.PRT.PRTComposite _prtFeature;
        private NewVision.IBL.IBLComposite _iblFeature;
        private NewVision.SSR.MySSR _ssrFeature;
        private NewVision.VXGI.MyVXGI _vxgiFeature;
        private NewVision.PCSS.MyPCSS _pcssFeature;
        private NewVision.SSAO.MyGTAO _ssaoFeature;

        public override void Create()
        {
            // 初始化各个模块
            _ltcFeature = new NewVision.LTC.LTCAreaLightFeature();
            _prtFeature = new NewVision.PRT.PRTComposite();
            _iblFeature = new NewVision.IBL.IBLComposite();
            _ssrFeature = new NewVision.SSR.MySSR();
            _vxgiFeature = new NewVision.VXGI.MyVXGI();
            _pcssFeature = new NewVision.PCSS.MyPCSS();
            _ssaoFeature = new NewVision.SSAO.MyGTAO();

            // 应用设置到各个模块
            ApplySettings();

            // 各个模块在内部已经设置了默认的渲染事件
        }

        private void ApplySettings()
        {
            // 应用PCSS设置
            if (_pcssFeature != null)
            {
                _pcssFeature.settings = settings.pcssSettings;
            }

            // 应用LTC设置
            if (_ltcFeature != null)
            {
                // 使用反射设置私有字段
                var fieldInfo = typeof(NewVision.LTC.LTCAreaLightFeature).GetField("m_Settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(_ltcFeature, settings.ltcSettings);
                }
            }

            // 应用SSR设置
            if (_ssrFeature != null)
            {
                // 使用反射设置字段
                var fieldInfo = typeof(NewVision.SSR.MySSR).GetField("Settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(_ssrFeature, settings.ssrSettings);
                }
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // 每次渲染前应用最新设置
            ApplySettings();

            // 根据配置启用或禁用各个模块
            if (settings.enablePCSS)
            {
                _pcssFeature.AddRenderPasses(renderer, ref renderingData);
            }

            if (settings.enableLTC)
            {
                _ltcFeature.AddRenderPasses(renderer, ref renderingData);
            }

            if (settings.enableIBL)
            {
                _iblFeature.AddRenderPasses(renderer, ref renderingData);
            }

            if (settings.enablePRT)
            {
                _prtFeature.AddRenderPasses(renderer, ref renderingData);
            }

            if (settings.enableVXGI)
            {
                _vxgiFeature.AddRenderPasses(renderer, ref renderingData);
            }

            if (settings.enableSSAO)
            {
                _ssaoFeature.AddRenderPasses(renderer, ref renderingData);
            }

            if (settings.enableSSR)
            {
                _ssrFeature.AddRenderPasses(renderer, ref renderingData);
            }
        }

        protected override void Dispose(bool disposing)
        {
            // 释放资源
            // 注意：各个模块的Dispose方法是protected的，不能从外部直接调用
            // 它们会在各自的生命周期中自动处理资源释放
        }
    }
}
