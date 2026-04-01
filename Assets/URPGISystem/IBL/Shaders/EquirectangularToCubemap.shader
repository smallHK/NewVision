Shader "Hidden/NewVision/IBL/EquirectangularToCubemap"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FaceIndex ("Face Index", Int) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            int _FaceIndex;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            /// <summary>
            /// 将Cubemap面和UV转换为3D方向向量
            /// </summary>
            float3 GetDirectionForFace(int face, float2 uv)
            {
                float2 st = uv * 2.0 - 1.0;

                float3 dir = float3(0, 0, 0);

                if (face == 0)
                {
                    dir = float3(1.0, -st.y, -st.x);
                }
                else if (face == 1)
                {
                    dir = float3(-1.0, -st.y, st.x);
                }
                else if (face == 2)
                {
                    dir = float3(st.x, 1.0, st.y);
                }
                else if (face == 3)
                {
                    dir = float3(st.x, -1.0, -st.y);
                }
                else if (face == 4)
                {
                    dir = float3(st.x, -st.y, 1.0);
                }
                else
                {
                    dir = float3(-st.x, -st.y, -1.0);
                }

                return normalize(dir);
            }

            /// <summary>
            /// 将3D方向向量转换为等距圆柱投影UV坐标
            /// </summary>
            float2 DirectionToEquirectangular(float3 dir)
            {
                float longitude = atan2(dir.x, dir.z);
                float latitude = asin(dir.y);

                float2 uv;
                uv.x = (longitude / PI) * 0.5 + 0.5;
                uv.y = (latitude / (PI * 0.5)) * 0.5 + 0.5;

                return uv;
            }

            /// <summary>
            /// 将等距圆柱投影HDR转换为Cubemap面
            /// </summary>
            float4 frag(v2f i) : SV_Target
            {
                float3 dir = GetDirectionForFace(_FaceIndex, i.uv);
                float2 equiUV = DirectionToEquirectangular(dir);

                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, equiUV);
                return color;
            }
            ENDHLSL
        }
    }
}
