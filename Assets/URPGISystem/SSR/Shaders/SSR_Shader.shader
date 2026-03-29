Shader "Hidden/Universal Render Pipeline/MyExtension/SSR"
{

    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Never


        // 0 - Occlusion estimation with CameraDepthTexture
        Pass
        {
            // Name "Linear SSR"
            // ZTest Always
            // ZWrite Off
            // Cull Off

            // CGPROGRAM
            //     //#pragma vertex VertDefault
            //     //#pragma fragment SSAO
            //     //#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            //     //#pragma multi_compile_local _SOURCE_DEPTH _SOURCE_DEPTH_NORMALS
            //     //#pragma multi_compile_local _RECONSTRUCT_NORMAL_LOW _RECONSTRUCT_NORMAL_MEDIUM _RECONSTRUCT_NORMAL_HIGH
            //     //#pragma multi_compile_local _ _ORTHOGRAPHIC
            //     //#include "GTAOLib.hlsl"
            // #pragma vertex vert
            // #pragma fragment frag
            // #pragma enable_d3d11_debug_symbols

            // #include "UnityCG.cginc"
			// #include "NormalSample.hlsl"
            // #include "Common.hlsl"

            // struct appdata
            // {
            //     float4 vertex : POSITION;
            //     float2 uv : TEXCOORD0;
            // };

            // struct v2f
            // {
            //     float2 uv : TEXCOORD0;
            //     float4 vertex : SV_POSITION;
            // };

            //  v2f vert (appdata v)
            // {
            //     v2f o;
            //     o.vertex = UnityObjectToClipPos(v.vertex);
            //     o.uv = v.uv;
            //     return o;
            // }

            // uniform sampler2D _CameraDepthTexture;

            // uniform sampler2D _MainTex;
            // uniform sampler2D _GBuffer2;

            // float3 _WorldSpaceViewDir;
            // float _RenderScale;
            // float stride;
            // float numSteps;
            // float minSmoothness;
            // int iteration;
            // half3 frag(v2f i) : SV_Target
            // {
            //     float rawDepth = tex2D(_CameraDepthTexture, i.uv).r;
            //     float4 gbuff = tex2D(_GBuffer2, i.uv);
            //     float smoothness = gbuff.w;
            //     float stepS = smoothstep(minSmoothness, 1, smoothness);
			// 	float3 normal = UnpackNormal(gbuff.xyz);

            //     float4 clipSpace = float4(i.uv * 2 - 1, rawDepth, 1);
            //     float4 viewSpacePosition = mul(_InverseProjectionMatrix, clipSpace);
            //     viewSpacePosition /= viewSpacePosition.w;
            //     viewSpacePosition.y *= -1;
            //     float4 worldSpacePosition = mul(_InverseViewMatrix, viewSpacePosition);
            //     float3 viewDir = normalize(float3(worldSpacePosition.xyz) - _WorldSpaceCameraPos);
            //     float3 reflectionRay = reflect(viewDir, normal);

            //     float3 reflectionRay_v = mul(_ViewMatrix, float4(reflectionRay,0));
            //     reflectionRay_v.z *= -1;
            //     viewSpacePosition.z *= -1;

            //     float viewReflectDot = saturate(dot(viewDir, reflectionRay));
            //     float cameraViewReflectDot = saturate(dot(_WorldSpaceViewDir, reflectionRay));

            //     float thickness = stride * 2;
            //     float oneMinusViewReflectDot = sqrt(1 - viewReflectDot);
            //     stride /= oneMinusViewReflectDot;
            //     thickness /= oneMinusViewReflectDot;

            //     int hit = 0;
            //     float maskOut = 1;
            //     float3 currentPosition = viewSpacePosition.xyz;
            //     float2 currentScreenSpacePosition = i.uv;

            //     bool doRayMarch = smoothness > minSmoothness;

            //     float maxRayLength = numSteps * stride;
            //     float maxDist = lerp(min(viewSpacePosition.z, maxRayLength), maxRayLength, cameraViewReflectDot);
            //     float numSteps_f = maxDist / stride;
            //     numSteps = max(numSteps_f, 0);

            //     [branch]
            //     if (doRayMarch) {
            //         float3 ray = reflectionRay_v * stride;
            //         float depthDelta = 0;
            //         [loop]
            //         for (int step = 0; step < numSteps; step++)
            //         {
            //             currentPosition += ray;
            //             float currentDepth;
            //             float2 screenSpace;

            //             float4 uv = mul(_ProjectionMatrix, float4(currentPosition.x, currentPosition.y * -1, currentPosition.z * -1, 1));
            //             uv /= uv.w;
            //             uv.x *= 0.5f;
            //             uv.y *= 0.5f;
            //             uv.x += 0.5f;
            //             uv.y += 0.5f;
            //             [branch]
            //             if (uv.x >= 1 || uv.x < 0 || uv.y >= 1 || uv.y < 0) {
            //                 break;
            //             }
            //             //sample depth at current screen space
            //             float sampledDepth = tex2D(_CameraDepthTexture, uv.xy).r;
            //             [branch]
            //             //compare the current depth of the current position to the camera depth at the screen space
            //             if (abs(rawDepth - sampledDepth) > 0 && sampledDepth != 0) {
            //                 depthDelta = currentPosition.z - LinearEyeDepth(sampledDepth);

            //                 [branch]
            //                 if (depthDelta > 0 && depthDelta < stride * 2) {
            //                     currentScreenSpacePosition = uv.xy;
            //                     hit = 1;
            //                     break;
            //                 }
            //             }
            //         }

            //         if (depthDelta > thickness) {
            //             hit = 0;
            //         }
            //         int binarySearchSteps = binaryStepCount * hit;
            //         [loop]
            //         for (int i = 0; i < binaryStepCount; i++)
            //         {
            //             ray *= .5f;
            //             [flatten]
            //             if (depthDelta > 0) {
            //                 currentPosition -= ray;
            //             }
            //             else if (depthDelta < 0) {
            //                 currentPosition += ray;
            //             }
            //             else {
            //                 break;
            //             }

            //             float4 uv = mul(_ProjectionMatrix, float4(currentPosition.x, currentPosition.y * -1, currentPosition.z * -1, 1));
            //             uv /= uv.w;
            //             maskOut = ScreenEdgeMask(uv);
            //             uv.x *= 0.5f;
            //             uv.y *= 0.5f;
            //             uv.x += 0.5f;
            //             uv.y += 0.5f;
            //             currentScreenSpacePosition = uv;

            //             float sd = tex2D(_CameraDepthTexture, uv.xy).r;
            //             depthDelta = currentPosition.z - LinearEyeDepth(sd);
            //             float minv = 1 / max((oneMinusViewReflectDot * float(i)), 0.001);
            //             if (abs(depthDelta) > minv) {
            //                 hit = 0;
            //                 break;
            //             }
            //         }

            //         //remove backface intersections
			// 		float3 currentNormal = UnpackNormal(tex2D(_GBuffer2, currentScreenSpacePosition).xyz);
            //         float backFaceDot = dot(currentNormal, reflectionRay);
            //         [flatten]
            //         if (backFaceDot > 0) {
            //             hit = 0;
            //         }
            //     }

            //     float3 deltaDir = viewSpacePosition.xyz - currentPosition;
            //     float progress = dot(deltaDir, deltaDir) / (maxDist * maxDist);
            //     progress = smoothstep(0, .5, 1 - progress);

            //     maskOut *= hit;
            //     return half3(currentScreenSpacePosition, maskOut * progress);
            // }
            // ENDCG
        }

        //composite
        Pass
        {
            // Name "SSR Composite"
            // CGPROGRAM
            // #pragma vertex vert
            // #pragma fragment frag
            // #pragma enable_d3d11_debug_symbols

            // #include "UnityCG.cginc"
			// #include "NormalSample.hlsl"
            // #include "Common.hlsl"

            // struct appdata
            // {
            //     float4 vertex : POSITION;
            //     float2 uv : TEXCOORD0;
            // };

            // struct v2f
            // {
            //     float2 uv : TEXCOORD0;
            //     float4 vertex : SV_POSITION;
            // };

            // v2f vert(appdata v)
            // {
            //     v2f o;
            //     o.vertex = UnityObjectToClipPos(v.vertex);
            //     o.uv = v.uv;

            //     return o;
            // }
            // float _RenderScale;
            // float minSmoothness;

            // uniform sampler2D _GBuffer1;            //metalness color
            // uniform sampler2D _ReflectedColorMap;   //contains reflected uv coordinates
            // uniform sampler2D _MainTex;             //main screen color
            // uniform sampler2D _GBuffer2;            //normals and smoothness
            // uniform sampler2D _GBuffer0;             //diffuse color
            // uniform sampler2D _CameraDepthTexture;             //diffuse color

            // fixed4 frag(v2f i) : SV_Target
            // {
            //     _PaddedScale = 1 / _PaddedScale;
            //     float4 maint = tex2D(_MainTex, i.uv * _PaddedScale);
            //     float rawDepth = tex2D(_CameraDepthTexture, i.uv).r;
            //     [branch]
            //     if (rawDepth == 0) {
            //         return maint;
            //     }
            //     float3 worldSpacePosition = getWorldPosition(rawDepth, i.uv);
            //     float3 viewDir = normalize(float3(worldSpacePosition.xyz) - _WorldSpaceCameraPos);

            //     //Get screen space normals and smoothness
            //     float4 normal = tex2D(_GBuffer2, i.uv);
            //     normal.xyz = UnpackNormal(normal.xyz);
            //     float stepS = smoothstep(minSmoothness, 1, normal.w);
            //     float fresnal = 1 - dot(viewDir, -normal);
            //     normal.xyz = mul(_ViewMatrix, float4(normal.xyz, 0));
            //     normal.xyz = mul(_ProjectionMatrix, float4(normal.xyz, 0));
            //     normal.y *= -1;

            //     //Dither calculation
            //     float dither;
            //     //type 0 = use original mask
            //     //type 1 = dither original mask
            //     float type;
            //     [branch]
            //     if (_DitherMode == 0) {
            //         dither = Dither8x8(i.uv.xy * _RenderScale, .5);
            //         type = 0;
            //     }
            //     else {
            //         dither = IGN(i.uv.x * _ScreenParams.x * _RenderScale, i.uv.y * _ScreenParams.y * _RenderScale, _Frame);
            //         type = 0;
            //     }
            //     dither *= 2;
            //     dither -= 1;

            //     //Get dithered UV coords
            //     float stepSSqrd = pow(stepS, 2);
            //     const float2 uvOffset = normal * lerp(dither * 0.05f, 0, stepSSqrd);
            //     float3 reflectedUv = tex2D(_ReflectedColorMap, (i.uv + uvOffset * type) * _PaddedScale);
            //     float maskVal = saturate(reflectedUv.z) * stepS;
            //     reflectedUv.xy += uvOffset * (1 - type);

            //     //Get luminance mask for emmissive materials
            //     float lumin = saturate(RGB2Lum(maint) - 1);
            //     float luminMask = 1 - lumin;
            //     luminMask = pow(luminMask, 5);

            //     //get metal and ao and spec color
            //     float2 gb1 = tex2D(_GBuffer1, i.uv.xy).ra;     
            //     float4 specularColor = float4(tex2D(_GBuffer0, i.uv.xy).rgb, 1);    

            //     //calculate fresnal
            //     float fresnalMask = 1 - saturate(RGB2Lum(specularColor));
            //     fresnalMask = lerp(1, fresnalMask, gb1.x);
            //     fresnal = lerp(1, fresnal * fresnal, fresnalMask);

            //     //values for metallic blending
            //     const float lMet = 0.3f;
            //     const float hMet = 1.0f;
            //     const float lSpecCol = 0.0;
            //     const float hSpecCol = 0.6f;

            //     //values for smoothness blending
            //     const float blurL = 0.0f;
            //     const float blurH = 5.0f;
            //     const float blurPow = 4;

            //     //mix colors
            //     specularColor.xyz = lerp(float3(1, 1, 1), specularColor.xyz, lerp(lSpecCol, hSpecCol, gb1.x));

            //     float fm = clamp(gb1.x, lMet, hMet);
            //     float ff = 1 - fm;
            //     float roughnessBlurAmount = lerp(blurL, blurH, 1 - pow(stepS, blurPow));
            //     float4 reflectedTexture = tex2Dlod(_MainTex, float4(reflectedUv.xy, 0, roughnessBlurAmount));

            //     float ao = gb1.y;
            //     float refw = maskVal * ao * fresnal * luminMask;
                
            //     float4 blendedColor = maint * ff + (reflectedTexture * specularColor) * fm;

            //     float4 res = lerp(maint, blendedColor, refw);

            //     return fixed4(res);            
            
            // }
            // ENDCG
        }


   //     Pass
   //     {
   //         Name "HiZ SSR"
   //         CGPROGRAM
   //         #pragma vertex vert
   //         #pragma fragment frag
   //         #pragma enable_d3d11_debug_symbols
   //         #define HIZ_START_LEVEL 0           //normally in good code, you can start and 
   //                                             //stop at higher levels to improve performance, 
   //                                             //but my code just shits itself, probably due to not making the depth pyramid correctly
   //         #define HIZ_MAX_LEVEL 10
   //         #define HIZ_STOP_LEVEL 0

   //         #include "UnityCG.cginc"
			//#include "NormalSample.hlsl"
   //         #include "Common.hlsl"

   //         struct appdata
   //         {
   //             float4 vertex : POSITION;
   //             float2 uv : TEXCOORD0;
   //         };

   //         struct v2f
   //         {
   //             float2 uv : TEXCOORD0;
   //             float4 vertex : SV_POSITION;
   //         };

   //         v2f vert(appdata v)
   //         {
   //             v2f o;
   //             o.vertex = UnityObjectToClipPos(v.vertex);
   //             o.uv = v.uv;

   //             return o;
   //         }

   //         uniform sampler2D _GBuffer2;
   //         uniform sampler2D _MainTex;
   //         float4 _MainTex_TexelSize;

   //         float3 _WorldSpaceViewDir;
   //         float _RenderScale;
   //         float numSteps;
   //         float minSmoothness;
   //         int iteration;
   //         int reflectSky;
   //         float2 crossEpsilon;

   //         ENDCG
   //     }
    }
}