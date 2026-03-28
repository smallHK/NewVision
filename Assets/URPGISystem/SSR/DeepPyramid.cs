using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


namespace NewVision.SSR
{

    public class DeepPyramid : ScriptableRendererFeature
    {
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            throw new System.NotImplementedException();
        }

        public override void Create()
        {
            throw new System.NotImplementedException();
        }

        class DepthPyramidPass : ScriptableRenderPass
        {
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                throw new System.NotImplementedException();
            }
        }
    }


}
