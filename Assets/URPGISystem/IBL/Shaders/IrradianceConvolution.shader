Shader "Hidden/NewVision/IBL/IrradianceConvolution"
{
    Properties
    {
        _MainTex ("Environment Map", Cube) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "IrradianceConvolution"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "brdf.hlsl"

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

            TEXTURECUBE(_MainTex);
            SAMPLER(sampler_MainTex);

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.worldPos = v.normal;
                return o;
            }

            /// <summary>
            /// 计算半球方向到球面坐标的立体角微分
            /// </summary>
            float2 HammersleyNoBitOps(uint i, uint N)
            {
                float E1 = frac(float(i) * 0.5 + 0.5);
                float E2 = frac(float(i) * 0.61803398874989484820458683436564);
                return float2(E1, E2);
            }

            /// <summary>
            /// 使用低差异序列采样半球方向
            /// </summary>
            float3 SampleHemisphere(float2 Xi, float3 N)
            {
                float phi = 2.0 * PI * Xi.x;
                float cosTheta = sqrt(1.0 - Xi.y);
                float sinTheta = sqrt(Xi.y);

                float3 H;
                H.x = cos(phi) * sinTheta;
                H.y = sin(phi) * sinTheta;
                H.z = cosTheta;

                float3 up = abs(N.z) < 0.999 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
                float3 tangent = normalize(cross(up, N));
                float3 bitangent = cross(N, tangent);

                float3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
                return normalize(sampleVec);
            }

            /// <summary>
            /// 漫反射辐照度卷积
            /// 对环境贴图进行半球积分，计算漫反射间接光照
            /// </summary>
            float4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.worldPos);
                float3 irradiance = float3(0.0, 0.0, 0.0);

                float3 up = float3(0.0, 1.0, 0.0);
                float3 right = normalize(cross(up, N));
                up = cross(N, right);

                float sampleDelta = 0.025;
                float nrSamples = 0.0;

                const uint SAMPLE_COUNT = 1024u;
                for (uint i = 0u; i < SAMPLE_COUNT; ++i)
                {
                    float2 Xi = HammersleyNoBitOps(i, SAMPLE_COUNT);
                    float3 sampleDir = SampleHemisphere(Xi, N);

                    float3 L = normalize(right * sampleDir.x + up * sampleDir.y + N * sampleDir.z);

                    float NdotL = max(dot(N, L), 0.0);
                    irradiance += SAMPLE_TEXTURECUBE(_MainTex, sampler_MainTex, L).rgb * NdotL;
                    nrSamples++;
                }

                irradiance = PI * irradiance / nrSamples;

                return float4(irradiance, 1.0);
            }
            ENDHLSL
        }
    }
}
