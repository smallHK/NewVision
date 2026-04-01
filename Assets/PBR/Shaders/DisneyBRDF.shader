Shader "PBR/DisneyBRDF"
{
    //Properties
    //{
    //    _BaseMap ("Base Map", 2D) = "white" {}
    //    _BaseColor ("Base Color", Color) = (0.8, 0.8, 0.8, 1)
        
    //    _Metallic ("Metallic", Range(0, 1)) = 0
    //    _Roughness ("Roughness", Range(0, 1)) = 0.5
        
    //    _Specular ("Specular", Range(0, 1)) = 0.5
    //    _SpecularTint ("Specular Tint", Range(0, 1)) = 0
        
    //    _Anisotropic ("Anisotropic", Range(0, 1)) = 0
        
    //    _Sheen ("Sheen", Range(0, 1)) = 0
    //    _SheenTint ("Sheen Tint", Range(0, 1)) = 0.5
        
    //    _Clearcoat ("Clearcoat", Range(0, 1)) = 0
    //    _ClearcoatGloss ("Clearcoat Gloss", Range(0, 1)) = 1
        
    //    _NormalMap ("Normal Map", 2D) = "bump" {}
    //    _NormalScale ("Normal Scale", Range(0, 2)) = 1
        
    //    _OcclusionMap ("Occlusion Map", 2D) = "white" {}
    //    _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        
    //    _EmissionMap ("Emission Map", 2D) = "white" {}
    //    _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        
    //    _EnvMap ("Environment Map", Cube) = "" {}
    //}
    
    //SubShader
    //{
    //    Tags 
    //    { 
    //        "RenderType" = "Opaque" 
    //        "RenderPipeline" = "UniversalPipeline" 
    //    }
    //    LOD 300
        
    //    Pass
    //    {
    //        Name "DisneyBRDFForward"
    //        Tags { "LightMode" = "UniversalForward" }
            
    //        HLSLPROGRAM
    //        #pragma vertex vert
    //        #pragma fragment frag
    //        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
    //        #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
    //        #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
    //        #pragma multi_compile_fragment _ _SHADOWS_SOFT
    //        #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
    //        #pragma multi_compile _ LIGHTMAP_ON
    //        #pragma multi_compile_fog
            
    //        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    //        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    //        #include "DisneyBRDF.hlsl"
            
    //        struct Attributes
    //        {
    //            float4 positionOS : POSITION;
    //            float3 normalOS : NORMAL;
    //            float4 tangentOS : TANGENT;
    //            float2 uv : TEXCOORD0;
    //            float2 lightmapUV : TEXCOORD1;
    //        };
            
    //        struct Varyings
    //        {
    //            float4 positionCS : SV_POSITION;
    //            float2 uv : TEXCOORD0;
    //            float3 normalWS : TEXCOORD1;
    //            float4 tangentWS : TEXCOORD2;
    //            float3 viewDirWS : TEXCOORD3;
    //            float fogFactor : TEXCOORD4;
    //            DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 5);
    //        };
            
    //        TEXTURE2D(_BaseMap);
    //        SAMPLER(sampler_BaseMap);
            
    //        TEXTURE2D(_NormalMap);
    //        SAMPLER(sampler_NormalMap);
            
    //        TEXTURE2D(_OcclusionMap);
    //        SAMPLER(sampler_OcclusionMap);
            
    //        TEXTURE2D(_EmissionMap);
    //        SAMPLER(sampler_EmissionMap);
            
    //        TEXTURECUBE(_EnvMap);
    //        SAMPLER(sampler_EnvMap);
            
    //        CBUFFER_START(UnityPerMaterial)
    //            float4 _BaseMap_ST;
    //            float4 _BaseColor;
    //            float _Metallic;
    //            float _Roughness;
    //            float _Specular;
    //            float _SpecularTint;
    //            float _Anisotropic;
    //            float _Sheen;
    //            float _SheenTint;
    //            float _Clearcoat;
    //            float _ClearcoatGloss;
    //            float _NormalScale;
    //            float _OcclusionStrength;
    //            float4 _EmissionColor;
    //        CBUFFER_END
            
    //        Varyings vert(Attributes input)
    //        {
    //            Varyings output;
                
    //            VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    //            VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
    //            output.positionCS = vertexInput.positionCS;
    //            output.normalWS = normalInput.normalWS;
                
    //            real sign = input.tangentOS.w * GetOddNegativeScale();
    //            output.tangentWS = float4(normalInput.tangentWS.xyz, sign);
                
    //            output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
    //            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                
    //            OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    //            OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
                
    //            output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
    //            return output;
    //        }
            
    //        float3 GetNormalWS(Varyings input)
    //        {
    //            float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalScale);
                
    //            float sgn = input.tangentWS.w;
    //            float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    //            float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
                
    //            return TransformTangentToWorld(normalTS, tangentToWorld);
    //        }
            
    //        half4 frag(Varyings input) : SV_Target
    //        {
    //            float3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
    //            float3 normalWS = GetNormalWS(input);
    //            float3 viewDirWS = normalize(input.viewDirWS);
                
    //            float occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g;
    //            occlusion = lerp(1.0, occlusion, _OcclusionStrength);
                
    //            float3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                
    //            Light mainLight = GetMainLight(input.shadowCoord, input.positionCS, unity_ShadowMask);
                
    //            float3 L = mainLight.direction;
    //            float3 V = viewDirWS;
    //            float3 N = normalize(normalWS);
                
    //            float sgn = input.tangentWS.w;
    //            float3 tangentWS = input.tangentWS.xyz;
    //            float3 bitangentWS = sgn * cross(N, tangentWS);
                
    //            float3 directLighting = DisneyBRDF(N, V, L, tangentWS, bitangentWS,
    //                                                baseColor, _Metallic, _Roughness,
    //                                                _Specular, _SpecularTint, _Anisotropic,
    //                                                _Sheen, _SheenTint, _Clearcoat, _ClearcoatGloss);
    //            directLighting *= mainLight.color * mainLight.shadowAttenuation;
                
    //            #ifdef _ADDITIONAL_LIGHTS
    //            uint additionalLightsCount = GetAdditionalLightsCount();
    //            for (uint lightIndex = 0u; lightIndex < additionalLightsCount; ++lightIndex)
    //            {
    //                Light light = GetAdditionalLight(lightIndex, input.positionCS);
    //                float3 additionalLighting = DisneyBRDF(N, V, light.direction, tangentWS, bitangentWS,
    //                                                      baseColor, _Metallic, _Roughness,
    //                                                      _Specular, _SpecularTint, _Anisotropic,
    //                                                      _Sheen, _SheenTint, _Clearcoat, _ClearcoatGloss);
    //                directLighting += additionalLighting * light.color * light.shadowAttenuation * light.distanceAttenuation;
    //            }
    //            #endif
                
    //            float3 indirectDiffuse = SAMPLE_TEXTURECUBE(_EnvMap, sampler_EnvMap, N).rgb * baseColor;
                
    //            float3 R = reflect(-V, N);
    //            float3 indirectSpecular = SAMPLE_TEXTURECUBE(_EnvMap, sampler_EnvMap, R).rgb;
                
    //            float NdotV = max(0.001, dot(N, V));
    //            float3 Cdlin = baseColor;
    //            float Cdlum = 0.3 * Cdlin.r + 0.6 * Cdlin.g + 0.1 * Cdlin.b;
    //            float3 Ctint = Cdlum > 0.0 ? Cdlin / Cdlum : float3(1, 1, 1);
    //            float3 Cspec0 = lerp(_Specular * 0.08 * lerp(float3(1, 1, 1), Ctint, _SpecularTint), 
    //                                 Cdlin, _Metallic);
    //            float3 F = SchlickFresnel(NdotV, Cspec0);
                
    //            float3 indirectLighting = (1.0 - F) * (1.0 - _Metallic) * indirectDiffuse + F * indirectSpecular;
    //            indirectLighting += 0.25 * _Clearcoat * SchlickFresnel(NdotV, 0.04) * 
    //                                SAMPLE_TEXTURECUBE(_EnvMap, sampler_EnvMap, R).rgb;
                
    //            float3 ambient = SampleSH(N) * baseColor * (1.0 - _Metallic);
    //            indirectLighting += ambient;
                
    //            float3 finalColor = directLighting + indirectLighting * occlusion + emission;
                
    //            finalColor = MixFog(finalColor, input.fogFactor);
                
    //            return half4(finalColor, 1.0);
    //        }
    //        ENDHLSL
    //    }
        
    //    Pass
    //    {
    //        Name "ShadowCaster"
    //        Tags { "LightMode" = "ShadowCaster" }
            
    //        ZWrite On
    //        ZTest LEqual
    //        ColorMask 0
            
    //        HLSLPROGRAM
    //        #pragma vertex ShadowPassVertex
    //        #pragma fragment ShadowPassFragment
            
    //        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    //        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    //        #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
    //        #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            
    //        ENDHLSL
    //    }
        
    //    Pass
    //    {
    //        Name "DepthOnly"
    //        Tags { "LightMode" = "DepthOnly" }
            
    //        ZWrite On
    //        ColorMask 0
            
    //        HLSLPROGRAM
    //        #pragma vertex DepthOnlyVertex
    //        #pragma fragment DepthOnlyFragment
            
    //        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    //        #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
    //        #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            
    //        ENDHLSL
    //    }
        
    //    Pass
    //    {
    //        Name "Meta"
    //        Tags { "LightMode" = "Meta" }
            
    //        Cull Off
            
    //        HLSLPROGRAM
    //        #pragma vertex UniversalVertexMeta
    //        #pragma fragment UniversalFragmentMeta
            
    //        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    //        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            
    //        TEXTURE2D(_BaseMap);
    //        SAMPLER(sampler_BaseMap);
            
    //        CBUFFER_START(UnityPerMaterial)
    //            float4 _BaseMap_ST;
    //            float4 _BaseColor;
    //            float _Metallic;
    //            float _Roughness;
    //            float _Specular;
    //            float _SpecularTint;
    //            float _Anisotropic;
    //            float _Sheen;
    //            float _SheenTint;
    //            float _Clearcoat;
    //            float _ClearcoatGloss;
    //            float _NormalScale;
    //            float _OcclusionStrength;
    //            float4 _EmissionColor;
    //        CBUFFER_END
            
    //        struct Attributes
    //        {
    //            float4 positionOS : POSITION;
    //            float3 normalOS : NORMAL;
    //            float2 uv0 : TEXCOORD0;
    //            float2 uv1 : TEXCOORD1;
    //            float2 uv2 : TEXCOORD2;
    //        };
            
    //        struct Varyings
    //        {
    //            float4 positionCS : SV_POSITION;
    //            float2 uv : TEXCOORD0;
    //        };
            
    //        Varyings UniversalVertexMeta(Attributes input)
    //        {
    //            Varyings output;
    //            output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
    //            output.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
    //            return output;
    //        }
            
    //        half4 UniversalFragmentMeta(Varyings input) : SV_Target
    //        {
    //            float3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
    //            float3 emission = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _EmissionColor.rgb;
                
    //            MetaInput metaInput;
    //            metaInput.Albedo = baseColor;
    //            metaInput.Emission = emission;
    //            metaInput.SpecularColor = float3(0, 0, 0);
                
    //            return MetaFragment(metaInput);
    //        }
            
    //        ENDHLSL
    //    }
    //}
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
