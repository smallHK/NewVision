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
            // URP 原生库
            // --------------------------
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadow.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBuffer.hlsl"
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

            // --------------------------
            // 输入输出结构
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
            // 【扩展点 1】LTC 直接光替换区
            // 提示：将下方 ComputeDirectLighting 替换为 LTC 矩阵查找 + 积分
            // 步骤：
            // 1. 引入 LTC LUT 纹理 (ltc_mat, ltc_mag)
            // 2. 根据 roughness 查找 LTC 矩阵
            // 3. 变换光线方向并积分
            // ==================================================
            float3 ComputeDirectLighting(Light light, float3 N, float3 V, float roughness, float metallic, float3 albedo)
            {
                // 【保留】URP 原生直接光计算 (可替换为 LTC)
                return LightingPhysicallyBased(light, N, V, roughness, metallic, albedo, 1.0 - roughness);
            }

            // ==================================================
            // 【核心】VXGI 锥追踪实现
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
                
                // 简单的半球采样 (可优化为多方向锥追踪)
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
            // 【扩展点 2】IBL / PRT 扩展区
            // 提示：替换下方 IBL 采样为自定义卷积或 PRT 球谐
            // ==================================================
            float3 ComputeIBL(float3 N, float3 V, float roughness, float metallic, float3 albedo)
            {
                // 【保留】URP 原生 SH 环境光 (可替换为 IBL/PRT)
                float3 envDiff = SampleSH(N);
                return envDiff * albedo * (1 - metallic);
            }

            // --------------------------
            // 主 Fragment Shader
            // --------------------------
            half4 Frag(Varyings input) : SV_Target
            {
                // 1. 从深度重建世界坐标
                float depth = SampleSceneDepth(input.uv);
                float4 posHCS = float4(input.uv * 2 - 1, depth, 1);
                float4 posWS = mul(unity_CameraToWorld, mul(unity_MatrixInvVP, posHCS));
                posWS /= posWS.w;

                // 2. 解码 URP GBuffer
                GBufferData gbuffer = SampleGBuffer(input.uv, true);
                float3 albedo = gbuffer.albedo;
                float3 N = gbuffer.normalWS;
                float roughness = 1.0 - gbuffer.smoothness;
                float metallic = gbuffer.metallic;
                float3 emission = gbuffer.emission;
                float occlusion = gbuffer.occlusion;

                // 3. 计算视图方向
                float3 V = SafeNormalize(_WorldSpaceCameraPos - posWS.xyz);

                // 4. 获取 URP 主光源 (含阴影)
                Light mainLight = GetMainLight(input.uv);

                // --------------------------
                // 光照流水线
                // --------------------------
                float3 finalColor = 0;

                // A. 直接光 (当前：URP原生 | 扩展：替换为 LTC)
                finalColor += ComputeDirectLighting(mainLight, N, V, roughness, metallic, albedo);

                // B. 间接光 (当前：VXGI | 扩展：可混合 PRT)
                finalColor += ComputeVXGI_Diffuse(posWS.xyz, N);
                finalColor += ComputeVXGI_Specular(posWS.xyz, V, N, roughness);

                // C. 环境光 (当前：URP SH | 扩展：替换为 IBL)
                finalColor += ComputeIBL(N, V, roughness, metallic, albedo);

                // D. 自发光 + 遮挡
                finalColor += emission;
                finalColor *= occlusion;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}