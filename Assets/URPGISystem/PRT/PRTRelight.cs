using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision.PRT
{
    [ExecuteAlways]
    public class PRTRelight : ScriptableRendererFeature
    {
        class CustomRenderPass : ScriptableRenderPass
        {
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                ProbeVolume[] volumes = GameObject.FindObjectsOfType(typeof(ProbeVolume)) as ProbeVolume[];
                ProbeVolume volume = volumes.Length==0 ? null : volumes[0];
                if(volume != null)
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
                foreach(var probe in probes)
                {
                    if(probe==null) continue;
                    probe.TryInit();
                    probe.ReLight(cmd);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
            }
        }

        CustomRenderPass m_ScriptablePass;

        public override void Create()
        {
            m_ScriptablePass = new CustomRenderPass();

            m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}