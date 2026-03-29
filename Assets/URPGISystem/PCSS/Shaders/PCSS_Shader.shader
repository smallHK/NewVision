// PCSS (Percentage Closer Soft Shadows) for URP
// Based on NVIDIA's PCSS implementation

Shader "Hidden/PCSS/PCSS_Shader"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    // Configuration
    uniform float RECEIVER_PLANE_MIN_FRACTIONAL_ERROR = 0.025;

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
        float3 ray : TEXCOORD1;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 ray : TEXCOORD1;
        float3 orthoPosNear : TEXCOORD2;
        float3 orthoPosFar : TEXCOORD3;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float4 clipPos = TransformObjectToHClip(input.positionOS.xyz);
        output.positionCS = clipPos;
        output.uv = input.uv;
        output.ray = input.ray;

        // Compute orthographic positions for orthographic projection support
        clipPos.y *= _ProjectionParams.x;
        float3 orthoPosNear = mul(unity_CameraInvProjection, float4(clipPos.x, clipPos.y, -1, 1)).xyz;
        float3 orthoPosFar = mul(unity_CameraInvProjection, float4(clipPos.x, clipPos.y, 1, 1)).xyz;
        orthoPosNear.z *= -1;
        orthoPosFar.z *= -1;
        output.orthoPosNear = orthoPosNear;
        output.orthoPosFar = orthoPosFar;

        return output;
    }

    // PCSS Configuration
    uniform float Blocker_Samples = 16;
    uniform float PCF_Samples = 16;
    uniform float Softness = 1.0;
    uniform float SoftnessFalloff = 1.0;
    uniform float Blocker_GradientBias = 0.0;
    uniform float PCF_GradientBias = 1.0;
    uniform float CascadeBlendDistance = 0.5;

    uniform sampler2D _MainLightShadowmapTexture;
    float4 _MainLightShadowmapTexture_TexelSize;

    uniform sampler2D _NoiseTexture;
    uniform float4 NoiseCoords;

    // 深度纹理采样
    uniform sampler2D _CameraDepthTexture;

    inline float SampleSceneDepth(float2 uv)
    {
        return tex2Dlod(_CameraDepthTexture, float4(uv, 0, 0)).r;
    }

    // 线性深度转换
    inline float Linear01Depth(float depth)
    {
        float z = depth * 2.0 - 1.0;
        return (2.0 * _ProjectionParams.y) / (_ProjectionParams.z + _ProjectionParams.y - z * (_ProjectionParams.z - _ProjectionParams.y));
    }

    // 阴影相关函数
    uniform float4 _LightSplitsNear;
    uniform float4 _LightSplitsFar;
    uniform float4x4 _MainLightWorldToShadow[4];
    uniform float4 unity_ShadowCascadeScales;

    inline float4 GetCascadeWeights(float4 wpos, float z)
    {
        float4 zNear = float4(z >= _LightSplitsNear);
        float4 zFar = float4(z < _LightSplitsFar);
        float4 weights = zNear * zFar;
        return weights;
    }

    inline float4 GetShadowCoord(float4 wpos, float4 cascadeWeights)
    {
        float3 sc0 = mul(_MainLightWorldToShadow[0], wpos).xyz;
        float3 sc1 = mul(_MainLightWorldToShadow[1], wpos).xyz;
        float3 sc2 = mul(_MainLightWorldToShadow[2], wpos).xyz;
        float3 sc3 = mul(_MainLightWorldToShadow[3], wpos).xyz;
        float4 shadowMapCoordinate = float4(sc0 * cascadeWeights[0] + sc1 * cascadeWeights[1] + sc2 * cascadeWeights[2] + sc3 * cascadeWeights[3], 1);
    #if defined(UNITY_REVERSED_Z)
        float noCascadeWeights = 1 - dot(cascadeWeights, float4(1, 1, 1, 1));
        shadowMapCoordinate.z += noCascadeWeights;
    #endif
        return shadowMapCoordinate;
    }

    inline float4 GetShadowCoord_SingleCascade(float4 wpos)
    {
        return float4(mul(_MainLightWorldToShadow[0], wpos).xyz, 0);
    }

    inline float SAMPLE_SHADOW(sampler2D shadowMap, float4 coord)
    {
        float depth = tex2Dlod(shadowMap, float4(coord.xy, 0, 0)).r;
    #if defined(UNITY_REVERSED_Z)
        return step(coord.z, depth);
    #else
        return step(depth, coord.z);
    #endif
    }

    // Poisson samples
    #if defined(POISSON_32)
    static const float2 PoissonOffsets[32] = {
        float2(0.06407013, 0.05409927),
        float2(0.7366577, 0.5789394),
        float2(-0.6270542, -0.5320278),
        float2(-0.4096107, 0.8411095),
        float2(0.6849564, -0.4990818),
        float2(-0.874181, -0.04579735),
        float2(0.9989998, 0.0009880066),
        float2(-0.004920578, -0.9151649),
        float2(0.1805763, 0.9747483),
        float2(-0.2138451, 0.2635818),
        float2(0.109845, 0.3884785),
        float2(0.06876755, -0.3581074),
        float2(0.374073, -0.7661266),
        float2(0.3079132, -0.1216763),
        float2(-0.3794335, -0.8271583),
        float2(-0.203878, -0.07715034),
        float2(0.5912697, 0.1469799),
        float2(-0.88069, 0.3031784),
        float2(0.5040108, 0.8283722),
        float2(-0.5844124, 0.5494877),
        float2(0.6017799, -0.1726654),
        float2(-0.5554981, 0.1559997),
        float2(-0.3016369, -0.3900928),
        float2(-0.5550632, -0.1723762),
        float2(0.925029, 0.2995041),
        float2(-0.2473137, 0.5538505),
        float2(0.9183037, -0.2862392),
        float2(0.2469421, 0.6718712),
        float2(0.3916397, -0.4328209),
        float2(-0.03576927, -0.6220032),
        float2(-0.04661255, 0.7995201),
        float2(0.4402924, 0.3640312),
    };
    #else
    static const float2 PoissonOffsets[64] = {
        float2(0.0617981, 0.07294159),
        float2(0.6470215, 0.7474022),
        float2(-0.5987766, -0.7512833),
        float2(-0.693034, 0.6913887),
        float2(0.6987045, -0.6843052),
        float2(-0.9402866, 0.04474335),
        float2(0.8934509, 0.07369385),
        float2(0.1592735, -0.9686295),
        float2(-0.05664673, 0.995282),
        float2(-0.1203411, -0.1301079),
        float2(0.1741608, -0.1682285),
        float2(-0.09369049, 0.3196758),
        float2(0.185363, 0.3213367),
        float2(-0.1493771, -0.3147511),
        float2(0.4452095, 0.2580113),
        float2(-0.1080467, -0.5329178),
        float2(0.1604507, 0.5460774),
        float2(-0.4037193, -0.2611179),
        float2(0.5947998, -0.2146744),
        float2(0.3276062, 0.9244621),
        float2(-0.6518704, -0.2503952),
        float2(-0.3580975, 0.2806469),
        float2(0.8587891, 0.4838005),
        float2(-0.1596546, -0.8791054),
        float2(-0.3096867, 0.5588146),
        float2(-0.5128918, 0.1448544),
        float2(0.8581337, -0.424046),
        float2(0.1562584, -0.5610626),
        float2(-0.7647934, 0.2709858),
        float2(-0.3090832, 0.9020988),
        float2(0.3935608, 0.4609676),
        float2(0.3929337, -0.5010948),
        float2(-0.8682281, -0.1990303),
        float2(-0.01973724, 0.6478714),
        float2(-0.3897587, -0.4665619),
        float2(-0.7416366, -0.4377831),
        float2(-0.5523247, 0.4272514),
        float2(-0.5325066, 0.8410385),
        float2(0.3085465, -0.7842533),
        float2(0.8400612, -0.200119),
        float2(0.6632416, 0.3067062),
        float2(-0.4462856, -0.04265022),
        float2(0.06892014, 0.812484),
        float2(0.5149567, -0.7502338),
        float2(0.6464897, -0.4666451),
        float2(-0.159861, 0.1038342),
        float2(0.6455986, 0.04419327),
        float2(-0.7445076, 0.5035095),
        float2(0.9430245, 0.3139912),
        float2(0.0349884, -0.7968109),
        float2(-0.9517487, 0.2963554),
        float2(-0.7304786, -0.01006928),
        float2(-0.5862702, -0.5531025),
        float2(0.3029106, 0.09497032),
        float2(0.09025345, -0.3503742),
        float2(0.4356628, -0.0710125),
        float2(0.4112572, 0.7500054),
        float2(0.3401214, -0.3047142),
        float2(-0.2192158, -0.6911137),
        float2(-0.4676369, 0.6570358),
        float2(0.6295372, 0.5629555),
        float2(0.1253822, 0.9892166),
        float2(-0.1154335, 0.8248222),
        float2(-0.4230408, -0.7129914),
    };
    #endif

    // Helper methods
    inline float ValueNoise(float3 pos)
    {
        float3 Noise_skew = pos + 0.2127 + pos.x * pos.y * pos.z * 0.3713;
        float3 Noise_rnd = 4.789 * sin(489.123 * (Noise_skew));
        return frac(Noise_rnd.x * Noise_rnd.y * Noise_rnd.z * (1.0 + Noise_skew.x));
    }

    inline float2 Rotate(float2 pos, float2 rotationTrig)
    {
        return float2(pos.x * rotationTrig.x - pos.y * rotationTrig.y, pos.y * rotationTrig.x + pos.x * rotationTrig.y);
    }

    inline float SampleShadowmapDepth(float2 uv)
    {
        return tex2Dlod(_MainLightShadowmapTexture, float4(uv, 0.0, 0.0)).r;
    }

    inline float SampleShadowmap_Soft(float4 coord)
    {
        return SAMPLE_SHADOW(_MainLightShadowmapTexture, coord);
    }

    inline float SampleShadowmap(float4 coord)
    {
        float depth = SampleShadowmapDepth(coord.xy);
        return step(depth, coord.z);
    }

    inline float GetScale(float4 cascadeWeights)
    {
        float scale = 1.0;
        scale = (cascadeWeights.y > 0.0) ? 2.0 : scale;
        scale = (cascadeWeights.z > 0.0) ? 4.0 : scale;
        scale = (cascadeWeights.w > 0.0) ? 8.0 : scale;
        return 1.0 / scale;
    }

    // Find Blocker
    float2 FindBlocker(float2 uv, float depth, float scale, float searchUV, float2 receiverPlaneDepthBias, float2 rotationTrig)
    {
        float avgBlockerDepth = 0.0;
        float numBlockers = 0.0;
        float blockerSum = 0.0;

        for (int i = 0; i < Blocker_Samples; i++)
        {
            float2 offset = PoissonOffsets[i] * searchUV * scale;
            offset = Rotate(offset, rotationTrig);

            float shadowMapDepth = SampleShadowmapDepth(uv + offset);
            float biasedDepth = depth;

    #if defined(USE_BLOCKER_BIAS)
            biasedDepth += dot(offset, receiverPlaneDepthBias) * Blocker_GradientBias;
    #endif

    #if defined(UNITY_REVERSED_Z)
            if (shadowMapDepth > biasedDepth)
    #else
            if (shadowMapDepth < biasedDepth)
    #endif
            {
                blockerSum += shadowMapDepth;
                numBlockers += 1.0;
            }
        }

        avgBlockerDepth = blockerSum / numBlockers;

    #if defined(UNITY_REVERSED_Z)
        avgBlockerDepth = 1.0 - avgBlockerDepth;
    #endif

        return float2(avgBlockerDepth, numBlockers);
    }

    // PCF Sampling
    float PCF_Filter(float2 uv, float depth, float scale, float filterRadiusUV, float2 receiverPlaneDepthBias, float penumbra, float2 rotationTrig)
    {
        float sum = 0.0f;
    #if defined(UNITY_REVERSED_Z)
        receiverPlaneDepthBias *= -1.0;
    #endif

        for (int i = 0; i < PCF_Samples; i++)
        {
            float2 offset = PoissonOffsets[i] * filterRadiusUV * scale;
            offset = Rotate(offset, rotationTrig);

            float biasedDepth = depth;

    #if defined(USE_PCF_BIAS)
            biasedDepth += dot(offset, receiverPlaneDepthBias) * PCF_GradientBias;
    #endif

            float value = SampleShadowmap_Soft(float4(uv.xy + offset, biasedDepth, 0));
            sum += value;
        }

        sum /= PCF_Samples;
        return sum;
    }

    // PCSS Main
    float PCSS_Main(float4 coords, float2 receiverPlaneDepthBias, float random, float scale)
    {
        float2 uv = coords.xy;
        float depth = coords.z;
        float zAwareDepth = depth;

    #if defined(UNITY_REVERSED_Z)
        zAwareDepth = 1.0 - depth;
    #endif

        float rotationAngle = random * 3.1415926;
        float2 rotationTrig = float2(cos(rotationAngle), sin(rotationAngle));

    #if defined(UNITY_REVERSED_Z)
        receiverPlaneDepthBias *= -1.0;
    #endif

        // STEP 1: blocker search
        float searchSize = Softness * saturate(zAwareDepth - 0.02) / zAwareDepth;
        float2 blockerInfo = FindBlocker(uv, depth, scale, searchSize, receiverPlaneDepthBias, rotationTrig);

        if (blockerInfo.y < 1)
        {
            // There are no occluders so early out
            return 1.0;
        }

        // STEP 2: penumbra size
        float penumbra = zAwareDepth - blockerInfo.x;

    #if defined(USE_FALLOFF)
        penumbra = 1.0 - pow(1.0 - penumbra, SoftnessFalloff);
    #endif

        float filterRadiusUV = penumbra * Softness;

        // STEP 3: filtering
        float shadow = PCF_Filter(uv, depth, scale, filterRadiusUV, receiverPlaneDepthBias, penumbra, rotationTrig);
        return shadow;
    }

    // Compute camera space position from depth
    inline float3 ComputeCameraSpacePosFromDepth(Varyings i)
    {
        float zdepth = SampleSceneDepth(i.uv);
        float3 vposPersp = (i.ray * Linear01Depth(zdepth)).xyz;

    #if defined(UNITY_REVERSED_Z)
        zdepth = 1.0 - zdepth;
    #endif

    #if defined(ORTHOGRAPHIC_SUPPORTED)
        float3 vposOrtho = lerp(i.orthoPosNear, i.orthoPosFar, zdepth);
        return lerp(vposPersp, vposOrtho, unity_OrthoParams.w);
    #else
        return vposPersp;
    #endif
    }

    // PCSS Fragment Shader
    half4 Frag_PCSS(Varyings i) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

        float3 vpos = ComputeCameraSpacePosFromDepth(i);
        float4 wpos = mul(unity_CameraToWorld, float4(vpos, 1));

        float4 cascadeWeights = GetCascadeWeights(wpos, vpos.z);
        float4 coord = GetShadowCoord(wpos, cascadeWeights);

        float random = tex2D(_NoiseTexture, i.uv * NoiseCoords.xy * _ScreenParams.xy).a;
        random = mad(random, 2.0, -1.0);

        float2 receiverPlaneDepthBiasCascade0 = 0.0;
        float2 receiverPlaneDepthBias = 0.0;

    #if defined(USE_STATIC_BIAS) || defined(USE_BLOCKER_BIAS) || defined(USE_PCF_BIAS)
        float3 coordCascade0 = GetShadowCoord_SingleCascade(wpos);
        float3 dx = ddx(coordCascade0.xyz);
        float3 dy = ddy(coordCascade0.xyz);

        receiverPlaneDepthBiasCascade0.x = dy.y * dx.z - dx.y * dy.z;
        receiverPlaneDepthBiasCascade0.y = dx.x * dy.z - dy.x * dx.z;
        receiverPlaneDepthBiasCascade0 *= 1.0f / ((dx.x * dy.y) - (dx.y * dy.x));

        float biasMultiply = dot(cascadeWeights, unity_ShadowCascadeScales);
        receiverPlaneDepthBias = receiverPlaneDepthBiasCascade0 * biasMultiply;

    #if defined(USE_STATIC_BIAS)
        float fractionalSamplingError = 2.0 * dot(_MainLightShadowmapTexture_TexelSize.xy, abs(receiverPlaneDepthBias));
        fractionalSamplingError = min(fractionalSamplingError, RECEIVER_PLANE_MIN_FRACTIONAL_ERROR);

    #if defined(UNITY_REVERSED_Z)
        fractionalSamplingError *= -1.0;
    #endif

        coord.z -= fractionalSamplingError;
    #endif
    #endif

        float scale = GetScale(cascadeWeights);
        float shadow = PCSS_Main(coord, receiverPlaneDepthBias, random, scale);

    #if defined(USE_CASCADE_BLENDING) && !defined(SHADOWS_SPLIT_SPHERES) && !defined(SHADOWS_SINGLE_CASCADE)
        float4 z4 = (float4(vpos.z, vpos.z, vpos.z, vpos.z) - _LightSplitsNear) / (_LightSplitsFar - _LightSplitsNear);
        float alpha = dot(z4 * cascadeWeights, float4(1, 1, 1, 1));

        if (alpha > 1.0 - CascadeBlendDistance)
        {
            alpha = (alpha - (1.0 - CascadeBlendDistance)) / CascadeBlendDistance;

            cascadeWeights = fixed4(0, cascadeWeights.xyz);
            coord = GetShadowCoord(wpos, cascadeWeights);

            scale = GetScale(cascadeWeights);

    #if defined(USE_STATIC_BIAS) || defined(USE_BLOCKER_BIAS) || defined(USE_PCF_BIAS)
            biasMultiply = dot(cascadeWeights, unity_ShadowCascadeScales);
            receiverPlaneDepthBias = receiverPlaneDepthBiasCascade0 * biasMultiply;

    #if defined(USE_STATIC_BIAS)
            fractionalSamplingError = 2.0 * dot(_MainLightShadowmapTexture_TexelSize.xy, abs(receiverPlaneDepthBias));
            fractionalSamplingError = min(fractionalSamplingError, RECEIVER_PLANE_MIN_FRACTIONAL_ERROR);

    #if defined(UNITY_REVERSED_Z)
            fractionalSamplingError *= -1.0;
    #endif

            coord.z -= fractionalSamplingError;
    #endif
    #endif

            float shadowNextCascade = PCSS_Main(coord, receiverPlaneDepthBias, random, scale);
            shadow = lerp(shadow, shadowNextCascade, saturate(alpha));
        }
    #endif

        return half4(shadow, shadow, shadow, 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "PCSS"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_PCSS
            #pragma multi_compile POISSON_32 POISSON_64
            #pragma shader_feature USE_FALLOFF
            #pragma shader_feature USE_CASCADE_BLENDING
            #pragma shader_feature USE_STATIC_BIAS
            #pragma shader_feature USE_BLOCKER_BIAS
            #pragma shader_feature USE_PCF_BIAS
            #pragma shader_feature ORTHOGRAPHIC_SUPPORTED
            #pragma target 3.0
            ENDHLSL
        }
    }

    Fallback Off
}