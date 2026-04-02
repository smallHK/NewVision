/**
 * IBLComposite.shader
 * IBL合成着色器
 * 
 * 功能：
 * - 从GBuffer获取场景几何和材质信息
 * - 使用预计算的IBL贴图计算间接光照
 * - 将IBL结果合成到场景颜色
 * 
 * 基于PBR的Image-Based Lighting实现
 * 支持漫反射和镜面反射间接光照
 */
Shader "NewVision/IBL/Composite"
{
    Properties
    {
        /// <summary>主纹理（场景颜色缓冲）</summary>
        _MainTex ("Texture", 2D) = "white" {}
        
        /// <summary>漫反射辐照度贴图 - 用于漫反射IBL</summary>
        _IrradianceMap ("Irradiance Map", Cube) = "white" {}
        
        /// <summary>镜面反射预过滤贴图 - 用于镜面反射IBL</summary>
        _PrefilterMap ("Prefilter Map", Cube) = "white" {}
        
        /// <summary>BRDF查找表 - 用于镜面反射BRDF积分</summary>
        _BRDFLut ("BRDF Lut", 2D) = "white" {}
        
        /// <summary>是否使用自定义BRDF Lut（Toggle）</summary>
        [Toggle(_USE_CUSTOM_BRDF_LUT)] _UseCustomBRDFLut ("Use Custom BRDF Lut", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _USE_CUSTOM_BRDF_LUT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "brdf.hlsl"

            // ============================================================================
            // 顶点着色器输入结构
            // ============================================================================
            struct appdata
            {
                float4 vertex : POSITION;   // 顶点位置
                float2 uv : TEXCOORD0;      // 纹理坐标
            };

            // ============================================================================
            // 顶点着色器输出结构 / 片段着色器输入结构
            // ============================================================================
            struct v2f
            {
                float2 uv : TEXCOORD0;      // 纹理坐标（屏幕空间UV）
                float4 vertex : SV_POSITION; // 裁剪空间位置
            };

            // ============================================================================
            // 顶点着色器
            // ============================================================================
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // ============================================================================
            // 纹理和采样器声明
            // ============================================================================
            
            /// <summary>场景颜色纹理</summary>
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            /// <summary>漫反射辐照度Cubemap</summary>
            TEXTURECUBE(_IrradianceMap);
            SAMPLER(sampler_IrradianceMap);

            /// <summary>镜面反射预过滤Cubemap</summary>
            TEXTURECUBE(_PrefilterMap);
            SAMPLER(sampler_PrefilterMap);

            /// <summary>BRDF查找表</summary>
            TEXTURE2D(_BRDFLut);
            SAMPLER(sampler_BRDFLut);
            
            /// <summary>GBuffer0 - Albedo + Ambient Occlusion</summary>
            TEXTURE2D_X(_GBuffer0);
            /// <summary>GBuffer1 - Metallic + Smoothness + AO</summary>
            TEXTURE2D_X(_GBuffer1);
            /// <summary>GBuffer2 - Normal</summary>
            TEXTURE2D_X(_GBuffer2);
            SAMPLER(sampler_GBuffer0);
            SAMPLER(sampler_GBuffer1);
            SAMPLER(sampler_GBuffer2);

            // ============================================================================
            // 辅助函数
            // ============================================================================

            /**
             * 从屏幕空间坐标重建世界坐标
             * 
             * 原理：
             * 1. 采样深度缓冲获取场景深度
             * 2. 构建NDC坐标
             * 3. 使用逆视图投影矩阵变换到世界空间
             * 
             * @param screenPos 屏幕空间UV坐标
             * @return 世界坐标位置
             */
            float4 GetFragmentWorldPos(float2 screenPos)
            {
                // 采样场景深度
                float sceneRawDepth = SampleSceneDepth(screenPos);
                
                // 构建NDC坐标（范围[-1, 1]）
                float4 ndc = float4(screenPos.x * 2 - 1, screenPos.y * 2 - 1, sceneRawDepth, 1);
                
                // 处理平台差异（DirectX vs OpenGL的Y轴方向）
                #if UNITY_UV_STARTS_AT_TOP
                    ndc.y *= -1;
                #endif
                
                // 使用逆视图投影矩阵变换到世界空间
                float4 worldPos = mul(UNITY_MATRIX_I_VP, ndc);
                worldPos /= worldPos.w;

                return worldPos;
            }
            
            /**
             * 从GBuffer解包法线
             * 
             * 支持两种编码格式：
             * - Oct编码：URP默认使用，节省空间
             * - 标准编码：直接存储XYZ，范围[0,1]
             * 
             * @param packedNormal GBuffer中存储的打包法线
             * @return 归一化的世界空间法线
             */
            float3 UnpackNormalFromGBuffer(float3 packedNormal)
            {
                #if defined(_GBUFFER_NORMALS_OCT)
                    // Oct编码解码
                    float2 octNormal = packedNormal.xy * 2.0 - 1.0;
                    float3 n = float3(octNormal.x, octNormal.y, 1.0 - abs(octNormal.x) - abs(octNormal.y));
                    float t = max(-n.z, 0.0);
                    n.xy += n.xy >= 0.0 ? -t.xx : t.xx;
                    return normalize(n);
                #else
                    // 标准编码解码
                    return normalize(packedNormal * 2.0 - 1.0);
                #endif
            }

            /**
             * 采样BRDF LUT
             * 
             * @param NdotV 法线与视线的余弦值
             * @param roughness 表面粗糙度
             * @return float2.x = 缩放因子, float2.y = 偏移因子
             */
            float2 SampleBRDFLut(float NdotV, float roughness)
            {
                return SAMPLE_TEXTURE2D(_BRDFLut, sampler_BRDFLut, float2(NdotV, roughness)).rg;
            }

            // ============================================================================
            // 片段着色器 - IBL计算核心
            // ============================================================================
            
            /**
             * 片段着色器
             * 
             * IBL计算流程：
             * 1. 从GBuffer获取几何和材质信息
             * 2. 计算视线方向和反射方向
             * 3. 计算菲涅尔基础反射率F0
             * 4. 计算漫反射IBL（IrradianceMap采样）
             * 5. 计算镜面反射IBL（PrefilterMap + BRDF LUT）
             * 6. 合并结果并叠加到场景颜色
             * 
             * @param i 片段着色器输入
             * @return 最终颜色
             */
            float4 frag (v2f i) : SV_Target
            {
                // ===== 获取当前场景颜色 =====
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // ===== 采样深度缓冲 =====
                float sceneRawDepth = SampleSceneDepth(i.uv);
                
                // ===== 深度有效性检查 =====
                // 深度<=0表示天空盒或无效像素，直接返回原颜色
                if (sceneRawDepth <= 0.0 || sceneRawDepth >= 1.0)
                {
                    return color;
                }

                // ===== Step 1: 从GBuffer获取几何和材质信息 =====
                
                // 重建世界坐标
                float4 worldPos = GetFragmentWorldPos(i.uv);
                
                // 获取Albedo（GBuffer0.xyz）
                float3 albedo = SAMPLE_TEXTURE2D_X(_GBuffer0, sampler_GBuffer0, i.uv).xyz;
                
                // 获取并解包法线（GBuffer2）
                float3 packedNormal = SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, i.uv).xyz;
                float3 normal = UnpackNormalFromGBuffer(packedNormal);
                
                // 获取Metallic和Smoothness（GBuffer1）
                float4 metallicSmoothnessAO = SAMPLE_TEXTURE2D_X(_GBuffer1, sampler_GBuffer1, i.uv);
                float metallic = metallicSmoothnessAO.r;
                float smoothness = metallicSmoothnessAO.a;
                float roughness = 1.0 - smoothness;

                // ===== Step 2: 计算视线方向和反射方向 =====
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos.xyz);
                float3 reflectDir = reflect(-viewDir, normal);

                // ===== Step 3: 计算菲涅尔基础反射率F0 =====
                // 非金属使用0.04作为基础值，金属使用albedo作为F0
                float3 F0 = float3(0.04, 0.04, 0.04);
                F0 = lerp(F0, albedo, metallic);

                // ===== Step 4: 计算菲涅尔项（考虑粗糙度） =====
                float3 F = fresnelSchlickRoughness(max(dot(normal, viewDir), 0.0), F0, roughness);

                // ===== Step 5: 计算漫反射系数 =====
                // kS为镜面反射比例，kD为漫反射比例
                // 金属不产生漫反射
                float3 kS = F;
                float3 kD = 1.0 - kS;
                kD *= 1.0 - metallic;

                // ===== Step 6: 计算漫反射IBL =====
                // 从IrradianceMap采样获得环境漫反射光照
                float3 irradiance = SAMPLE_TEXTURECUBE(_IrradianceMap, sampler_IrradianceMap, normal).rgb;
                float3 diffuse = irradiance * albedo;

                // ===== Step 7: 计算镜面反射IBL =====
                // 使用粗糙度作为LOD级别采样PrefilterMap
                const float MAX_REFLECTION_LOD = 4.0;
                float3 prefilteredColor = SAMPLE_TEXTURECUBE_LOD(_PrefilterMap, sampler_PrefilterMap, reflectDir, roughness * MAX_REFLECTION_LOD).rgb;
                
                // 从BRDF LUT获取缩放和偏移值
                float NdotV = max(dot(normal, viewDir), 0.0);
                float2 brdf = SampleBRDFLut(NdotV, roughness);
                
                // 计算最终镜面反射贡献
                float3 specular = prefilteredColor * (F * brdf.x + brdf.y);

                // ===== Step 8: 合并IBL贡献 =====
                float3 ibl = (kD * diffuse + specular);

                // 叠加到场景颜色
                color.rgb += ibl;
                
                return color;
            }
            ENDHLSL
        }
    }
}
