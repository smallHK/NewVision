/**
 * EquirectangularToCubemap.shader
 * 等距圆柱投影转Cubemap着色器
 * 
 * 功能：
 * - 将等距圆柱投影(Equirectangular)的HDR纹理转换为Cubemap
 * - 支持HDR环境贴图的格式转换
 * 
 * 原理：
 * - 等距圆柱投影：将球面映射到矩形，经度对应X轴，纬度对应Y轴
 * - Cubemap：将球面映射到6个正方形面
 * - 转换过程：遍历Cubemap每个像素，计算对应方向，采样等距圆柱投影纹理
 * 
 * 等距圆柱投影UV计算：
 * - u = longitude / (2π) + 0.5 = atan2(x, z) / (2π) + 0.5
 * - v = latitude / π + 0.5 = asin(y) / π + 0.5
 */
Shader "Hidden/NewVision/IBL/EquirectangularToCubemap"
{
    Properties
    {
        /// <summary>等距圆柱投影HDR纹理</summary>
        _MainTex ("Texture", 2D) = "white" {}
        
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
            Name "EquirectangularToCubemap"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float2 uv : TEXCOORD0;          // 纹理坐标 [0,1]
                float4 vertex : SV_POSITION;    // 裁剪空间位置
            };

            // ============================================================================
            // 纹理和采样器声明
            // ============================================================================
            TEXTURE2D(_MainTex);
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
            float3 GetDirectionForFace(int face, float2 uv)
            {
                float2 st = uv * 2.0 - 1.0;

                float3 dir = float3(0, 0, 0);

                if (face == 0)
                {
                    // +X面：x=1, y=-st.y, z=-st.x
                    dir = float3(1.0, -st.y, -st.x);
                }
                else if (face == 1)
                {
                    // -X面：x=-1, y=-st.y, z=st.x
                    dir = float3(-1.0, -st.y, st.x);
                }
                else if (face == 2)
                {
                    // +Y面：x=st.x, y=1, z=st.y
                    dir = float3(st.x, 1.0, st.y);
                }
                else if (face == 3)
                {
                    // -Y面：x=st.x, y=-1, z=-st.y
                    dir = float3(st.x, -1.0, -st.y);
                }
                else if (face == 4)
                {
                    // +Z面：x=st.x, y=-st.y, z=1
                    dir = float3(st.x, -st.y, 1.0);
                }
                else
                {
                    // -Z面：x=-st.x, y=-st.y, z=-1
                    dir = float3(-st.x, -st.y, -1.0);
                }

                return normalize(dir);
            }

            /**
             * 将3D方向向量转换为等距圆柱投影UV坐标
             * 
             * 计算公式：
             * - longitude = atan2(x, z)
             * - latitude = asin(y)
             * - u = longitude / (2π) + 0.5
             * - v = latitude / π + 0.5
             * 
             * @param dir 归一化的方向向量
             * @return UV坐标 [0,1]x[0,1]
             */
            float2 DirectionToEquirectangular(float3 dir)
            {
                // 计算经度（方位角）
                float longitude = atan2(dir.x, dir.z);
                
                // 计算纬度（仰角）
                float latitude = asin(dir.y);

                // 转换到UV空间 [0,1]
                float2 uv;
                uv.x = (longitude / PI) * 0.5 + 0.5;
                uv.y = (latitude / (PI * 0.5)) * 0.5 + 0.5;

                return uv;
            }

            // ============================================================================
            // 片段着色器 - 等距圆柱投影转Cubemap核心
            // ============================================================================
            
            /**
             * 片段着色器
             * 
             * 计算流程：
             * 1. 根据UV和面索引计算当前像素对应的方向向量
             * 2. 将方向向量转换为等距圆柱投影UV坐标
             * 3. 采样HDR纹理
             * 
             * 输入：
             * - uv: 当前Cubemap面的纹理坐标 [0,1]
             * - _FaceIndex: 当前渲染的Cubemap面索引
             * 
             * 输出：
             * - 对应方向的HDR颜色
             */
            float4 frag(v2f i) : SV_Target
            {
                // Step 1: 计算当前像素对应的方向向量
                float3 dir = GetDirectionForFace(_FaceIndex, i.uv);
                
                // Step 2: 将方向向量转换为等距圆柱投影UV坐标
                float2 equiUV = DirectionToEquirectangular(dir);

                // Step 3: 采样HDR纹理
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, equiUV);
                
                return color;
            }
            ENDHLSL
        }
    }
}
