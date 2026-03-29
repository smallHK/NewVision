using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision.PRT
{
    public class PRTComposite : ScriptableRendererFeature
    {
        class CustomRenderPass : ScriptableRenderPass
        {
            public Material blitMaterial;
            public RenderTargetHandle tempRTHandle;
            public RenderTargetIdentifier blitSrc;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor rtDesc = renderingData.cameraData.cameraTargetDescriptor;
                cmd.GetTemporaryRT(tempRTHandle.id, rtDesc);

                blitSrc = renderingData.cameraData.renderer.cameraColorTarget;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                RenderTargetIdentifier tempRT = tempRTHandle.Identifier();

                ProbeVolume[] volumes = GameObject.FindObjectsOfType(typeof(ProbeVolume)) as ProbeVolume[];
                ProbeVolume volume = volumes.Length==0 ? null : volumes[0];
                if(volume != null)
                {
                    cmd.Blit(blitSrc, tempRT, blitMaterial);
                    cmd.Blit(tempRT, blitSrc);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(tempRTHandle.id);
            }
        }

        public Material compositeMaterial;
        CustomRenderPass m_ScriptablePass;

        public override void Create()
        {
            m_ScriptablePass = new CustomRenderPass();

            m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            m_ScriptablePass.blitMaterial = compositeMaterial;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}