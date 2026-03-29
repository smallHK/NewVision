Shader "Hidden/Universal Render Pipeline/MyExtension/PCSS"
{

    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_pcss
            
            // 关键字
            #pragma multi_compile POISSON_32 POISSON_64
            #pragma shader_feature USE_FALLOFF
            #pragma shader_feature USE_CASCADE_BLENDING
            #pragma shader_feature USE_STATIC_BIAS
            #pragma shader_feature USE_BLOCKER_BIAS
            #pragma shader_feature USE_PCF_BIAS
            #pragma shader_feature ORTHOGRAPHIC_SUPPORTED
            // 引入 URP 核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadow.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // 材质属性
            CBUFFER_START(UnityPerMaterial)
            sampler2D _MainTex;
            float4 _MainTex_ST;
            CBUFFER_END

            int Blocker_Samples;
            int PCF_Samples;
            float Softness;
            float SoftnessFalloff;
            float RECEIVER_PLANE_MIN_FRACTIONAL_ERROR;
            float Blocker_GradientBias;
            float PCF_GradientBias;
            float CascadeBlendDistance;
            float _ShadowDistance;
            sampler2D _NoiseTexture;
            float4 NoiseCoords;

            // 输入输出结构
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 ray : TEXCOORD1;
                #if defined(ORTHOGRAPHIC_SUPPORTED)
                float3 orthoPosNear : TEXCOORD2;
                float3 orthoPosFar  : TEXCOORD3;
                #endif
            };
            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                // 计算视空间射线 (用于透视相机重建位置)
                float4 clipPos = float4(input.positionOS.xy * 2.0, 1.0, 1.0);
                float4 viewPos = mul(unity_CameraInvProjection, clipPos);
                output.ray = viewPos.xyz / viewPos.w;

                #if defined(ORTHOGRAPHIC_SUPPORTED)
                // 正交相机支持
                clipPos.y *= _ProjectionParams.x;
                float3 orthoPosNear = mul(unity_CameraInvProjection, float4(clipPos.x, clipPos.y, -1, 1)).xyz;
                float3 orthoPosFar  = mul(unity_CameraInvProjection, float4(clipPos.x, clipPos.y,  1, 1)).xyz;
                orthoPosNear.z *= -1;
                orthoPosFar.z *= -1;
                output.orthoPosNear = orthoPosNear;
                output.orthoPosFar = orthoPosFar;
                #endif

                return output;
            }
            //-----------------------------------------------------------------------------
            // 泊松采样盘
            //-----------------------------------------------------------------------------
            #if defined(POISSON_32)
            static const float2 PoissonOffsets[32] = {
                float2(0.06407013, 0.05409927), float2(0.7366577, 0.5789394),
                float2(-0.6270542, -0.5320278), float2(-0.4096107, 0.8411095),
                float2(0.6849564, -0.4990818), float2(-0.874181, -0.04579735),
                float2(0.9989998, 0.0009880066), float2(-0.004920578, -0.9151649),
                float2(0.1805763, 0.9747483), float2(-0.2138451, 0.2635818),
                float2(0.109845, 0.3884785), float2(0.06876755, -0.3581074),
                float2(0.374073, -0.7661266), float2(0.3079132, -0.1216763),
                float2(-0.3794335, -0.8271583), float2(-0.203878, -0.07715034),
                float2(0.5912697, 0.1469799), float2(-0.88069, 0.3031784),
                float2(0.5040108, 0.8283722), float2(-0.5844124, 0.5494877),
                float2(0.6017799, -0.1726654), float2(-0.5554981, 0.1559997),
                float2(-0.3016369, -0.3900928), float2(-0.5550632, -0.1723762),
                float2(0.925029, 0.2995041), float2(-0.2473137, 0.5538505),
                float2(0.9183037, -0.2862392), float2(0.2469421, 0.6718712),
                float2(0.3916397, -0.4328209), float2(-0.03576927, -0.6220032),
                float2(-0.04661255, 0.7995201), float2(0.4402924, 0.3640312)
            };
            #else
            static const float2 PoissonOffsets[64] = {
                float2(0.0617981, 0.07294159), float2(0.6470215, 0.7474022),
                float2(-0.5987766, -0.7512833), float2(-0.693034, 0.6913887),
                float2(0.6987045, -0.6843052), float2(-0.9402866, 0.04474335),
                float2(0.8934509, 0.07369385), float2(0.1592735, -0.9686295),
                float2(-0.05664673, 0.995282), float2(-0.1203411, -0.1301079),
                float2(0.1741608, -0.1682285), float2(-0.09369049, 0.3196758),
                float2(0.185363, 0.3213367), float2(-0.1493771, -0.3147511),
                float2(0.4452095, 0.2580113), float2(-0.1080467, -0.5329178),
                float2(0.1604507, 0.5460774), float2(-0.4037193, -0.2611179),
                float2(0.5947998, -0.2146744), float2(0.3276062, 0.9244621),
                float2(-0.6518704, -0.2503952), float2(-0.3580975, 0.2806469),
                float2(0.8587891, 0.4838005), float2(-0.1596546, -0.8791054),
                float2(-0.3096867, 0.5588146), float2(-0.5128918, 0.1448544),
                float2(0.8581337, -0.424046), float2(0.1562584, -0.5610626),
                float2(-0.7647934, 0.2709858), float2(-0.3090832, 0.9020988),
                float2(0.3935608, 0.4609676), float2(0.3929337, -0.5010948),
                float2(-0.8682281, -0.1990303), float2(-0.01973724, 0.6478714),
                float2(-0.3897587, -0.4665619), float2(-0.7416366, -0.4377831),
                float2(-0.5523247, 0.4272514), float2(-0.5325066, 0.8410385),
                float2(0.3085465, -0.7842533), float2(0.8400612, -0.200119),
                float2(0.6632416, 0.3067062), float2(-0.4462856, -0.04265022),
                float2(0.06892014, 0.812484), float2(0.5149567, -0.7502338),
                float2(0.6464897, -0.4666451), float2(-0.159861, 0.1038342),
                float2(0.6455986, 0.04419327), float2(-0.7445076, 0.5035095),
                float2(0.9430245, 0.3139912), float2(0.0349884, -0.7968109),
                float2(-0.9517487, 0.2963554), float2(-0.7304786, -0.01006928),
                float2(-0.5862702, -0.5531025), float2(0.3029106, 0.09497032),
                float2(0.09025345, -0.3503742), float2(0.4356628, -0.0710125),
                float2(0.4112572, 0.7500054), float2(0.3401214, -0.3047142),
                float2(-0.2192158, -0.6911137), float2(-0.4676369, 0.6570358),
                float2(0.6295372, 0.5629555), float2(0.1253822, 0.9892166),
                float2(-0.1154335, 0.8248222), float2(-0.4230408, -0.7129914)
            };
            #endif
            //-----------------------------------------------------------------------------
            // 辅助函数
            //-----------------------------------------------------------------------------
            inline float2 Rotate(float2 pos, float2 rotationTrig)
            {
                return float2(pos.x * rotationTrig.x - pos.y * rotationTrig.y, pos.y * rotationTrig.x + pos.x * rotationTrig.y);
            }

            inline float GetScale(float4 cascadeWeights)
            {
                float scale = 1.0;
                scale = (cascadeWeights.y > 0.0) ? 2.0 : scale;
                scale = (cascadeWeights.z > 0.0) ? 4.0 : scale;
                scale = (cascadeWeights.w > 0.0) ? 8.0 : scale;
                return 1.0 / scale;
            }

            //-----------------------------------------------------------------------------
            // 视空间位置重建
            //-----------------------------------------------------------------------------
            inline float3 computeCameraSpacePosFromDepth(Varyings i, float rawDepth)
            {
                #if defined(ORTHOGRAPHIC_SUPPORTED)
                float3 vposPersp = i.ray * Linear01Depth(rawDepth, _ZBufferParams);
                float3 vposOrtho = lerp(i.orthoPosNear, i.orthoPosFar, rawDepth);
                return lerp(vposPersp, vposOrtho, unity_OrthoParams.w);
                #else
                return i.ray * Linear01Depth(rawDepth, _ZBufferParams);
                #endif
            }

            //-----------------------------------------------------------------------------
            // 接收平面深度偏置 (Receiver Plane Bias)
            //-----------------------------------------------------------------------------
            float2 getReceiverPlaneDepthBias(float3 shadowCoord)
            {
                float3 dx = ddx(shadowCoord);
                float3 dy = ddy(shadowCoord);
                float2 biasUV;
                biasUV.x = dy.y * dx.z - dx.y * dy.z;
                biasUV.y = dx.x * dy.z - dy.x * dx.z;
                biasUV *= 1.0f / ((dx.x * dy.y) - (dx.y * dy.x));
                return biasUV;
            }
            //-----------------------------------------------------------------------------
            // 步骤 1: 搜索遮挡物 (Blocker Search)
            //-----------------------------------------------------------------------------
            float2 FindBlocker(float2 uv, float depth, float scale, float searchUV, float2 receiverPlaneDepthBias, float2 rotationTrig)
            {
                float avgBlockerDepth = 0.0;
                float numBlockers = 0.0;
                float blockerSum = 0.0;

                for (int i = 0; i < Blocker_Samples; i++)
                {
                     float2 offset = PoissonOffsets[i] * searchUV * scale;
                     offset = Rotate(offset, rotationTrig);                  
                     
                    // 采样阴影贴图深度
                    float shadowMapDepth = SAMPLE_TEXTURE2D_LOD(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture, uv + offset, 0).r;
                    float biasedDepth = depth;
                    #if defined(USE_BLOCKER_BIAS)
                    biasedDepth += dot(offset, receiverPlaneDepthBias) * Blocker_GradientBias;
                    #endif
                    #if UNITY_REVERSED_Z
                    if (shadowMapDepth > biasedDepth)
                    #else
                    if (shadowMapDepth < biasedDepth)
                    #endif
                    {
                        blockerSum += shadowMapDepth;
                        numBlockers += 1.0;
                    }

                }
                avgBlockerDepth = blockerSum / max(numBlockers, 1.0);
                #if UNITY_REVERSED_Z
                avgBlockerDepth = 1.0 - avgBlockerDepth;
                #endif

                return float2(avgBlockerDepth, numBlockers);
            }
            //-----------------------------------------------------------------------------
            // 步骤 3: PCF 过滤采样
            //-----------------------------------------------------------------------------
            float PCF_Filter(float2 uv, float depth, float scale, float filterRadiusUV, float2 receiverPlaneDepthBias, float penumbra, float2 rotationTrig)
            {
                float sum = 0.0f;
                
                #if UNITY_REVERSED_Z
                receiverPlaneDepthBias *= -1.0;
                #endif

                for (int i = 0; i < PCF_Samples; i++)
                {
                    float2 offset = PoissonOffsets[i] * filterRadiusUV * scale;
                    offset = Rotate(offset, rotationTrig);

                    float biasedDepth = depth;
                    #if defined(USE_PCF_BIAS)
                    biasedDepth += dot(offset, receiverPlaneDepthBias) * PCF_GradientBias;
                    #endif

                    // URP 标准阴影采样
                    float4 shadowCoord = float4(uv + offset, biasedDepth, 0.0);
                    float value = MainLightRealtimeShadow(shadowCoord);
                    sum += value;
                }
                sum /= PCF_Samples;
                return sum;
            }
            //-----------------------------------------------------------------------------
            // PCSS 主逻辑
            //-----------------------------------------------------------------------------
            float PCSS_Main(float4 coords, float2 receiverPlaneDepthBias, float random, float scale, float4 lightShadowData)
            {
                float2 uv = coords.xy;
                float depth = coords.z;
                float zAwareDepth = depth;

                #if UNITY_REVERSED_Z
                zAwareDepth = 1.0 - depth;
                #endif
                float rotationAngle = random * 3.1415926;
                float2 rotationTrig = float2(cos(rotationAngle), sin(rotationAngle));

                // 步骤 1: 遮挡物搜索
                float searchSize = Softness * saturate(zAwareDepth - .02) / zAwareDepth;
                float2 blockerInfo = FindBlocker(uv, depth, scale, searchSize, receiverPlaneDepthBias, rotationTrig);

                if (blockerInfo.y < 1)
                {
                    return 1.0; // 无遮挡物
                }
                
                // 步骤 2: 半影大小计算
                float penumbra = zAwareDepth - blockerInfo.x;
                
                #if defined(USE_FALLOFF)
                penumbra = 1.0 - pow(1.0 - penumbra, SoftnessFalloff);
                #endif

                float filterRadiusUV = penumbra * Softness;

                // 步骤 3: 过滤
                float shadow = PCF_Filter(uv, depth, scale, filterRadiusUV, receiverPlaneDepthBias, penumbra, rotationTrig);
                return lerp(lightShadowData.r, 1.0f, shadow);

            }
            //-----------------------------------------------------------------------------
            // 片段着色器
            //-----------------------------------------------------------------------------
            half4 frag_pcss (Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                // 1. 重建世界坐标
                float rawDepth = SampleSceneDepth(i.uv);
                float3 vpos = computeCameraSpacePosFromDepth(i, rawDepth);
                float4 wpos = mul(unity_CameraToWorld, float4(vpos, 1));

                // 2. 获取主光源
                Light mainLight = GetMainLight();

                // 3. 计算阴影坐标 (使用 URP 内置函数)
                float4 shadowCoord = TransformWorldToShadowCoord(wpos.xyz);


                // 4. 随机旋转 (噪声纹理)
                float random = 1.0;
                if (_NoiseTexture != 0)
                {
                    random = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_MainTex, i.uv * NoiseCoords.xy * _ScreenParams.xy).a;
                    random = mad(random, 2.0, -1.0);
                }

                // 5. 接收平面偏置
                float2 receiverPlaneDepthBias = 0.0;
                #if defined(USE_STATIC_BIAS) || defined(USE_BLOCKER_BIAS) || defined(USE_PCF_BIAS)
                receiverPlaneDepthBias = getReceiverPlaneDepthBias(shadowCoord.xyz);
                
                #if defined(USE_STATIC_BIAS)
                    float4 shadowTexelSize = _MainLightShadowmapTexture_TexelSize;
                    float fractionalSamplingError = 2.0 * dot(shadowTexelSize.xy, abs(receiverPlaneDepthBias));
                    fractionalSamplingError = min(fractionalSamplingError, RECEIVER_PLANE_MIN_FRACTIONAL_ERROR);
                    #if UNITY_REVERSED_Z
                    fractionalSamplingError *= -1.0;
                    #endif
                    shadowCoord.z -= fractionalSamplingError;
                #endif
                #endif

                // 6. 执行 PCSS
                // 注意：URP 级联逻辑封装较深，这里简化为单级联处理，保留核心 PCSS 算法
                float scale = 1.0; 
                float shadow = PCSS_Main(shadowCoord, receiverPlaneDepthBias, random, scale, mainLight.shadowData);

                // 7. 应用阴影
                col.rgb *= shadow;
                return col;
            }
            ENDHLSL
        }


    }
}