// SSR Shader - Screen Space Reflections implementation
// 包含三个Pass：线性光线追踪、合成和HiZ光线追踪
Shader "Hidden/SSR_Shader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} // 主纹理，用于采样屏幕颜色
    }

    SubShader
    {
        // 关闭背面剔除、深度写入和深度测试
        Cull Off ZWrite Off ZTest Never

        // 0 - Linear SSR Pass：使用线性光线追踪方法
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            // 包含必要的HLSL库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Depth.hlsl" // ✅ 加上这一行

            #include "NormalSample.hlsl"
            #include "Common.hlsl"

            // 顶点输入结构体
            struct appdata
            {
                float4 vertex : POSITION; // 顶点位置
                float2 uv : TEXCOORD0;    // 纹理坐标
            };

            // 顶点输出结构体
            struct v2f
            {
                float2 uv : TEXCOORD0;    // 纹理坐标
                float4 vertex : SV_POSITION; // 裁剪空间位置
            };

            // 顶点着色器：简单的全屏四边形变换
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex); // 物体空间到裁剪空间的变换
                o.uv = v.uv; // 传递纹理坐标
                return o;
            }

            // 声明纹理和采样器
            TEXTURE2D_X(_CameraDepthTexture); // 相机深度纹理
            TEXTURE2D_X(_MainTex);            // 主屏幕纹理
            TEXTURE2D_X(_GBuffer2);           // GBuffer2，存储法线和光滑度
            TEXTURE2D_X(_GBuffer1); // 金属度、光滑度


            // 采样器状态
            //SamplerState sampler_CameraDepthTexture;
            //SamplerState sampler_MainTex;
            //SamplerState sampler_GBuffer2;
            //SamplerState sampler_GBuffer1;
            SAMPLER(sampler_CameraDepthTexture);
            SAMPLER(sampler_MainTex);
            SAMPLER(sampler_GBuffer2);
            SAMPLER(sampler_GBuffer1);

            // 全局变量
            float3 _WorldSpaceViewDir; // 世界空间相机方向
            float _RenderScale;        // 渲染缩放
            float stride;              // 光线步进步长
            float numSteps;            // 最大光线追踪步数
            float minSmoothness;       // 光滑度阈值
            int iteration;             // 迭代次数


            // 片段着色器：执行线性光线追踪
            half3 frag(v2f i) : SV_Target
            {
                // 1. 获取深度和材质信息
                float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;
                
                // 背景区域（天空盒）直接返回黑色，不进行反射
                if (rawDepth == 0) {
                    return half3(0, 0, 0);
                }
                
                float4 gbuff = SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, i.uv);
                //float4 gbuff1 = SAMPLE_TEXTURE2D_X(_GBuffer1, sampler_GBuffer1, i.uv);
                float smoothness = gbuff.a; // 从GBuffer2获取光滑度
                float stepS = smoothstep(minSmoothness, 1, smoothness); // 计算光滑度因子
                float3 normal = UnpackNormal(gbuff.xyz); // 解码法线

                // 2. 坐标转换：从屏幕空间到世界空间
                float4 clipSpace = float4(i.uv * 2 - 1, rawDepth, 1); // 屏幕空间坐标（-1到1）                 
              
                //float4 viewSpacePosition = mul(_InverseProjectionMatrix, clipSpace); // 裁剪空间到视图空间
                //float4 viewSpacePosition = mul(UNITY_MATRIX_I_P, clipSpace); // 裁剪空间到视图空间
                float4 viewSpacePosition = mul(_MyInverseProjectionMatrix, clipSpace); // 裁剪空间到视图空间
                viewSpacePosition /= viewSpacePosition.w; // 透视除法

                //float4 worldSpacePosition = mul(_InverseViewMatrix, viewSpacePosition); // 视图空间到世界空间
                //float4 worldSpacePosition = mul(UNITY_MATRIX_I_V, viewSpacePosition); // 视图空间到世界空间
                float4 worldSpacePosition = mul(_MyInverseViewMatrix, viewSpacePosition); // 视图空间到世界空间
                float3 viewDir = normalize(float3(worldSpacePosition.xyz - _WorldSpaceCameraPos));             
                float3 reflectionRay = reflect(viewDir, normal); // 计算反射光线方向

                // 3. 转换反射光线到视图空间
                //float3 reflectionRay_v = mul(GetWorldToViewMatrix(), float4(reflectionRay, 0)); // 世界空间到视图空间
                 float3 reflectionRay_v = mul(_MyViewMatrix, float4(reflectionRay, 0)); // 世界空间到视图空间
              
                // 4. 计算光线追踪参数
                float viewReflectDot = saturate(dot(viewDir, reflectionRay));
                float cameraViewReflectDot = saturate(dot(_WorldSpaceViewDir, reflectionRay));

                // 5. 根据视角调整步长和厚度（直接修改stride，与参考实现一致）
                float thickness = stride * 2;
                float oneMinusViewReflectDot = sqrt(1 - viewReflectDot);
                stride /= oneMinusViewReflectDot; // 视角越陡，步长越小
                thickness /= oneMinusViewReflectDot;

                // 6. 初始化光线追踪变量
                int hit = 0; // 是否命中
                float maskOut = 1; // 边缘遮罩
                float3 currentPosition = viewSpacePosition.xyz; // 当前光线位置
                float2 currentScreenSpacePosition = i.uv; // 当前屏幕空间位置

                // 7. 检查是否需要光线追踪（只有光滑度足够高才进行）
                bool doRayMarch = smoothness > minSmoothness;
                                
                // 8. 计算最大光线长度
                float maxRayLength = numSteps * stride;
                float maxDist = lerp(min(viewSpacePosition.z * -1, maxRayLength), maxRayLength, cameraViewReflectDot);
                float numSteps_f = maxDist / stride;
                numSteps = max(numSteps_f, 0);

                //return half3(maxDist, stride, numSteps);
                // 9. 执行光线追踪
                if (doRayMarch) {
                    float3 ray = reflectionRay_v * stride; // 光线方向和步长
                    float depthDelta = 0;


                    // 9.1 线性光线步进
                    [loop]
                    for (int step = 0; step < numSteps; step++)
                    {
                        //float4 uv = mul(_MyProjectionMatrix, float4(currentPosition.x, currentPosition.y, currentPosition.z, 1));
                        //return half3(uv.xy/ uv.w, uv.w);

                        currentPosition += ray; // 沿着反射光线移动


                        //return half3(reflectionRay_v.rgb);

                        // 9.2 将当前位置转换为屏幕空间坐标
                        //float4 uv = mul(GetViewToHClipMatrix(), float4(currentPosition.x, currentPosition.y, currentPosition.z, 1));
                        float4 uv = mul(_MyProjectionMatrix, float4(currentPosition.x, currentPosition.y, currentPosition.z, 1));

                    
                        uv /= uv.w; // 透视除法
                        uv.x *= 0.5f;
                        uv.y *= 0.5f;
                        uv.x += 0.5f;
                        uv.y += 0.5f;
                        
                       


                        // 9.3 检查是否超出屏幕边界
                        if (uv.x >= 1 || uv.x < 0 || uv.y >= 1 || uv.y < 0) {
                            //return half3(1, 1, 1);
                            break;
                        }
                        

                        // 9.4 采样深度纹理
                        float sampledDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv.xy).r;

                       //return half3(step, numSteps, thickness);

                        // 9.5 比较当前位置深度与采样深度
                        if (abs(rawDepth - sampledDepth) > 0 && sampledDepth != 0) {
                            
                            float samEyeDepth = LinearEyeDepth(sampledDepth, _ZBufferParams);
                            //depthDelta = currentPosition.z - LinearEyeDepth(sampledDepth, _ZBufferParams);
                            depthDelta = currentPosition.z * (-1) - samEyeDepth;


                            //if( depthDelta > 0)
                            //{
                            //    return half3(1, 1, 1);
                            //}
                            //return half3(currentPosition.z, samEyeDepth, step);  

                            // 9.6 检查是否命中物体
                            if (depthDelta > 0 && depthDelta < stride * 2) {
                                
                                currentScreenSpacePosition = uv.xy; // 记录命中位置
                                hit = 1; // 标记命中
                                break;
                            }
                        }

                        //return half3(rawDepth, sampledDepth, step);
                    }

                    // 9.7 检查深度差是否在合理范围内
                    if (depthDelta > thickness) {
                        hit = 0;
                    }
                          
          
                    // 9.8 二分法精确查找命中点
                    #define binaryStepCount 16
                    int binarySearchSteps = binaryStepCount * hit;
                //return half3(hit, 0, 0);

                    [loop]
                    //for (int j = 0; j < binarySearchSteps; j++)
                    for (int j = 0; j < binaryStepCount; j++)
                    {
                        ray *= .5f; // 步长减半
                        if (depthDelta > 0) {
                            currentPosition -= ray; // 向相机方向移动
                        }
                        else if (depthDelta < 0) {
                            currentPosition += ray; // 远离相机方向移动
                        }
                        else {
                            break;
                        }

                        // 9.9 重新计算屏幕空间坐标
                        //float4 uv = mul(GetViewToHClipMatrix(), float4(currentPosition.x, currentPosition.y * -1, currentPosition.z * -1, 1));
                        //float4 uv = mul(GetViewToHClipMatrix(), float4(currentPosition.x, currentPosition.y, currentPosition.z * -1, 1));
                        float4 uv = mul(_MyProjectionMatrix, float4(currentPosition.x, currentPosition.y, currentPosition.z, 1));

                        uv /= uv.w;
                        maskOut = ScreenEdgeMask(uv); // 计算边缘遮罩
                        uv.x *= 0.5f;
                        uv.y *= 0.5f;
                        uv.x += 0.5f;
                        uv.y += 0.5f;
                        currentScreenSpacePosition = uv;

                        //return half3(uv.xy, maskOut);

                        // 9.10 重新采样深度并计算深度差
                        float sd = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv.xy).r;
                        depthDelta = currentPosition.z * -1 - LinearEyeDepth(sd, _ZBufferParams);
                        float minv = 1 / max((oneMinusViewReflectDot * float(j)), 0.001);
                        if (abs(depthDelta) > minv) {
                            hit = 0; // 深度差过大，认为未命中
                            break;
                        }
                    }
                    //return half3(hit, 0,  0);
                    // 9.11 检查是否命中物体背面
                    float3 currentNormal = UnpackNormal(SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, currentScreenSpacePosition).xyz);
                    float backFaceDot = dot(currentNormal, reflectionRay);
                    if (backFaceDot > 0) {
                        hit = 0; // 命中背面，认为未命中
                    }
                }

                //return half3(hit, 0, 0);

                // 10. 计算光线追踪进度
                float3 deltaDir = viewSpacePosition.xyz - currentPosition;
                float progress = dot(deltaDir, deltaDir) / (maxDist * maxDist);
                progress = smoothstep(0, .5, 1 - progress); // 平滑处理

                // 11. 应用遮罩
                maskOut *= hit;
                
                // 12. 返回反射信息：屏幕空间坐标和反射强度
                return half3(currentScreenSpacePosition, maskOut * progress);
            }
            ENDHLSL
        }

        // 1 - Composite Pass：合成反射结果
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            // 包含必要的HLSL库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NormalSample.hlsl"
            #include "Common.hlsl"

            // 顶点输入结构体
            struct appdata
            {
                float4 vertex : POSITION; // 顶点位置
                float2 uv : TEXCOORD0;    // 纹理坐标
            };

            // 顶点输出结构体
            struct v2f
            {
                float2 uv : TEXCOORD0;    // 纹理坐标
                float4 vertex : SV_POSITION; // 裁剪空间位置
            };

            // 顶点着色器：简单的全屏四边形变换
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex); // 物体空间到裁剪空间的变换
                o.uv = v.uv; // 传递纹理坐标
                return o;
            }

            // 全局变量
            float _RenderScale;    // 渲染缩放
            float minSmoothness;   // 光滑度阈值

            // 声明纹理和采样器
            TEXTURE2D_X(_GBuffer1);            // GBuffer1，存储金属度和AO
            TEXTURE2D_X(_ReflectedColorMap);   // 反射颜色映射，包含反射UV坐标
            TEXTURE2D_X(_MainTex);             // 主屏幕纹理
            TEXTURE2D_X(_GBuffer2);            // GBuffer2，存储法线和光滑度
            TEXTURE2D_X(_GBuffer0);             // GBuffer0，存储漫反射颜色
            TEXTURE2D_X(_CameraDepthTexture);   // 相机深度纹理

            // 采样器状态
            SamplerState sampler_GBuffer1;
            SamplerState sampler_ReflectedColorMap;
            SamplerState sampler_MainTex;
            SamplerState sampler_GBuffer2;
            SamplerState sampler_GBuffer0;
            SamplerState sampler_CameraDepthTexture;

            // 片段着色器：合成反射结果
            float4 frag(v2f i) : SV_Target
            {
                // 1. 调整填充比例
                _PaddedScale = 1 / _PaddedScale;
                
                // 2. 采样主纹理
                float4 maint = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, i.uv * _PaddedScale);
                
                // 3. 采样深度纹理
                float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;
                if (rawDepth == 0) {
                    return maint; // 背景区域直接返回主纹理颜色
                }
                
                // 4. 计算世界空间位置
                float3 worldSpacePosition = getWorldPosition(rawDepth, i.uv);
                float3 viewDir = normalize(float3(worldSpacePosition.xyz) - _WorldSpaceCameraPos);

                // 5. 获取法线和光滑度
                float4 normal = SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, i.uv);
                normal.xyz = UnpackNormal(normal.xyz); // 解码法线
                float stepS = smoothstep(minSmoothness, 1, normal.w); // 计算光滑度因子
                float fresnal = 1 - dot(viewDir, -normal); // 计算菲涅尔效应
                
                // 6. 转换法线到裁剪空间
                normal.xyz = mul(GetWorldToViewMatrix(), float4(normal.xyz, 0));
                normal.xyz = mul(GetViewToHClipMatrix(), float4(normal.xyz, 0));
                normal.y *= -1;

                // 7. 计算抖动值
                float dither;
                float type;
                if (_DitherMode == 0) {
                    dither = Dither8x8(i.uv.xy * _RenderScale, .5); // 8x8抖动
                    type = 0;
                }
                else {
                    dither = IGN(i.uv.x * _ScreenParams.x * _RenderScale, i.uv.y * _ScreenParams.y * _RenderScale, _Frame); // 交错梯度噪声
                    type = 0;
                }
                dither *= 2;
                dither -= 1;

                // 8. 计算UV偏移
                float stepSSqrd = pow(stepS, 2);
                const float2 uvOffset = normal * lerp(dither * 0.05f, 0, stepSSqrd);
                
                // 9. 采样反射颜色映射
                float3 reflectedUv = SAMPLE_TEXTURE2D_X(_ReflectedColorMap, sampler_ReflectedColorMap, (i.uv + uvOffset * type) * _PaddedScale);
                float maskVal = saturate(reflectedUv.z) * stepS; // 反射强度
                reflectedUv.xy += uvOffset * (1 - type); // 应用UV偏移

                // 10. 计算亮度遮罩
                float lumin = saturate(RGB2Lum(maint) - 1);
                float luminMask = 1 - lumin;
                luminMask = pow(luminMask, 5); // 增强暗部反射

                // 11. 采样材质属性
                float2 gb1 = SAMPLE_TEXTURE2D_X(_GBuffer1, sampler_GBuffer1, i.uv.xy).ra; // 金属度和AO
                float4 specularColor = float4(SAMPLE_TEXTURE2D_X(_GBuffer0, sampler_GBuffer0, i.uv.xy).rgb, 1); // 漫反射颜色

                // 12. 调整菲涅尔效应
                float fresnalMask = 1 - saturate(RGB2Lum(specularColor));
                fresnalMask = lerp(1, fresnalMask, gb1.x);
                fresnal = lerp(1, fresnal * fresnal, fresnalMask);

                // 13. 材质参数
                const float lMet = 0.3f;   // 低金属度
                const float hMet = 1.0f;   // 高金属度
                const float lSpecCol = 0.0; // 低金属度时的 specular 颜色混合因子
                const float hSpecCol = 0.6f; // 高金属度时的 specular 颜色混合因子

                // 14. 模糊参数
                const float blurL = 0.0f;   // 低粗糙度时的模糊量
                const float blurH = 5.0f;   // 高粗糙度时的模糊量
                const float blurPow = 4;     // 模糊强度指数

                // 15. 调整 specular 颜色
                specularColor.xyz = lerp(float3(1, 1, 1), specularColor.xyz, lerp(lSpecCol, hSpecCol, gb1.x));

                // 16. 计算混合因子
                float fm = clamp(gb1.x, lMet, hMet); // 金属度因子
                float ff = 1 - fm; // 非金属度因子
                
                // 17. 根据粗糙度计算模糊量
                float roughnessBlurAmount = lerp(blurL, blurH, 1 - pow(stepS, blurPow));
                
                // 18. 采样反射纹理（带模糊）
                float4 reflectedTexture = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_MainTex, reflectedUv.xy, roughnessBlurAmount);

                // 19. 计算反射权重
                float ao = gb1.y; // 环境遮挡
                float refw = maskVal * ao * fresnal * luminMask * 2.0f; // 增加反射强度
                
                // 20. 混合颜色
                float4 blendedColor = maint * ff + (reflectedTexture * specularColor) * fm;

                // 21. 根据反射权重混合原始颜色和反射颜色
                float4 res = lerp(maint, blendedColor, refw);

                // 22. 返回最终颜色
                return res;
            }
            ENDHLSL
        }

        //// 2 - HiZ SSR Pass：使用深度金字塔加速的光线追踪
        //Pass
        //{
        //    HLSLPROGRAM
        //    #pragma vertex vert
        //    #pragma fragment frag
        //    #pragma enable_d3d11_debug_symbols
        //    #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            
        //    // HiZ参数定义
        //    #define HIZ_START_LEVEL 0     // 起始金字塔层级
        //    #define HIZ_MAX_LEVEL 10      // 最大金字塔层级
        //    #define HIZ_STOP_LEVEL 0       // 停止金字塔层级

        //    // 包含必要的HLSL库
        //    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        //    #include "NormalSample.hlsl"
        //    #include "Common.hlsl"

        //    // 顶点输入结构体
        //    struct appdata
        //    {
        //        float4 vertex : POSITION; // 顶点位置
        //        float2 uv : TEXCOORD0;    // 纹理坐标
        //    };

        //    // 顶点输出结构体
        //    struct v2f
        //    {
        //        float2 uv : TEXCOORD0;    // 纹理坐标
        //        float4 vertex : SV_POSITION; // 裁剪空间位置
        //    };

        //    // 顶点着色器：简单的全屏四边形变换
        //    v2f vert(appdata v)
        //    {
        //        v2f o;
        //        o.vertex = TransformObjectToHClip(v.vertex); // 物体空间到裁剪空间的变换
        //        o.uv = v.uv; // 传递纹理坐标
        //        return o;
        //    }

        //    // 声明纹理和采样器
        //    TEXTURE2D_X(_GBuffer2);            // GBuffer2，存储法线和光滑度
        //    TEXTURE2D_X(_MainTex);             // 主屏幕纹理
        //    TEXTURE2D_X(_CameraDepthTexture);   // 相机深度纹理

        //    // 全局变量
        //    float3 _WorldSpaceViewDir; // 世界空间相机方向
        //    float _RenderScale;        // 渲染缩放
        //    float numSteps;            // 最大光线追踪步数
        //    float minSmoothness;       // 光滑度阈值
        //    int iteration;             // 迭代次数
        //    int reflectSky;            // 是否反射天空
        //    float2 crossEpsilon;       // 交叉epsilon值
        //    float stride;              // 光线步进步长

        //    // 采样器状态
        //    SamplerState sampler_GBuffer2;
        //    SamplerState sampler_MainTex;
        //    SamplerState sampler_CameraDepthTexture;

        //    // 片段着色器：执行HiZ光线追踪
        //    float4 frag(v2f i) : SV_Target
        //    {
        //        // 1. 获取深度和材质信息
        //        float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;
                
        //        // 背景区域（天空盒）直接返回黑色，不进行反射
        //        if (rawDepth == 0) {
        //            return float4(0, 0, 0, 1);
        //        }
                
        //        float4 gbuff = SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, i.uv);
        //        float smoothness = gbuff.w; // 从GBuffer2获取光滑度
        //        float stepS = smoothstep(minSmoothness, 1, smoothness); // 计算光滑度因子
        //        float3 normal = UnpackNormal(gbuff.xyz); // 解码法线

        //        // 2. 坐标转换：从屏幕空间到世界空间
        //        float4 clipSpace = float4(i.uv * 2 - 1, rawDepth, 1); // 屏幕空间坐标（-1到1）
        //        float4 viewSpacePosition = mul(_InverseProjectionMatrix, clipSpace); // 裁剪空间到视图空间
        //        viewSpacePosition /= viewSpacePosition.w; // 透视除法
        //        viewSpacePosition.y *= -1; // 翻转Y轴（关键！）
        //        float4 worldSpacePosition = mul(_InverseViewMatrix, viewSpacePosition); // 视图空间到世界空间
        //        float3 viewDir = normalize(float3(worldSpacePosition.xyz) - _WorldSpaceCameraPos); // 视图方向（点到相机）
        //        float3 reflectionRay = reflect(viewDir, normal); // 计算反射光线方向

        //        // 3. 转换反射光线到视图空间
        //        float3 reflectionRay_v = mul(GetWorldToViewMatrix(), float4(reflectionRay, 0)); // 世界空间到视图空间
        //        reflectionRay_v.z *= -1; // 翻转Z轴
        //        viewSpacePosition.z *= -1; // 翻转Z轴（正值表示距离）

        //        // 4. 计算光线追踪参数
        //        float viewReflectDot = saturate(dot(viewDir, reflectionRay));
        //        float cameraViewReflectDot = saturate(dot(_WorldSpaceViewDir, reflectionRay));

        //        // 5. 根据视角调整步长和厚度
        //        float thickness = stride * 2;
        //        float oneMinusViewReflectDot = sqrt(1 - viewReflectDot);
        //        stride /= oneMinusViewReflectDot; // 视角越陡，步长越小
        //        thickness /= oneMinusViewReflectDot;

        //        // 6. 初始化光线追踪变量
        //        int hit = 0; // 是否命中
        //        float maskOut = 1; // 边缘遮罩
        //        float3 currentPosition = viewSpacePosition.xyz; // 当前光线位置
        //        float2 currentScreenSpacePosition = i.uv; // 当前屏幕空间位置

        //        // 7. 检查是否需要光线追踪（只有光滑度足够高才进行）
        //        bool doRayMarch = smoothness > minSmoothness;

        //        // 8. 计算最大光线长度
        //        float maxRayLength = numSteps * stride;
        //        float maxDist = lerp(min(viewSpacePosition.z, maxRayLength), maxRayLength, cameraViewReflectDot);

        //        // 9. 执行HiZ光线追踪
        //        if (doRayMarch) {
        //            float3 ray = reflectionRay_v * stride; // 光线方向和步长
        //            float depthDelta = 0;

        //            // 9.1 多分辨率层级光线追踪
        //            [unroll(10)] for (int level = HIZ_START_LEVEL; level < HIZ_MAX_LEVEL; level++) {
        //                float stepSize = 1.0f / pow(2, level); // 层级越大，步长越大
                        
        //                // 9.2 在当前层级执行光线步进
        //                [unroll(64)] for (int step = 0; step < numSteps; step++) {
        //                    currentPosition += ray * stepSize; // 沿着反射光线移动
                            
        //                    // 9.3 将当前位置转换为屏幕空间坐标
        //                    float4 uvProj = mul(GetViewToHClipMatrix(), float4(currentPosition.x, currentPosition.y * -1, currentPosition.z * -1, 1));
        //                    uvProj /= uvProj.w; // 透视除法
        //                    uvProj.x *= 0.5f;
        //                    uvProj.y *= 0.5f;
        //                    uvProj.x += 0.5f;
        //                    uvProj.y += 0.5f;
                            
        //                    // 9.4 检查是否超出屏幕边界
        //                    if (uvProj.x >= 1 || uvProj.x < 0 || uvProj.y >= 1 || uvProj.y < 0) {
        //                        break;
        //                    }
                            
        //                    // 9.5 采样深度纹理
        //                    float sampledDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uvProj.xy).r;
                            
        //                    // 9.6 比较当前位置深度与采样深度
        //                    if (abs(rawDepth - sampledDepth) > 0 && sampledDepth != 0) {
        //                        float linearDepth = LinearEyeDepth(sampledDepth); // 转换为线性深度
        //                        depthDelta = currentPosition.z - linearDepth; // 计算深度差
                                
        //                        // 9.7 检查是否命中物体
        //                        if (depthDelta > 0 && depthDelta < stride * 2) {
        //                            currentScreenSpacePosition = uvProj.xy; // 记录命中位置
        //                            hit = 1; // 标记命中
        //                            break;
        //                        }
        //                    }
        //                }
                        
        //                // 9.8 如果命中，停止层级遍历
        //                if (hit > 0) break;
        //            }

        //            // 9.9 检查深度差是否在合理范围内
        //            if (depthDelta > thickness) {
        //                hit = 0;
        //            }

        //            // 9.10 二分法精确查找命中点
        //            #define binaryStepCount 16
        //            int binarySearchSteps = binaryStepCount * hit;
        //            for (int i = 0; i < binaryStepCount; i++) {
        //                ray *= .5f; // 步长减半
        //                if (depthDelta > 0) {
        //                    currentPosition -= ray; // 向相机方向移动
        //                }
        //                else if (depthDelta < 0) {
        //                    currentPosition += ray; // 远离相机方向移动
        //                }
        //                else {
        //                    break;
        //                }

        //                // 9.11 重新计算屏幕空间坐标
        //                float4 uv = mul(GetViewToHClipMatrix(), float4(currentPosition.x, currentPosition.y * -1, currentPosition.z * -1, 1));
        //                uv /= uv.w;
        //                maskOut = ScreenEdgeMask(uv); // 计算边缘遮罩
        //                uv.x *= 0.5f;
        //                uv.y *= 0.5f;
        //                uv.x += 0.5f;
        //                uv.y += 0.5f;
        //                currentScreenSpacePosition = uv;

        //                // 9.12 重新采样深度并计算深度差
        //                float sd = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv.xy).r;
        //                depthDelta = currentPosition.z - LinearEyeDepth(sd);
        //                float minv = 1 / max((oneMinusViewReflectDot * float(i)), 0.001);
        //                if (abs(depthDelta) > minv) {
        //                    hit = 0; // 深度差过大，认为未命中
        //                    break;
        //                }
        //            }

        //            // 9.13 检查是否命中物体背面
        //            float3 currentNormal = UnpackNormal(SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, currentScreenSpacePosition).xyz);
        //            float backFaceDot = dot(currentNormal, reflectionRay);
        //            if (backFaceDot > 0) {
        //                hit = 0; // 命中背面，认为未命中
        //            }
        //        }

        //        // 10. 计算光线追踪进度
        //        float3 deltaDir = viewSpacePosition.xyz - currentPosition;
        //        float progress = dot(deltaDir, deltaDir) / (maxDist * maxDist);
        //        progress = smoothstep(0, .5, 1 - progress); // 平滑处理

        //        // 11. 应用遮罩
        //        maskOut *= hit;
                
        //        // 12. 返回反射信息：屏幕空间坐标和反射强度
        //        return float4(currentScreenSpacePosition, maskOut * progress, 1.0);
        //    }
        //    ENDHLSL
        //}
    }
}
