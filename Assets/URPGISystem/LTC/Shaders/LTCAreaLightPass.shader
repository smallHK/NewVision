Shader "Hidden/LTCAreaLightPass"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            //Name "LTCAreaLightPass"
            //ZTest Always
            //ZWrite Off
            //Cull Off

            //HLSLPROGRAM
            //#pragma vertex vert
            //#pragma fragment frag

            //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            //#include "LTCAreaLight.hlsl"
            //#include "LTCCore.hlsl"

            //struct Attributes
            //{
            //    float4 positionOS   : POSITION;
            //    float2 uv           : TEXCOORD0;
            //};

            //struct Varyings
            //{
            //    float4 positionHCS  : SV_POSITION;
            //    float2 uv           : TEXCOORD0;
            //};

            //Varyings vert(Attributes input)
            //{
            //    Varyings output;
            //    output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
            //    output.uv = input.uv;
            //    return output;
            //}

            //TEXTURE2D(_CameraColorTexture);
            //SAMPLER(sampler_CameraColorTexture);

            //float4 _CameraTopLeftCorner;
            //float4 _CameraXExtent;
            //float4 _CameraYExtent;

            //TEXTURE2D(_TransformInv_Diffuse);
            //SAMPLER(sampler_TransformInv_Diffuse);
            //TEXTURE2D(_TransformInv_Specular);
            //SAMPLER(sampler_TransformInv_Specular);
            //TEXTURE2D(_AmpDiffAmpSpecFresnel);
            //SAMPLER(sampler_AmpDiffAmpSpecFresnel);

            //float4 frag(Varyings input) : SV_Target
            //{
            //    float4 color = SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_CameraColorTexture, input.uv);
                
            //    float3 worldPos = _CameraTopLeftCorner.xyz + input.uv.x * _CameraXExtent.xyz + input.uv.y * _CameraYExtent.xyz;
            //    float3 viewDir = normalize(worldPos - _WorldSpaceCameraPos);
            //    float3 normal = normalize(cross(dfdx(worldPos), dfdy(worldPos)));
                
            //    for (int i = 0; i < GetAreaLightCount(); i++)
            //    {
            //        AreaLight light = GetAreaLight(i);
            //        float3 lightColor = GetAreaLightColor(i);
            //        float lightIntensity = GetAreaLightIntensity(i);
                    
            //        float3x3 invMat = float3x3(
            //            1, 0, 0,
            //            0, 1, 0,
            //            0, 0, 1
            //        );
                    
            //        float4 ltcResult = LTC_Evaluate(normal, -viewDir, worldPos, invMat, light.vertices);
            //        color.rgb += lightColor * lightIntensity * ltcResult.r;
            //    }
                
            //    return color;
            //}
            //ENDHLSL
        }
    }
}