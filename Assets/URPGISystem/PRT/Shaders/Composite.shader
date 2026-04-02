Shader "NewVision/PRT/Composite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
            #include "SH.hlsl"

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
            TEXTURE2D_X(_CameraDepthTexture);
            TEXTURE2D_X_HALF(_GBuffer0);
            TEXTURE2D_X_HALF(_GBuffer1);
            TEXTURE2D_X_HALF(_GBuffer2);
            SamplerState my_point_clamp_sampler;

            float _coefficientVoxelGridSize;
            float4 _coefficientVoxelCorner;
            float4 _coefficientVoxelSize;
            StructuredBuffer<int> _coefficientVoxel; 
            StructuredBuffer<int> _lastFrameCoefficientVoxel;

            float _GIIntensity;

            // 步骤1: 从屏幕坐标和深度重建世界坐标
            float4 GetFragmentWorldPos(float2 screenPos)
            {
                // 步骤1.1: 采样深度纹理获取原始深度值
                float sceneRawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, my_point_clamp_sampler, screenPos);
                // 步骤1.2: 构建NDC坐标
                float4 ndc = float4(screenPos.x * 2 - 1, screenPos.y * 2 - 1, sceneRawDepth, 1);
                #if UNITY_UV_STARTS_AT_TOP
                    ndc.y *= -1;
                #endif
                // 步骤1.3: 使用逆视图投影矩阵变换到世界空间
                float4 worldPos = mul(UNITY_MATRIX_I_VP, ndc);
                worldPos /= worldPos.w;

                return worldPos;
            }

            // 步骤2: 片元着色器 - 合成间接光照
            float4 frag (v2f i) : SV_Target
            {
                // 步骤2.1: 采样原始场景颜色
                float4 color = tex2D(_MainTex, i.uv);

                // 步骤2.2: 从深度重建世界坐标
                float4 worldPos = GetFragmentWorldPos(i.uv);
                // 步骤2.3: 从GBuffer采样反照率（Albedo）
                float3 albedo = SAMPLE_TEXTURE2D_X_LOD(_GBuffer0, my_point_clamp_sampler, i.uv, 0).xyz;
                // 步骤2.4: 从GBuffer采样法线
                float3 normal = SAMPLE_TEXTURE2D_X_LOD(_GBuffer2, my_point_clamp_sampler, i.uv, 0).xyz;

                // 步骤2.5: 从SH体素采样间接光照
                float3 gi = SampleSHVoxel(
                    worldPos, 
                    albedo, 
                    normal,
                    _coefficientVoxel,
                    _coefficientVoxelGridSize,
                    _coefficientVoxelCorner,
                    _coefficientVoxelSize
                );
                // 步骤2.6: 将间接光照添加到场景颜色
                color.rgb += gi * _GIIntensity;
                
                return color;
            }
            ENDHLSL
        }
    }
}
