/**
 * PrefilterConvolution.shader
 * 镜面反射预过滤着色器
 * 
 * 功能：
 * - 对环境Cubemap进行GGX重要性采样预过滤
 * - 生成多级mipmap，每级对应不同粗糙度
 * - 用于PBR镜面反射间接光照的Prefilter Map
 * 
 * 原理：
 * - 使用GGX分布的重要性采样减少采样数
 * - 根据粗糙度计算采样权重
 * - 使用mipmap级别来近似预过滤效果
 * - 5级mipmap对应粗糙度 0.0, 0.25, 0.5, 0.75, 1.0
 */
Shader "Hidden/NewVision/IBL/PrefilterConvolution"
{
    Properties
    {
        /// <summary>环境Cubemap</summary>
        _MainTex ("Environment Map", Cube) = "white" {}
        
        /// <summary>粗糙度 (0-1)</summary>
        _Roughness ("Roughness", Range(0.0, 1.0)) = 0.0
        
        /// <summary>当前渲染的Cubemap面索引 (0-5)</summary>
        _FaceIndex ("Face Index", Int) = 0
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

            // ============================================================================
            // 顶点着色器输入结构
            // ============================================================================
            struct appdata
            {
                float4 vertex : POSITION;   // 顶点位置
                float2 uv : TEXCOORD0;      // 纹理坐标 [0,1]
            };

            // ============================================================================
            // 顶点着色器输出结构 / 片段着色器输入结构
            // ============================================================================
            struct v2f
            {
                float4 vertex : SV_POSITION;    // 裁剪空间位置
                float2 uv : TEXCOORD0;          // 纹理坐标 [0,1]
            };

            // ============================================================================
            // 纹理和采样器声明
            // ============================================================================
            TEXTURECUBE(_MainTex);
            SAMPLER(sampler_MainTex);
            float _Roughness;
            int _FaceIndex;

            // ============================================================================
            // 顶点着色器
            // ============================================================================
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // ============================================================================
            // 辅助函数
            // ============================================================================

            /**
             * 将UV坐标和面索引转换为Cubemap采样方向
             * 
             * UV范围[0,1]转换为[-1,1]的方向向量
             * 对应Cubemap的6个面：
             * - 0: +X (Right)  - 看向+X方向，左是+Z，右是-Z，上是+Y，下是-Y
             * - 1: -X (Left)   - 看向-X方向，左是-Z，右是+Z，上是+Y，下是-Y
             * - 2: +Y (Top)    - 看向+Y方向，左是-X，右是+X，上是+Z，下是-Z
             * - 3: -Y (Bottom) - 看向-Y方向，左是-X，右是+X，上是-Z，下是+Z
             * - 4: +Z (Front)  - 看向+Z方向，左是-X，右是+X，上是+Y，下是-Y
             * - 5: -Z (Back)   - 看向-Z方向，左是+X，右是-X，上是+Y，下是-Y
             */
            float3 GetCubemapDirection(float2 uv, int face)
            {
                float2 st = uv * 2.0 - 1.0;
                float3 dir = float3(0, 0, 0);

                if (face == 0)       dir = float3(1.0, st.y, -st.x);    // +X
                else if (face == 1)  dir = float3(-1.0, st.y, st.x);    // -X
                else if (face == 2)  dir = float3(st.x, 1.0, st.y);     // +Y
                else if (face == 3)  dir = float3(st.x, -1.0, -st.y);   // -Y
                else if (face == 4)  dir = float3(st.x, st.y, 1.0);     // +Z
                else                 dir = float3(-st.x, st.y, -1.0);   // -Z

                return normalize(dir);
            }

            // ============================================================================
            // 预过滤环境贴图核心函数
            // ============================================================================

            /**
             * 预过滤环境贴图
             * 
             * 使用GGX重要性采样对环境贴图进行预过滤
             * 
             * 原理：
             * 1. 使用GGX分布的重要性采样生成采样方向
             * 2. 根据PDF计算mipmap级别以减少锯齿
             * 3. 加权累加采样结果
             * 
             * @param roughness 粗糙度 (0-1)
             * @param R 反射方向
             * @return 预过滤后的颜色
             */
            float3 PrefilterEnvMap(float roughness, float3 R)
            {
                // Step 1: 初始化
                float3 N = R;           // 法线方向（与反射方向相同）
                float3 V = R;           // 视线方向（与反射方向相同）
                
                float3 prefilteredColor = float3(0.0, 0.0, 0.0);
                float totalWeight = 0.0;

                // Step 2: 蒙特卡洛积分
                const uint SAMPLE_COUNT = 1024u;
                for (uint i = 0u; i < SAMPLE_COUNT; ++i)
                {
                    // 2.1 生成Hammersley序列
                    float2 Xi = Hammersley(i, SAMPLE_COUNT);
                    
                    // 2.2 使用GGX重要性采样生成半程向量
                    float3 H = ImportanceSampleGGX(Xi, N, roughness);
                    
                    // 2.3 计算光线方向（反射半程向量）
                    float3 L = normalize(2.0 * dot(V, H) * H - V);

                    // 2.4 计算权重并累加
                    float NdotL = max(dot(N, L), 0.0);
                    if (NdotL > 0.0)
                    {
                        // 计算GGX分布值
                        float D = DistributionGGX(N, H, roughness);
                        float NdotH = max(dot(N, H), 0.0);
                        float HdotV = max(dot(H, V), 0.0);
                        
                        // 计算PDF（概率密度函数）
                        float pdf = D * NdotH / (4.0 * HdotV) + 0.0001;

                        // 计算mipmap级别
                        // 使用PDF来决定采样哪个mipmap级别
                        // PDF越小，采样越稀疏，需要更高的mipmap级别来减少噪声
                        float resolution = 512.0;
                        float saTexel = 4.0 * PI / (6.0 * resolution * resolution);
                        float saSample = 1.0 / (float(SAMPLE_COUNT) * pdf + 0.0001);

                        float mipLevel = roughness == 0.0 ? 0.0 : 0.5 * log2(saSample / saTexel);

                        // 采样环境贴图（使用mipmap）
                        prefilteredColor += SAMPLE_TEXTURECUBE_LOD(_MainTex, sampler_MainTex, L, mipLevel).rgb * NdotL;
                        totalWeight += NdotL;
                    }
                }

                // Step 3: 归一化
                return prefilteredColor / totalWeight;
            }

            // ============================================================================
            // 片段着色器 - 镜面反射预过滤核心
            // ============================================================================
            
            /**
             * 片段着色器
             * 
             * 计算流程：
             * 1. 根据UV和面索引获取反射方向
             * 2. 调用PrefilterEnvMap进行预过滤
             * 3. 返回预过滤后的颜色
             */
            float4 frag(v2f i) : SV_Target
            {
                // Step 1: 获取当前像素对应的反射方向
                float3 R = GetCubemapDirection(i.uv, _FaceIndex);
                
                // Step 2: 执行预过滤
                float3 prefilteredColor = PrefilterEnvMap(_Roughness, R);
                
                return float4(prefilteredColor, 1.0);
            }
            ENDHLSL
        }
    }
}
