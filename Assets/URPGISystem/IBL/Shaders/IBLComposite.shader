Shader "NewVision/IBL/Composite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _IrradianceMap ("Irradiance Map", Cube) = "white" {}
        _PrefilterMap ("Prefilter Map", Cube) = "white" {}
        _BRDFLut ("BRDF Lut", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "brdf.hlsl"

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

            sampler2D _MainTex;
            samplerCUBE _IrradianceMap;
            samplerCUBE _PrefilterMap;
            sampler2D _BRDFLut;
            TEXTURE2D_X(_GBuffer0);
            TEXTURE2D_X_HALF(_GBuffer1);
            TEXTURE2D_X_HALF(_GBuffer2);
            TEXTURE2D_X(_CameraDepthTexture);
            SamplerState my_point_clamp_sampler;

            float4 GetFragmentWorldPos(float2 screenPos)
            {
                float sceneRawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, my_point_clamp_sampler, screenPos).r;
                float4 ndc = float4(screenPos.x * 2 - 1, screenPos.y * 2 - 1, sceneRawDepth, 1);
                #if UNITY_UV_STARTS_AT_TOP
                    ndc.y *= -1;
                #endif
                float4 worldPos = mul(UNITY_MATRIX_I_VP, ndc);
                worldPos /= worldPos.w;

                return worldPos;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);

                // decode from gbuffer
                float4 worldPos = GetFragmentWorldPos(i.uv);
                float3 albedo = SAMPLE_TEXTURE2D_X_LOD(_GBuffer0, my_point_clamp_sampler, i.uv, 0).xyz;
                float3 normal = SAMPLE_TEXTURE2D_X_LOD(_GBuffer2, my_point_clamp_sampler, i.uv, 0).xyz;
                float3 metallicRoughness = SAMPLE_TEXTURE2D_X_LOD(_GBuffer1, my_point_clamp_sampler, i.uv, 0).xyz;
                float metallic = metallicRoughness.r;
                float roughness = metallicRoughness.g;

                // view direction
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos.xyz);
                float3 reflectDir = reflect(-viewDir, normal);

                // calculate F0
                float3 F0 = float3(0.04, 0.04, 0.04);
                F0 = lerp(F0, albedo, metallic);

                // indirect lighting (IBL)
                float3 F = fresnelSchlickRoughness(max(dot(normal, viewDir), 0.0), F0, roughness);

                // diffuse component
                float3 kS = F;
                float3 kD = 1.0 - kS;
                kD *= 1.0 - metallic;

                // irradiance
                float3 irradiance = texCUBE(_IrradianceMap, normal).rgb;
                float3 diffuse = irradiance * albedo;

                // specular component
                const float MAX_REFLECTION_LOD = 4.0;
                float3 prefilteredColor = texCUBElod(_PrefilterMap, float4(reflectDir, roughness * MAX_REFLECTION_LOD)).rgb;
                float2 brdf = tex2D(_BRDFLut, float2(max(dot(normal, viewDir), 0.0), roughness)).rg;
                float3 specular = prefilteredColor * (F * brdf.x + brdf.y);

                // final IBL contribution
                float3 ibl = (kD * diffuse + specular);

                color.rgb += ibl;
                
                return color;
            }
            ENDHLSL
        }
    }
}