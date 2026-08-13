Shader "JayFos/Water/URPWater"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.3, 0.7, 1, 0.85)
        _WaveStrength("Wave Strength", Range(0, 0.5)) = 0.06
        _WaveSpeed("Wave Speed", Range(0, 3)) = 0.8
        _WaveFrequency("Wave Frequency", Range(0, 10)) = 1.2
        _NormalScale("Normal Scale", Range(0, 2)) = 0.4
        _ShallowColor("Shallow Color", Color) = (0.2, 0.85, 0.95, 1)
        _DeepColor("Deep Color", Color) = (0.05, 0.25, 0.55, 1)
        _DepthMax("Depth Max", Range(0.1, 40)) = 10
        _FoamColor("Foam Color", Color) = (1, 1, 1, 0.9)
        _FoamDistance("Foam Distance", Range(0, 30)) = 2.5
        _FoamNoiseScale("Foam Noise Scale", Range(0.01, 200)) = 40
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _WaveStrength;
                float _WaveSpeed;
                float _WaveFrequency;
                float _NormalScale;
                float4 _ShallowColor;
                float4 _DeepColor;
                float _DepthMax;
                float4 _FoamColor;
                float _FoamDistance;
                float _FoamNoiseScale;
            CBUFFER_END

            float _CloudShadowIntensity;

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 baseUV : TEXCOORD1;
                float4 screenUV : TEXCOORD2;
            };

            float2 WaterNoiseDir(float2 p)
            {
                p = fmod(p, 289);
                float x = fmod((34 * p.x + 1) * p.x, 289) + p.y;
                x = fmod((34 * x + 1) * x, 289);
                x = frac(x / 41) * 2 - 1;
                return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
            }

            float WaterNoise(float2 uv, float scale)
            {
                float2 p = uv * scale;
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d00 = dot(WaterNoiseDir(ip), fp);
                float d01 = dot(WaterNoiseDir(ip + float2(0, 1)), fp - float2(0, 1));
                float d10 = dot(WaterNoiseDir(ip + float2(1, 0)), fp - float2(1, 0));
                float d11 = dot(WaterNoiseDir(ip + float2(1, 1)), fp - float2(1, 1));
                fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
                return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);

                float waveTime = _Time.y * _WaveSpeed;
                float wave = sin(o.positionWS.x * _WaveFrequency + waveTime)
                           + cos(o.positionWS.z * _WaveFrequency * 1.3 + waveTime * 0.8);
                o.positionWS.y += wave * _WaveStrength;

                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.baseUV = o.positionWS.xz * 0.1 * _BaseMap_ST.xy + _BaseMap_ST.zw;
                o.screenUV = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float4 ndc = i.screenUV / i.screenUV.w;

                float sceneDepthRaw = SampleSceneDepth(ndc.xy);
                float sceneEye = LinearEyeDepth(sceneDepthRaw, _ZBufferParams);
                float surfaceEye = LinearEyeDepth(ndc.z, _ZBufferParams);
                float thickness = max(0.0, sceneEye - surfaceEye);

                float depthFactor = saturate(thickness / max(_DepthMax, 0.001));
                half4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);

                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.baseUV);
                waterColor.rgb = lerp(waterColor.rgb, base.rgb * _BaseColor.rgb, 0.35);

                float foamInset = saturate(1.0 - thickness / max(_FoamDistance, 0.001));
                float foamNoise = WaterNoise(i.positionWS.xz + _Time.y * 0.15, _FoamNoiseScale);
                float foam = step(0.45, foamNoise) * foamInset;
                half3 foamed = lerp(waterColor.rgb, _FoamColor.rgb, foam * _BaseColor.a);

                float waveN = sin(i.positionWS.x * _WaveFrequency + _Time.y * _WaveSpeed);
                float3 N = normalize(float3(waveN * _NormalScale, 1.0, waveN * 0.4 * _NormalScale));
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
                float spec = saturate(dot(reflect(-_MainLightPosition.xyz, N), V));
                foamed += spec * _ShallowColor.rgb * 0.25;

                // Uniform cloud-coverage darkening (global value, not spatial projection).
                foamed *= (1.0 - _CloudShadowIntensity * 0.6);

                return half4(foamed, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
