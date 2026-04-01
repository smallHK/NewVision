Shader "Hidden/NewVision/IBL/PrefilterConvolution"
{
    Properties
    {
        _MainTex ("Environment Map", Cube) = "white" {}
        _Roughness ("Roughness", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "PrefilterConvolution"

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
            float _Roughness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.worldPos = v.normal;
                return o;
            }

            /// <summary>
            /// 分布加权采样
            /// 根据GGX分布进行重要性采样，用于镜面反射预过滤
            /// </summary>
            float3 PrefilterEnvMap(float roughness, float3 R)
            {
                float3 N = R;
                float3 V = R;

                float3 prefilteredColor = float3(0.0, 0.0, 0.0);
                float totalWeight = 0.0;

                const uint SAMPLE_COUNT = 1024u;
                for (uint i = 0u; i < SAMPLE_COUNT; ++i)
                {
                    float2 Xi = Hammersley(i, SAMPLE_COUNT);
                    float3 H = ImportanceSampleGGX(Xi, N, roughness);
                    float3 L = normalize(2.0 * dot(V, H) * H - V);

                    float NdotL = max(dot(N, L), 0.0);
                    if (NdotL > 0.0)
                    {
                        float D = DistributionGGX(N, H, roughness);
                        float NdotH = max(dot(N, H), 0.0);
                        float HdotV = max(dot(H, V), 0.0);
                        float pdf = D * NdotH / (4.0 * HdotV) + 0.0001;

                        float resolution = 512.0;
                        float saTexel = 4.0 * PI / (6.0 * resolution * resolution);
                        float saSample = 1.0 / (float(SAMPLE_COUNT) * pdf + 0.0001);

                        float mipLevel = roughness == 0.0 ? 0.0 : 0.5 * log2(saSample / saTexel);

                        prefilteredColor += SAMPLE_TEXTURECUBE_LOD(_MainTex, sampler_MainTex, L, mipLevel).rgb * NdotL;
                        totalWeight += NdotL;
                    }
                }

                return prefilteredColor / totalWeight;
            }

            /// <summary>
            /// 镜面反射预过滤
            /// 根据粗糙度生成预过滤环境贴图
            /// </summary>
            float4 frag(v2f i) : SV_Target
            {
                float3 R = normalize(i.worldPos);
                float3 prefilteredColor = PrefilterEnvMap(_Roughness, R);
                return float4(prefilteredColor, 1.0);
            }
            ENDHLSL
        }
    }
}
