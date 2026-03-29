Shader "Hidden/VXGI/Voxelization"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Name "VOXELIZATION"
            Tags { "LightMode"="Voxelization" }

            Cull Off
            ZTest Always
            ZWrite Off
            HLSLPROGRAM
            #pragma require geometry
            #pragma require randomwrite
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma shader_feature _EMISSION
            #pragma shader_feature_local _METALLICGLOSSMAP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define AXIS_X 0
            #define AXIS_Y 1
            #define AXIS_Z 2
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half3 _EmissionColor;
                float4 _MainTex_ST;
                half _Metallic;
            CBUFFER_END

            TEXTURE2D(_EmissionMap);
            TEXTURE2D(_MainTex);
            TEXTURE2D(_MetallicGlossMap);
            SAMPLER(sampler_MainTex);

            RW_TEXTURE3D(float4, _VoxelRadiance);
            int _VoxelResolution;
            float4x4 _WorldToVoxel;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2g
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD;
            };

            struct g2f
            {
                float4 position : SV_POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float axis : TEXCOORD1;
            };

            v2g vert(Attributes input)
            {
                v2g o;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 posVS = mul(_WorldToVoxel, float4(posWS, 1)).xyz;
                o.vertex = float4(posVS * 2 - 1, 1);
                o.normal = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return o;
            }
            float3 SwizzleAxis(float3 position, uint axis) {
                uint a = axis + 1;
                float3 p = position;
                position.x = p[(0 + a) % 3];
                position.y = p[(1 + a) % 3];
                position.z = p[(2 + a) % 3];
                return position;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g i[3], inout TriangleStream<g2f> triStream)
            {
                float3 normal = normalize(abs(cross(i[1].vertex - i[0].vertex, i[2].vertex - i[0].vertex)));
                uint axis = AXIS_Z;
                if (normal.x > normal.y && normal.x > normal.z) axis = AXIS_X;
                else if (normal.y > normal.x && normal.y > normal.z) axis = AXIS_Y;

                [unroll]
                for (int j = 0; j < 3; j++) {
                    g2f o;
                    o.position = float4(SwizzleAxis(i[j].vertex.xyz, axis), 1.0);
                    o.normal = i[j].normal;
                    o.axis = axis;
                    o.uv = i[j].uv;
                    triStream.Append(o);
                }
            }

            float3 RestoreAxis(float3 position, uint axis) {
                uint a = 2 - axis;
                float3 p = position;
                position.x = p[(0 + a) % 3];
                position.y = p[(1 + a) % 3];
                position.z = p[(2 + a) % 3];
                return position;
            }

            [earlydepthstencil]
            half4 frag(g2f i) : SV_TARGET
            {
#ifdef _METALLICGLOSSMAP
                float metallic = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MainTex, i.uv).r;
#else
                float metallic = _Metallic;
#endif

                i.normal = normalize(i.normal);

#ifdef _EMISSION
                float3 emission = _EmissionColor * SAMPLE_TEXTURE2D_LOD(_EmissionMap, sampler_MainTex, i.uv, 0).rgb;
#else
                float3 emission = 0.0;
#endif
                float3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Color.rgb;
                float3 color = albedo * (1 - metallic * 0.5);
                float3 envSH = SampleSH(float4(i.normal, 1.0));

                // 获取URP主光源
                Light mainLight = GetMainLight();

                // 计算主光源直接光照
                float NdotL = saturate(dot(i.normal, mainLight.direction));
                float3 directLight = mainLight.color * NdotL * mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                // 写入体素
                float3 voxelPos = (i.position.xyz * 0.5 + 0.5) * _VoxelResolution;
                voxelPos = RestoreAxis(voxelPos, i.axis);
                int3 voxelIdx = int3(floor(voxelPos));
                
                if (all(voxelIdx >= 0) && all(voxelIdx < _VoxelResolution))
                {
                    float4 voxelData = float4(
                        color * directLight +  // 主光源直接光照
                        emission +             // 自发光
                        envSH * 0.5,           // 环境光
                        1.0
                    );
                    _VoxelRadiance[voxelIdx] = voxelData;
                }
                return 0.0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}