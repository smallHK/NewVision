/**
 * BRDFIntegration.shader
 * BRDF积分着色器
 * 
 * 功能：
 * - 预计算Cook-Torrance BRDF的积分结果
 * - 生成BRDF查找表(LUT)
 * - 用于PBR镜面反射IBL的分离求和近似
 * 
 * 原理：
 * - 将BRDF积分分离为两个部分：缩放因子和偏移因子
 * - 输入：NdotV（法线与视线夹角余弦）和粗糙度
 * - 输出：R通道=缩放因子，G通道=偏移因子
 * - 使用蒙特卡洛积分预计算，运行时只需查表
 * 
 * 分离求和近似：
 * Lspec = ∫ L * f * cosθ dω
 *       ≈ ∫ L dω * ∫ f * cosθ dω
 *       = PrefilterMap * (F * scale + bias)
 */
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
            // 片段着色器 - BRDF积分核心
            // ============================================================================
            
            /**
             * 片段着色器
             * 
             * 计算流程：
             * 1. 从UV坐标获取NdotV和粗糙度
             * 2. 构建局部坐标系（N = (0,0,1), V = (√(1-NdotV²), 0, NdotV)）
             * 3. 使用Hammersley序列生成采样方向
             * 4. 对每个采样方向计算BRDF贡献
             * 5. 累加并归一化得到缩放因子(A)和偏移因子(B)
             * 
             * 输入UV映射：
             * - uv.x = NdotV (法线与视线的余弦值)
             * - uv.y = roughness (粗糙度)
             * 
             * 输出：
             * - R通道 = 缩放因子 (A)
             * - G通道 = 偏移因子 (B)
             * 
             * 运行时使用：
             * F = F0 + (1-F0) * (1-NdotV)^5
             * brdf = F * A + B
             */
            float4 frag(v2f i) : SV_Target
            {
                // Step 1: 从UV坐标获取输入参数
                // uv.x = NdotV (法线与视线的余弦值)
                // uv.y = roughness (粗糙度)
                float NdotV = i.uv.x;
                float roughness = i.uv.y;

                // Step 2: 构建局部坐标系
                // 法线固定为(0,0,1)
                // 视线方向根据NdotV计算
                float3 V;
                V.x = sqrt(1.0 - NdotV * NdotV);  // sin(acos(NdotV))
                V.y = 0.0;
                V.z = NdotV;                       // cos(acos(NdotV))

                // Step 3: 初始化累加器
                float A = 0.0;  // 缩放因子累加器
                float B = 0.0;  // 偏移因子累加器

                float3 N = float3(0.0, 0.0, 1.0);

                // Step 4: 蒙特卡洛积分
                const uint SAMPLE_COUNT = 512u;
                for (uint i = 0u; i < SAMPLE_COUNT; ++i)
                {
                    // 4.1 生成Hammersley序列
                    float2 Xi = Hammersley(i, SAMPLE_COUNT);
                    
                    // 4.2 使用GGX重要性采样生成半程向量
                    float3 H = ImportanceSampleGGX(Xi, N, roughness);
                    
                    // 4.3 计算光线方向（反射半程向量）
                    float3 L = normalize(2.0 * dot(V, H) * H - V);

                    // 4.4 计算各项参数
                    float NdotL = max(L.z, 0.0);
                    float NdotH = max(H.z, 0.0);
                    float VdotH = max(dot(V, H), 0.0);

                    // 4.5 如果光线在法线半球内，累加BRDF贡献
                    if (NdotL > 0.0)
                    {
                        // 计算几何遮蔽项（IBL版本）
                        float G = GeometrySmith(N, V, L, roughness);
                        
                        // 计算几何可见性
                        float G_Vis = (G * VdotH) / (NdotH * NdotV);
                        
                        // 计算菲涅尔项（Schlick近似）
                        float Fc = pow(1.0 - VdotH, 5.0);

                        // 累加缩放因子和偏移因子
                        // A = ∫ (1-Fc) * G_Vis dω
                        // B = ∫ Fc * G_Vis dω
                        A += (1.0 - Fc) * G_Vis;
                        B += Fc * G_Vis;
                    }
                }

                // Step 5: 归一化
                A /= float(SAMPLE_COUNT);
                B /= float(SAMPLE_COUNT);

                // Step 6: 输出结果
                // R通道 = 缩放因子 (A)
                // G通道 = 偏移因子 (B)
                return float4(A, B, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
