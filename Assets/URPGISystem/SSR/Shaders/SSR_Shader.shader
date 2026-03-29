Shader "Hidden/SSR_Shader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Never

        // 0 - Linear SSR
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NormalSample.hlsl"
            #include "Common.hlsl"

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

            TEXTURE2D_X(_CameraDepthTexture);
            TEXTURE2D_X(_MainTex);
            TEXTURE2D_X(_GBuffer2);

            float3 _WorldSpaceViewDir;
            float _RenderScale;
            float stride;
            float numSteps;
            float minSmoothness;
            int iteration;

            SamplerState sampler_CameraDepthTexture;
            SamplerState sampler_MainTex;
            SamplerState sampler_GBuffer2;

            half3 frag(v2f i) : SV_Target
            {
                float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;
                float4 gbuff = SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, i.uv);
                float smoothness = gbuff.w;
                float stepS = smoothstep(minSmoothness, 1, smoothness);
                float3 normal = UnpackNormal(gbuff.xyz);

                float4 clipSpace = float4(i.uv * 2 - 1, rawDepth, 1);
                float4 viewSpacePosition = mul(_InverseProjectionMatrix, clipSpace);
                viewSpacePosition /= viewSpacePosition.w;
                viewSpacePosition.y *= -1;
                float4 worldSpacePosition = mul(_InverseViewMatrix, viewSpacePosition);
                float3 viewDir = normalize(float3(worldSpacePosition.xyz) - _WorldSpaceCameraPos);
                float3 reflectionRay = reflect(viewDir, normal);

                float3 reflectionRay_v = mul(_ViewMatrix, float4(reflectionRay,0));
                reflectionRay_v.z *= -1;
                viewSpacePosition.z *= -1;

                float viewReflectDot = saturate(dot(viewDir, reflectionRay));
                float cameraViewReflectDot = saturate(dot(_WorldSpaceViewDir, reflectionRay));

                float thickness = stride * 2;
                float oneMinusViewReflectDot = sqrt(1 - viewReflectDot);
                stride /= oneMinusViewReflectDot;
                thickness /= oneMinusViewReflectDot;

                int hit = 0;
                float maskOut = 1;
                float3 currentPosition = viewSpacePosition.xyz;
                float2 currentScreenSpacePosition = i.uv;

                bool doRayMarch = smoothness > minSmoothness;

                float maxRayLength = numSteps * stride;
                float maxDist = lerp(min(viewSpacePosition.z, maxRayLength), maxRayLength, cameraViewReflectDot);
                float numSteps_f = maxDist / stride;
                numSteps = max(numSteps_f, 0);

                if (doRayMarch) {
                    float3 ray = reflectionRay_v * stride;
                    float depthDelta = 0;
                    for (int step = 0; step < numSteps; step++)
                    {
                        currentPosition += ray;
                        float currentDepth;
                        float2 screenSpace;

                        float4 uv = mul(_ProjectionMatrix, float4(currentPosition.x, currentPosition.y * -1, currentPosition.z * -1, 1));
                        uv /= uv.w;
                        uv.x *= 0.5f;
                        uv.y *= 0.5f;
                        uv.x += 0.5f;
                        uv.y += 0.5f;
                        if (uv.x >= 1 || uv.x < 0 || uv.y >= 1 || uv.y < 0) {
                            break;
                        }
                        float sampledDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv.xy).r;
                        if (abs(rawDepth - sampledDepth) > 0 && sampledDepth != 0) {
                            depthDelta = currentPosition.z - LinearEyeDepth(sampledDepth);

                            if (depthDelta > 0 && depthDelta < stride * 2) {
                                currentScreenSpacePosition = uv.xy;
                                hit = 1;
                                break;
                            }
                        }
                    }

                    if (depthDelta > thickness) {
                        hit = 0;
                    }
                    int binarySearchSteps = 4 * hit;
                    for (int i = 0; i < binarySearchSteps; i++)
                    {
                        ray *= .5f;
                        if (depthDelta > 0) {
                            currentPosition -= ray;
                        }
                        else if (depthDelta < 0) {
                            currentPosition += ray;
                        }
                        else {
                            break;
                        }

                        float4 uv = mul(_ProjectionMatrix, float4(currentPosition.x, currentPosition.y * -1, currentPosition.z * -1, 1));
                        uv /= uv.w;
                        maskOut = ScreenEdgeMask(uv);
                        uv.x *= 0.5f;
                        uv.y *= 0.5f;
                        uv.x += 0.5f;
                        uv.y += 0.5f;
                        currentScreenSpacePosition = uv;

                        float sd = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv.xy).r;
                        depthDelta = currentPosition.z - LinearEyeDepth(sd);
                        float minv = 1 / max((oneMinusViewReflectDot * float(i)), 0.001);
                        if (abs(depthDelta) > minv) {
                            hit = 0;
                            break;
                        }
                    }

                    float3 currentNormal = UnpackNormal(SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, currentScreenSpacePosition).xyz);
                    float backFaceDot = dot(currentNormal, reflectionRay);
                    if (backFaceDot > 0) {
                        hit = 0;
                    }
                }

                float3 deltaDir = viewSpacePosition.xyz - currentPosition;
                float progress = dot(deltaDir, deltaDir) / (maxDist * maxDist);
                progress = smoothstep(0, .5, 1 - progress);

                maskOut *= hit;
                return half3(currentScreenSpacePosition, maskOut * progress);
            }
            ENDHLSL
        }

        // 1 - Composite
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NormalSample.hlsl"
            #include "Common.hlsl"

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

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;

                return o;
            }

            float _RenderScale;
            float minSmoothness;

            TEXTURE2D_X(_GBuffer1);            //metalness color
            TEXTURE2D_X(_ReflectedColorMap);   //contains reflected uv coordinates
            TEXTURE2D_X(_MainTex);             //main screen color
            TEXTURE2D_X(_GBuffer2);            //normals and smoothness
            TEXTURE2D_X(_GBuffer0);             //diffuse color
            TEXTURE2D_X(_CameraDepthTexture);   //depth texture

            SamplerState sampler_GBuffer1;
            SamplerState sampler_ReflectedColorMap;
            SamplerState sampler_MainTex;
            SamplerState sampler_GBuffer2;
            SamplerState sampler_GBuffer0;
            SamplerState sampler_CameraDepthTexture;

            float4 frag(v2f i) : SV_Target
            {
                _PaddedScale = 1 / _PaddedScale;
                float4 maint = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, i.uv * _PaddedScale);
                float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;
                if (rawDepth == 0) {
                    return maint;
                }
                float3 worldSpacePosition = getWorldPosition(rawDepth, i.uv);
                float3 viewDir = normalize(float3(worldSpacePosition.xyz) - _WorldSpaceCameraPos);

                float4 normal = SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, i.uv);
                normal.xyz = UnpackNormal(normal.xyz);
                float stepS = smoothstep(minSmoothness, 1, normal.w);
                float fresnal = 1 - dot(viewDir, -normal);
                normal.xyz = mul(_ViewMatrix, float4(normal.xyz, 0));
                normal.xyz = mul(_ProjectionMatrix, float4(normal.xyz, 0));
                normal.y *= -1;

                float dither;
                float type;
                if (_DitherMode == 0) {
                    dither = Dither8x8(i.uv.xy * _RenderScale, .5);
                    type = 0;
                }
                else {
                    dither = IGN(i.uv.x * _ScreenParams.x * _RenderScale, i.uv.y * _ScreenParams.y * _RenderScale, _Frame);
                    type = 0;
                }
                dither *= 2;
                dither -= 1;

                float stepSSqrd = pow(stepS, 2);
                const float2 uvOffset = normal * lerp(dither * 0.05f, 0, stepSSqrd);
                float3 reflectedUv = SAMPLE_TEXTURE2D_X(_ReflectedColorMap, sampler_ReflectedColorMap, (i.uv + uvOffset * type) * _PaddedScale);
                float maskVal = saturate(reflectedUv.z) * stepS;
                reflectedUv.xy += uvOffset * (1 - type);

                float lumin = saturate(RGB2Lum(maint) - 1);
                float luminMask = 1 - lumin;
                luminMask = pow(luminMask, 5);

                float2 gb1 = SAMPLE_TEXTURE2D_X(_GBuffer1, sampler_GBuffer1, i.uv.xy).ra;
                float4 specularColor = float4(SAMPLE_TEXTURE2D_X(_GBuffer0, sampler_GBuffer0, i.uv.xy).rgb, 1);

                float fresnalMask = 1 - saturate(RGB2Lum(specularColor));
                fresnalMask = lerp(1, fresnalMask, gb1.x);
                fresnal = lerp(1, fresnal * fresnal, fresnalMask);

                const float lMet = 0.3f;
                const float hMet = 1.0f;
                const float lSpecCol = 0.0;
                const float hSpecCol = 0.6f;

                const float blurL = 0.0f;
                const float blurH = 5.0f;
                const float blurPow = 4;

                specularColor.xyz = lerp(float3(1, 1, 1), specularColor.xyz, lerp(lSpecCol, hSpecCol, gb1.x));

                float fm = clamp(gb1.x, lMet, hMet);
                float ff = 1 - fm;
                float roughnessBlurAmount = lerp(blurL, blurH, 1 - pow(stepS, blurPow));
                float4 reflectedTexture = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_MainTex, reflectedUv.xy, roughnessBlurAmount);

                float ao = gb1.y;
                float refw = maskVal * ao * fresnal * luminMask;
                
                float4 blendedColor = maint * ff + (reflectedTexture * specularColor) * fm;

                float4 res = lerp(maint, blendedColor, refw);

                return res;
            }
            ENDHLSL
        }

        // 2 - HiZ SSR
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #define HIZ_START_LEVEL 0
            #define HIZ_MAX_LEVEL 10
            #define HIZ_STOP_LEVEL 0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NormalSample.hlsl"
            #include "Common.hlsl"

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

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;

                return o;
            }

            TEXTURE2D_X(_GBuffer2);
            TEXTURE2D_X(_MainTex);

            float3 _WorldSpaceViewDir;
            float _RenderScale;
            float numSteps;
            float minSmoothness;
            int iteration;
            int reflectSky;
            float2 crossEpsilon;

            SamplerState sampler_GBuffer2;
            SamplerState sampler_MainTex;

            float4 frag(v2f i) : SV_Target
            {
                float4 gbuff = SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, i.uv);
                float smoothness = gbuff.w;
                float stepS = smoothstep(minSmoothness, 1, smoothness);
                float3 normal = UnpackNormal(gbuff.xyz);

                float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;
                float4 clipSpace = float4(i.uv * 2 - 1, rawDepth, 1);
                float4 viewSpacePosition = mul(_InverseProjectionMatrix, clipSpace);
                viewSpacePosition /= viewSpacePosition.w;
                viewSpacePosition.y *= -1;
                float4 worldSpacePosition = mul(_InverseViewMatrix, viewSpacePosition);
                float3 viewDir = normalize(float3(worldSpacePosition.xyz) - _WorldSpaceCameraPos);
                float3 reflectionRay = reflect(viewDir, normal);

                float3 reflectionRay_v = mul(_ViewMatrix, float4(reflectionRay, 0));
                reflectionRay_v.z *= -1;
                viewSpacePosition.z *= -1;

                float viewReflectDot = saturate(dot(viewDir, reflectionRay));
                float cameraViewReflectDot = saturate(dot(_WorldSpaceViewDir, reflectionRay));

                float thickness = stride * 2;
                float oneMinusViewReflectDot = sqrt(1 - viewReflectDot);
                stride /= oneMinusViewReflectDot;
                thickness /= oneMinusViewReflectDot;

                int hit = 0;
                float maskOut = 1;
                float3 currentPosition = viewSpacePosition.xyz;
                float2 currentScreenSpacePosition = i.uv;

                bool doRayMarch = smoothness > minSmoothness;

                float maxRayLength = numSteps * stride;
                float maxDist = lerp(min(viewSpacePosition.z, maxRayLength), maxRayLength, cameraViewReflectDot);

                if (doRayMarch) {
                    float3 ray = reflectionRay_v * stride;
                    float depthDelta = 0;

                    for (int level = HIZ_START_LEVEL; level < HIZ_MAX_LEVEL; level++) {
                        float2 uv = i.uv;
                        float stepSize = 1.0f / pow(2, level);
                        
                        for (int step = 0; step < numSteps; step++) {
                            currentPosition += ray * stepSize;
                            
                            float4 uvProj = mul(_ProjectionMatrix, float4(currentPosition.x, currentPosition.y * -1, currentPosition.z * -1, 1));
                            uvProj /= uvProj.w;
                            uvProj.x *= 0.5f;
                            uvProj.y *= 0.5f;
                            uvProj.x += 0.5f;
                            uvProj.y += 0.5f;
                            
                            if (uvProj.x >= 1 || uvProj.x < 0 || uvProj.y >= 1 || uvProj.y < 0) {
                                break;
                            }
                            
                            float sampledDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uvProj.xy).r;
                            if (abs(rawDepth - sampledDepth) > 0 && sampledDepth != 0) {
                                depthDelta = currentPosition.z - LinearEyeDepth(sampledDepth);
                                
                                if (depthDelta > 0 && depthDelta < stride * 2) {
                                    currentScreenSpacePosition = uvProj.xy;
                                    hit = 1;
                                    break;
                                }
                            }
                        }
                        
                        if (hit > 0) break;
                    }

                    if (depthDelta > thickness) {
                        hit = 0;
                    }

                    int binarySearchSteps = 4 * hit;
                    for (int i = 0; i < binarySearchSteps; i++) {
                        ray *= .5f;
                        if (depthDelta > 0) {
                            currentPosition -= ray;
                        }
                        else if (depthDelta < 0) {
                            currentPosition += ray;
                        }
                        else {
                            break;
                        }

                        float4 uv = mul(_ProjectionMatrix, float4(currentPosition.x, currentPosition.y * -1, currentPosition.z * -1, 1));
                        uv /= uv.w;
                        maskOut = ScreenEdgeMask(uv);
                        uv.x *= 0.5f;
                        uv.y *= 0.5f;
                        uv.x += 0.5f;
                        uv.y += 0.5f;
                        currentScreenSpacePosition = uv;

                        float sd = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv.xy).r;
                        depthDelta = currentPosition.z - LinearEyeDepth(sd);
                        float minv = 1 / max((oneMinusViewReflectDot * float(i)), 0.001);
                        if (abs(depthDelta) > minv) {
                            hit = 0;
                            break;
                        }
                    }

                    float3 currentNormal = UnpackNormal(SAMPLE_TEXTURE2D_X(_GBuffer2, sampler_GBuffer2, currentScreenSpacePosition).xyz);
                    float backFaceDot = dot(currentNormal, reflectionRay);
                    if (backFaceDot > 0) {
                        hit = 0;
                    }
                }

                float3 deltaDir = viewSpacePosition.xyz - currentPosition;
                float progress = dot(deltaDir, deltaDir) / (maxDist * maxDist);
                progress = smoothstep(0, .5, 1 - progress);

                maskOut *= hit;
                return float4(currentScreenSpacePosition, maskOut * progress, 1.0);
            }
            ENDHLSL
        }
    }
}