using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision
{

    [Serializable]
    public class SSGISettings
    {

        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        public Material material = null;

        [Range(8, 128)]
        public int samplesCount = 8;

        [Range(0.0f, 512.0f)]
        public float indirectAmount = 8;

        [Range(0.0f, 5.0f)]
        public float noiseAmount = 2;

        public bool noise = true;
        public bool enabled = true;

    }

    public class SSGIPass : ScriptableRenderPass
    {

        private string m_profilerTag;
        private RenderTargetIdentifier m_source;
        private RenderTargetIdentifier m_tmpRT1;

        public Material material;
        public int samplesCount;

        public float indirectAmount;
        public float noiseAmount;

        public bool noise;
        public bool enabled;
       

        public void Setup(RenderTargetIdentifier source)
        {
            m_source = source;
        }

        public SSGIPass(string profilerTag)
        {
            m_profilerTag = profilerTag; 
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var width = cameraTextureDescriptor.width;
            var height = cameraTextureDescriptor.height;

            m_tmpRT1 = SetupRenderTargetIdentifier(cmd, 0, width, height);
         
        }

        private RenderTargetIdentifier SetupRenderTargetIdentifier(CommandBuffer cmd, int id, int width, int height)
        {
            int tmpId = Shader.PropertyToID($"SSGI_{id}_RT");

            cmd.GetTemporaryRT(tmpId, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);

            var rt = new RenderTargetIdentifier(tmpId);
            ConfigureTarget(rt);
            return rt;
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
        }


        public override void FrameCleanup(CommandBuffer cmd)
        {
            //base.FrameCleanup(cmd);
        }
    }

    public class SSGI : ScriptableRendererFeature
    {
        public SSGISettings settings = new SSGISettings();
        private SSGIPass pass;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {

            var src = renderer.cameraColorTarget;
            pass.Setup(src);
            renderer.EnqueuePass(pass);

        }

        public override void Create()
        {
            if(pass == null)
            {
                pass = new SSGIPass("SSGI");
            }

            pass.material = settings.material;
            pass.samplesCount = settings.samplesCount;
            pass.indirectAmount = settings.indirectAmount;
            pass.noiseAmount = settings.noiseAmount;
            pass.noise = settings.noise;
            pass.enabled = settings.enabled;
            pass.renderPassEvent = settings.renderPassEvent; 
        }
    }




}

