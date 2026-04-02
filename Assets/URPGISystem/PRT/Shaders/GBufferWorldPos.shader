Shader "NewVision/PRT/GBufferWorldPos"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            // 步骤1: 顶点着色器 - 变换顶点并计算世界位置
            v2f vert (appdata v)
            {
                v2f o;
                // 步骤1.1: 变换顶点到裁剪空间
                o.vertex = TransformObjectToHClip(v.vertex);
                // 步骤1.2: 变换顶点到世界空间
                o.worldPos = TransformObjectToWorld(v.vertex);
                return o;
            }

            // 步骤2: 片元着色器 - 输出世界位置
            float4 frag (v2f i) : SV_Target
            {
                // 步骤2.1: 输出世界空间位置（用于GBuffer捕获）
                return float4(i.worldPos, 1.0);
            }
            ENDHLSL
        }
    }
}
