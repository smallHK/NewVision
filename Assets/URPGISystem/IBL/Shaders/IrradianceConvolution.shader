/**
 * IrradianceConvolution.shader
 * 漫反射辐照度卷积着色器
 * 
 * 功能：
 * - 对环境Cubemap进行半球卷积积分
 * - 计算每个法线方向接收到的总辐照度
 * - 生成用于PBR漫反射间接光照的Irradiance Map
 * 
 * 原理：
 * - 使用Hammersley低差异序列进行蒙特卡洛积分
 * - 在法线周围的半球方向采样环境贴图
 * - 结果是低频的漫反射光照，32x32分辨率足够
 */
Shader "Hidden/NewVision/IBL/IrradianceConvolution"
{
    Properties
    {
        /// <summary>环境Cubemap</summary>
        _MainTex ("Environment Map", Cube) = "white" {}
        
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
            Name "IrradianceConvolution"

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
             * - 0: +X (Right)
             * - 1: -X (Left)
             * - 2: +Y (Top)
             * - 3: -Y (Bottom)
             * - 4: +Z (Front)
             * - 5: -Z (Back)
             */
            float3 GetCubemapDirection(float2 uv, int face)
            {
                float2 st = uv * 2.0 - 1.0;
                float3 dir = float3(0, 0, 0);

                if (face == 0)       dir = float3(1.0, -st.y, -st.x);   // +X
                else if (face == 1)  dir = float3(-1.0, -st.y, st.x);   // -X
                else if (face == 2)  dir = float3(st.x, 1.0, st.y);     // +Y
                else if (face == 3)  dir = float3(st.x, -1.0, -st.y);   // -Y
                else if (face == 4)  dir = float3(st.x, -st.y, 1.0);    // +Z
                else                 dir = float3(-st.x, -st.y, -1.0);  // -Z

                return normalize(dir);
            }

            /**
             * Hammersley低差异序列（无位操作版本）
             * 用于生成均匀分布的随机采样点
             * 
             * @param i 采样索引
             * @param N 总采样数
             * @return 二维坐标，用于球面采样
             */
            float2 HammersleyNoBitOps(uint i, uint N)
            {
                float E1 = frac(float(i) * 0.5 + 0.5);
                float E2 = frac(float(i) * 0.61803398874989484820458683436564);
                return float2(E1, E2);
            }

            /**
             * 在半球内生成采样方向
             * 使用余弦加权的重要性采样
             * 
             * @param Xi Hammersley序列生成的随机数
             * @param N 半球中心法线
             * @return 采样方向（世界空间）
             */
            float3 SampleHemisphere(float2 Xi, float3 N)
            {
                // 球面坐标转笛卡尔坐标
                float phi = 2.0 * PI * Xi.x;
                float cosTheta = sqrt(1.0 - Xi.y);  // 余弦加权
                float sinTheta = sqrt(Xi.y);

                float3 H;
                H.x = cos(phi) * sinTheta;
                H.y = sin(phi) * sinTheta;
                H.z = cosTheta;

                // 构建切线空间基
                float3 up = abs(N.z) < 0.999 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
                float3 tangent = normalize(cross(up, N));
                float3 bitangent = cross(N, tangent);

                // 从切线空间转换到世界空间
                float3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
                return normalize(sampleVec);
            }

            // ============================================================================
            // 片段着色器 - 漫反射辐照度卷积核心
            // ============================================================================
            
            /**
             * 片段着色器
             * 
             * 计算流程：
             * 1. 根据UV和面索引获取法线方向
             * 2. 构建切线空间基
             * 3. 使用Hammersley序列在半球内采样
             * 4. 对每个采样方向从环境贴图采样并累加
             * 5. 归一化得到最终辐照度
             */
            float4 frag(v2f i) : SV_Target
            {
                // Step 1: 获取当前像素对应的法线方向
                float3 N = GetCubemapDirection(i.uv, _FaceIndex);
                
                // Step 2: 初始化辐照度累加器
                float3 irradiance = float3(0.0, 0.0, 0.0);

                // Step 3: 构建切线空间基
                float3 up = float3(0.0, 1.0, 0.0);
                float3 right = normalize(cross(up, N));
                up = cross(N, right);

                // Step 4: 蒙特卡洛积分 - 在半球内采样
                const uint SAMPLE_COUNT = 1024u;
                for (uint j = 0u; j < SAMPLE_COUNT; ++j)
                {
                    // 4.1 生成低差异采样点
                    float2 Xi = HammersleyNoBitOps(j, SAMPLE_COUNT);
                    
                    // 4.2 转换为半球方向
                    float3 sampleDir = SampleHemisphere(Xi, N);

                    // 4.3 从切线空间转换到世界空间
                    float3 L = normalize(right * sampleDir.x + up * sampleDir.y + N * sampleDir.z);

                    // 4.4 采样环境贴图并累加
                    float NdotL = max(dot(N, L), 0.0);
                    irradiance += SAMPLE_TEXTURECUBE(_MainTex, sampler_MainTex, L).rgb * NdotL;
                }

                // Step 5: 归一化（除以采样数，乘以PI）
                irradiance = PI * irradiance / float(SAMPLE_COUNT);

                return float4(irradiance, 1.0);
            }
            ENDHLSL
        }
    }
}
