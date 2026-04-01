Shader "NewVision/IBL/Composite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _IrradianceMap ("Irradiance Map", Cube) = "white" {}
        _PrefilterMap ("Prefilter Map", Cube) = "white" {}
        _BRDFLut ("BRDF Lut", 2D) = "white" {}
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BSDF/BRDF.hlsl"
            #include "brdf.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            samplerCUBE _IrradianceMap;
            samplerCUBE _PrefilterMap;
            sampler2D _BRDFLut;
            
            TEXTURE2D_X(_GBuffer0);
            TEXTURE2D_X(_GBuffer1);
            TEXTURE2D_X(_GBuffer2);
            SAMPLER(sampler_GBuffer0);
            SAMPLER(sampler_GBuffer1);
            SAMPLER(sampler_GBuffer2);

            /// <summary>
            /// 从屏幕空间坐标重建世界坐标
            /// 通过深度缓冲和逆视图投影矩阵计算
            /// </summary>
            float4 GetFragmentWorldPos(float2 screenPos)
            {
                float sceneRawDepth = SampleSceneDepth(screenPos);
                float4 ndc = float4(screenPos.x * 2 - 1, screenPos.y * 2 - 1, sceneRawDepth, 1);
                #if UNITY_UV_STARTS_AT_TOP
                    ndc.y *= -1;
                #endif
                float4 worldPos = mul(UNITY_MATRIX_I_VP, ndc);
                worldPos /= worldPos.w;

                return worldPos;
            }
            
            /// <summary>
            /// 从GBuffer解包法线
            /// 支持Oct编码和标准编码两种格式
            /// </summary>
            float3 UnpackNormalFromGBuffer(float3 packedNormal)
            {
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = packedNormal.xy * 2.0 - 1.0;
                    float3 n = float3(octNormal.x, octNormal.y, 1.0 - abs(octNormal.x) - abs(octNormal.y));
                    float t = max(-n.z, 0.0);
                    n.xy += n.xy >= 0.0 ? -t.xx : t.xx;
                    return normalize(n);
                #else
                    return normalize(packedNormal * 2.0 - 1.0);
                #endif
            }

            /// <summary>
            /// 获取BRDF LUT采样结果
            /// 如果启用自定义BRDF Lut则采样自定义纹理，否则使用URP内置的BRDF Lut
            /// </summary>
            float2 SampleBRDFLut(float NdotV, float roughness)
            {
                #if defined(_USE_CUSTOM_BRDF_LUT)
                    return tex2D(_BRDFLut, float2(NdotV, roughness)).rg;
                #else
                    #if defined(UNITY_VERSION_2022_1_OR_NEWER)
                        return GetPreIntegratedBRDF(NdotV, roughness);
                    #else
                        return tex2D(_BRDFLut, float2(NdotV, roughness)).rg;
                    #endif
                #endif
            }

            /// <summary>
            /// 片段着色器 - 执行IBL计算
            /// 实现基于PBR的图像光照，包括漫反射和镜面反射间接光照
            /// </summary>
            float4 frag (v2f i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);

                // ===== 第一步：从GBuffer获取几何和材质信息 =====
                float4 worldPos = GetFragmentWorldPos(i.uv);
                float3 albedo = SAMPLE_TEXTURE2D_X(_GBuffer0, sampler_GBuffer0, i.uv).xyz;
                float3 packedNormal = SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, i.uv).xyz;
                float3 normal = UnpackNormalFromGBuffer(packedNormal);
                float4 metallicSmoothnessAO = SAMPLE_TEXTURE2D_X(_GBuffer1, sampler_GBuffer1, i.uv);
                float metallic = metallicSmoothnessAO.r;
                float smoothness = metallicSmoothnessAO.a;
                float roughness = 1.0 - smoothness;

                // ===== 第二步：计算视线方向和反射方向 =====
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos.xyz);
                float3 reflectDir = reflect(-viewDir, normal);

                // ===== 第三步：计算菲涅尔基础反射率F0 =====
                // 非金属使用0.04作为基础值，金属使用albedo作为F0
                float3 F0 = float3(0.04, 0.04, 0.04);
                F0 = lerp(F0, albedo, metallic);

                // ===== 第四步：计算菲涅尔项（考虑粗糙度） =====
                float3 F = fresnelSchlickRoughness(max(dot(normal, viewDir), 0.0), F0, roughness);

                // ===== 第五步：计算漫反射系数 =====
                // kS为镜面反射比例，kD为漫反射比例
                // 金属不产生漫反射
                float3 kS = F;
                float3 kD = 1.0 - kS;
                kD *= 1.0 - metallic;

                // ===== 第六步：计算漫反射IBL =====
                // 从IrradianceMap采样获得环境漫反射光照
                float3 irradiance = texCUBE(_IrradianceMap, normal).rgb;
                float3 diffuse = irradiance * albedo;

                // ===== 第七步：计算镜面反射IBL =====
                // 使用粗糙度作为LOD级别采样PrefilterMap
                // 从BRDF LUT获取缩放和偏移值（使用URP内置或自定义）
                const float MAX_REFLECTION_LOD = 4.0;
                float3 prefilteredColor = texCUBElod(_PrefilterMap, float4(reflectDir, roughness * MAX_REFLECTION_LOD)).rgb;
                float NdotV = max(dot(normal, viewDir), 0.0);
                float2 brdf = SampleBRDFLut(NdotV, roughness);
                float3 specular = prefilteredColor * (F * brdf.x + brdf.y);

                // ===== 第八步：合并IBL贡献 =====
                float3 ibl = (kD * diffuse + specular);

                color.rgb += ibl;
                
                return color;
            }
            ENDHLSL
        }
    }
}
