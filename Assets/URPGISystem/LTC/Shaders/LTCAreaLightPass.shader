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
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "LTCAreaLightPass"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "LTCAreaLight.hlsl"
            #include "LTCCore.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            
            TEXTURE2D(_CameraNormalsTexture);
            SAMPLER(sampler_CameraNormalsTexture);

            float4 _CameraTopLeftCorner;
            float4 _CameraXExtent;
            float4 _CameraYExtent;

            TEXTURE2D(_TransformInv_Diffuse);
            SAMPLER(sampler_TransformInv_Diffuse);
            TEXTURE2D(_TransformInv_Specular);
            SAMPLER(sampler_TransformInv_Specular);
            TEXTURE2D(_AmpDiffAmpSpecFresnel);
            SAMPLER(sampler_AmpDiffAmpSpecFresnel);

            float3 GetWorldPositionFromDepth(float2 uv, float depth)
            {
                float3 worldPos = _CameraTopLeftCorner.xyz + 
                                  uv.x * _CameraXExtent.xyz + 
                                  uv.y * _CameraYExtent.xyz;
                return worldPos;
            }
            
            float3 GetNormalFromGBuffer(float2 uv)
            {
                float3 normal;
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv).xy * 2.0 - 1.0;
                    normal = normalize(UnpackNormalOctQuadEncode(octNormal));
                #else
                    normal = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv).xyz * 2.0 - 1.0;
                    normal = normalize(normal);
                #endif
                return normal;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                float depth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, input.uv).r;
                #if UNITY_REVERSED_Z
                    depth = 1.0 - depth;
                #endif
                
                float3 worldPos = GetWorldPositionFromDepth(input.uv, depth);
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                float3 normal = GetNormalFromGBuffer(input.uv);
                
                float3 diffuseLight = float3(0, 0, 0);
                float3 specularLight = float3(0, 0, 0);
                
                int lightCount = GetAreaLightCount();
                
                for (int i = 0; i < lightCount; i++)
                {
                    AreaLight light = GetAreaLight(i);
                    float3 lightColor = GetAreaLightColor(i);
                    float lightIntensity = GetAreaLightIntensity(i);
                    
                    float4x4 lightVertices = light.vertices;
                    
                    float3x3 Minv = float3x3(
                        1, 0, 0,
                        0, 1, 0,
                        0, 0, 1
                    );
                    
                    float4 ltcResult = LTC_Evaluate(normal, viewDir, worldPos, Minv, lightVertices);
                    
                    diffuseLight += lightColor * lightIntensity * ltcResult.r;
                    specularLight += lightColor * lightIntensity * ltcResult.g;
                }
                
                color.rgb += diffuseLight * 0.5 + specularLight * 0.5;
                
                return saturate(color);
            }
            ENDHLSL
        }
    }
}
