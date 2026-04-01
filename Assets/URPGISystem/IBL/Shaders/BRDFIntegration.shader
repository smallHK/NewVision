Shader "Hidden/NewVision/IBL/BRDFIntegration"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "BRDFIntegration"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "brdf.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            /// <summary>
            /// BRDF积分着色器
            /// 生成BRDF查找表(LUT)
            /// R通道: 缩放因子
            /// G通道: 偏移因子
            /// </summary>
            float4 frag(v2f i) : SV_Target
            {
                float2 integratedBRDF = IntegrateBRDF(i.uv.x, i.uv.y);
                return float4(integratedBRDF, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
