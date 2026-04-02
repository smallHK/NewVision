/**
 * LTC区域光渲染通道Shader
 * 
 * 功能说明：
 * 使用LTC（Linearly Transformed Cosines）算法渲染区域光（矩形光源）
 * 
 * 渲染流程：
 * 1. 顶点着色器(vert): 将顶点变换到裁剪空间，传递UV坐标
 * 2. 片元着色器(frag):
 *    a. 从深度纹理重建世界坐标
 *    b. 从法线纹理获取表面法线
 *    c. 遍历所有区域光，计算漫反射和高光贡献
 *    d. 将光照结果叠加到颜色缓冲
 * 
 * 技术要点：
 * - 使用屏幕空间后处理方式进行渲染
 * - 通过深度纹理重建世界坐标
 * - 使用GBuffer法线进行光照计算
 */
Shader "Hidden/LTCAreaLightPass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        
        // 渲染状态设置
        Cull Off    // 关闭背面剔除，全屏四边形需要
        ZWrite Off  // 关闭深度写入，后处理不需要
        ZTest Always // 始终通过深度测试

        Pass
        {
            // Pass名称，用于Frame Debugger显示
            Name "LTCAreaLightPass"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // URP核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // LTC区域光相关头文件
            #include "LTCAreaLight.hlsl"  // 区域光数据结构和访问函数
            #include "LTCCore.hlsl"       // LTC核心算法实现

            // ==================== 顶点着色器输入结构 ====================
            struct Attributes
            {
                float4 positionOS   : POSITION;  // 物体空间位置
                float2 uv           : TEXCOORD0; // UV坐标
            };

            // ==================== 顶点着色器输出/片元着色器输入结构 ====================
            struct Varyings
            {
                float4 positionHCS  : SV_POSITION; // 齐次裁剪空间位置
                float2 uv           : TEXCOORD0;   // UV坐标
            };

            // ==================== 纹理声明 ====================
            // 主纹理（颜色缓冲）
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            // 相机深度纹理
            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            
            // 相机法线纹理（来自GBuffer）
            TEXTURE2D(_CameraNormalsTexture);
            SAMPLER(sampler_CameraNormalsTexture);

            // ==================== 相机参数（由C#传递） ====================
            // 相机视锥体左上角世界坐标
            float4 _CameraTopLeftCorner;
            // 相机视锥体X方向范围
            float4 _CameraXExtent;
            // 相机视锥体Y方向范围
            float4 _CameraYExtent;

            // ==================== LTC查找表纹理 ====================
            // 漫反射变换逆矩阵LUT
            TEXTURE2D(_TransformInv_Diffuse);
            SAMPLER(sampler_TransformInv_Diffuse);
            // 高光变换逆矩阵LUT
            TEXTURE2D(_TransformInv_Specular);
            SAMPLER(sampler_TransformInv_Specular);
            // 菲涅尔LUT
            TEXTURE2D(_AmpDiffAmpSpecFresnel);
            SAMPLER(sampler_AmpDiffAmpSpecFresnel);

            /**
             * 从深度纹理重建世界坐标
             * 
             * 原理：
             * 通过相机视锥体角点和UV坐标线性插值得到世界坐标
             * worldPos = topLeftCorner + uv.x * xExtent + uv.y * yExtent
             * 
             * @param uv 屏幕UV坐标
             * @param depth 深度值
             * @return 世界坐标位置
             */
            float3 GetWorldPositionFromDepth(float2 uv, float depth)
            {
                // 使用相机视锥体参数重建世界坐标
                float3 worldPos = _CameraTopLeftCorner.xyz + 
                                  uv.x * _CameraXExtent.xyz + 
                                  uv.y * _CameraYExtent.xyz;
                return worldPos;
            }
            
            /**
             * 从GBuffer法线纹理获取世界空间法线
             * 
             * 支持两种编码方式：
             * 1. Oct编码（_GBUFFER_NORMALS_OCT定义时）
             * 2. 标准编码（0-1范围存储）
             * 
             * @param uv 屏幕UV坐标
             * @return 世界空间法线向量（已归一化）
             */
            float3 GetNormalFromGBuffer(float2 uv)
            {
                float3 normal;
                #if defined(_GBUFFER_NORMALS_OCT)
                    // Oct编码方式：将两个8位值解码为法线
                    float2 octNormal = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv).xy * 2.0 - 1.0;
                    normal = normalize(UnpackNormalOctQuadEncode(octNormal));
                #else
                    // 标准编码方式：从0-1范围转换到-1到1范围
                    normal = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv).xyz * 2.0 - 1.0;
                    normal = normalize(normal);
                #endif
                return normal;
            }

            /**
             * 顶点着色器
             * 
             * 功能：将全屏四边形顶点变换到裁剪空间
             * 
             * @param input 顶点输入数据
             * @return 顶点输出数据
             */
            Varyings vert(Attributes input)
            {
                Varyings output;
                // 变换到齐次裁剪空间
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                // 传递UV坐标
                output.uv = input.uv;
                return output;
            }

            /**
             * 片元着色器
             * 
             * 渲染流程：
             * 1. 采样当前颜色缓冲
             * 2. 从深度纹理重建世界坐标
             * 3. 从法线纹理获取表面法线
             * 4. 遍历所有区域光，累加光照贡献
             * 5. 将光照结果叠加到颜色缓冲
             * 
             * @param input 片元输入数据
             * @return 最终颜色值
             */
            float4 frag(Varyings input) : SV_Target
            {
                // 步骤1: 采样当前颜色缓冲
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // 步骤2: 从深度纹理获取深度值
                float depth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, input.uv).r;
                
                // 处理反向Z（Unity使用反向Z时，1.0是近处，0.0是远处）
                #if UNITY_REVERSED_Z
                    depth = 1.0 - depth;
                #endif
                
                // 步骤3: 重建世界坐标和视图方向
                float3 worldPos = GetWorldPositionFromDepth(input.uv, depth);
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                
                // 步骤4: 获取表面法线
                float3 normal = GetNormalFromGBuffer(input.uv);
                
                // 步骤5: 初始化光照累加变量
                float3 diffuseLight = float3(0, 0, 0);
                float3 specularLight = float3(0, 0, 0);
                
                // 步骤6: 获取区域光数量
                int lightCount = GetAreaLightCount();
                
                // 步骤7: 遍历所有区域光，计算光照贡献
                for (int i = 0; i < lightCount; i++)
                {
                    // 获取区域光数据
                    AreaLight light = GetAreaLight(i);
                    float3 lightColor = GetAreaLightColor(i);
                    float lightIntensity = GetAreaLightIntensity(i);
                    
                    // 获取光源顶点位置
                    float4x4 lightVertices = light.vertices;
                    
                    // LTC变换矩阵（当前使用单位矩阵）
                    // TODO: 从LUT采样获取正确的变换矩阵
                    float3x3 Minv = float3x3(
                        1, 0, 0,
                        0, 1, 0,
                        0, 0, 1
                    );
                    
                    // 使用LTC算法计算光照
                    // 返回值：r=漫反射强度, g=高光强度
                    float4 ltcResult = LTC_Evaluate(normal, viewDir, worldPos, Minv, lightVertices);
                    
                    // 累加光照贡献
                    diffuseLight += lightColor * lightIntensity * ltcResult.r;
                    specularLight += lightColor * lightIntensity * ltcResult.g;
                }
                
                // 步骤8: 将光照结果叠加到颜色缓冲
                // 使用简单的混合权重
                color.rgb += diffuseLight * 0.5 + specularLight * 0.5;
                
                // 步骤9: 饱和化并返回最终颜色
                return saturate(color);
            }
            ENDHLSL
        }
    }
}
