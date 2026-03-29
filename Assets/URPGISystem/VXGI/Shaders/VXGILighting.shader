Shader "Hidden/VXGI/VXGILighting"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off        
        Pass
        {

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // --------------------------
            // URP 核心头文件
            // --------------------------
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // --------------------------
            // VXGI 全局变量
            // --------------------------
            CBUFFER_START(VXGISettings)
                float _IndirectDiffuseIntensity;
                float _IndirectSpecularIntensity;
                int _ConeTraceSteps;
                float _ConeAperture;
                float4x4 _WorldToVoxel;
                float _VoxelBound;
                int _VoxelResolution;
            CBUFFER_END

            TEXTURE3D(_VoxelRadiance);
            SAMPLER(sampler_VoxelRadiance);
            
            // GBuffer 纹理
            TEXTURE2D(_GBuffer0);
            TEXTURE2D(_GBuffer1);
            TEXTURE2D(_GBuffer2);
            SAMPLER(sampler_GBuffer0);
            SAMPLER(sampler_GBuffer1);
            SAMPLER(sampler_GBuffer2);

            // --------------------------
            // GBuffer 数据结构
            // --------------------------
            struct GBufferData {
                float3 albedo;
                float3 normalWS;
                float metallic;
                float smoothness;
                float3 emission;
                float occlusion;
            };

            // --------------------------
            // 采样 GBuffer
            // --------------------------
            GBufferData SampleGBuffer(float2 uv, bool depthOnly) {
                GBufferData gbuffer;
                
                // 采样 GBuffer 纹理
                float4 gbuffer0 = SAMPLE_TEXTURE2D(_GBuffer0, sampler_GBuffer0, uv);
                float4 gbuffer1 = SAMPLE_TEXTURE2D(_GBuffer1, sampler_GBuffer1, uv);
                float4 gbuffer2 = SAMPLE_TEXTURE2D(_GBuffer2, sampler_GBuffer2, uv);
                
                // 解析 GBuffer 数据
                gbuffer.albedo = gbuffer0.rgb;
                gbuffer.normalWS = normalize(gbuffer0.a * 2.0 - 1.0);
                gbuffer.metallic = gbuffer1.r;
                gbuffer.smoothness = gbuffer1.g;
                gbuffer.emission = gbuffer2.rgb;
                gbuffer.occlusion = gbuffer2.a;
                
                return gbuffer;
            }

            // --------------------------
            // 顶点着色器结构
            // --------------------------
            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.pos = TransformObjectToHClip(input.pos.xyz);
                output.uv = input.uv;
                return output;
            }
            
            // ==================================================
            // 直接光照计算
            // ==================================================
            float3 ComputeDirectLighting(Light light, float3 N, float3 V, float roughness, float metallic, float3 albedo)
            {
                // 计算直接光照
                float3 directLight = light.color * saturate(dot(N, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
                return directLight * albedo;
            }

            // ==================================================
            // VXGI 锥追踪实现
            // ==================================================
            float3 ConeTrace(float3 posVS, float3 dirVS, float aperture, int steps)
            {
                float3 color = 0;
                float dist = 0.1f;
                float stepSize = _VoxelBound / _VoxelResolution;

                for (int i = 0; i < steps; i++)
                {
                    float3 samplePos = posVS + dirVS * dist;
                    float radius = dist * aperture;
                    float mip = log2(radius * _VoxelResolution / _VoxelBound);
                    
                    if (all(samplePos >= 0) && all(samplePos <= 1))
                    {
                        float4 voxel = SAMPLE_TEXTURE3D_LOD(_VoxelRadiance, sampler_VoxelRadiance, samplePos, mip);
                        color += voxel.rgb * voxel.a;
                    }

                    dist += stepSize * (1 + i * 0.5f);
                }

                return color / steps;
            }

            float3 ComputeVXGI_Diffuse(float3 posWS, float3 normalWS)
            {
                float3 posVS = mul(_WorldToVoxel, float4(posWS, 1)).xyz;
                float3 normalVS = normalize(mul((float3x3)_WorldToVoxel, normalWS));
                
                // 简单的锥追踪（后续可扩展为半球采样）
                float3 indirect = ConeTrace(posVS, normalVS, _ConeAperture, _ConeTraceSteps);
                return indirect * _IndirectDiffuseIntensity;
            }

            float3 ComputeVXGI_Specular(float3 posWS, float3 viewWS, float3 normalWS, float roughness)
            {
                float3 posVS = mul(_WorldToVoxel, float4(posWS, 1)).xyz;
                float3 reflVS = normalize(mul((float3x3)_WorldToVoxel, reflect(-viewWS, normalWS)));
                
                float aperture = _ConeAperture * (roughness + 0.1f);
                float3 indirect = ConeTrace(posVS, reflVS, aperture, _ConeTraceSteps);
                return indirect * _IndirectSpecularIntensity;
            }


            // ==================================================
            // 环境光照计算
            // ==================================================
            float3 ComputeIBL(float3 N, float3 V, float roughness, float metallic, float3 albedo)
            {
                // 使用URP内置的SH环境光
                float3 envDiff = SampleSH(N);
                return envDiff * albedo * (1 - metallic);
            }

            // --------------------------
            // 主片段着色器
            // --------------------------
            half4 Frag(Varyings input) : SV_Target
            {
                // 1. 重建世界空间位置
                float depth = SampleSceneDepth(input.uv);
                float4 posHCS = float4(input.uv * 2 - 1, depth, 1);
                float4 posWS = mul(unity_CameraToWorld, mul(unity_MatrixInvVP, posHCS));
                posWS /= posWS.w;

                // 2. 采样 URP GBuffer
                GBufferData gbuffer = SampleGBuffer(input.uv, true);
                float3 albedo = gbuffer.albedo;
                float3 N = gbuffer.normalWS;
                float roughness = 1.0 - gbuffer.smoothness;
                float metallic = gbuffer.metallic;
                float3 emission = gbuffer.emission;
                float occlusion = gbuffer.occlusion;

                // 3. 计算观察方向
                float3 V = SafeNormalize(_WorldSpaceCameraPos - posWS.xyz);

                // 4. 获取 URP 主光源（带阴影）
                Light mainLight = GetMainLight();

                // --------------------------
                // 光照计算
                // --------------------------
                float3 finalColor = 0;

                // A. 直接光照（使用URP原生）
                finalColor += ComputeDirectLighting(mainLight, N, V, roughness, metallic, albedo);

                // B. 间接光照（使用VXGI）
                finalColor += ComputeVXGI_Diffuse(posWS.xyz, N);
                finalColor += ComputeVXGI_Specular(posWS.xyz, V, N, roughness);

                // C. 环境光照（使用URP SH）
                finalColor += ComputeIBL(N, V, roughness, metallic, albedo);

                // D. 自发光 +  occlusion
                finalColor += emission;
                finalColor *= occlusion;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}