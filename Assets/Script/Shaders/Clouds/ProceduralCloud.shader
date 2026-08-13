Shader "JayFos/Clouds/ProceduralCloud"
{
    Properties
    {
        _CloudColor("Cloud Color", Color) = (0.95, 0.97, 1, 1)
        _CloudShadowColor("Shadow Color", Color) = (0.6, 0.65, 0.75, 1)
        _ShadowIntensity("Cloud Shadow Multiplier", Range(0, 1)) = 0
        _Opacity("Opacity", Range(0, 1)) = 0.85
        _SoftEdge("Soft Edge", Range(0, 1)) = 0.5
        _NoiseScale("Noise Scale", Range(0.01, 1)) = 0.3
        _HeightFade("Height Fade", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+0"
        }

        Pass
        {
            Name "CloudForward"
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

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudColor;
                float4 _CloudShadowColor;
                float _ShadowIntensity;
                float _Opacity;
                float _SoftEdge;
                float _NoiseScale;
                float _HeightFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            float hash(float2 p)
            {
                float h = dot(p, float2(127.1, 311.7));
                return frac(sin(h) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 noiseUV = i.positionWS.xz * _NoiseScale;
                float n = fbm(noiseUV);

                float edgeDist = min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y));
                float edgeFade = smoothstep(0.0, _SoftEdge, edgeDist);

                float heightFade = 1.0 - abs(i.uv.y - 0.5) * 2.0 * _HeightFade;
                heightFade = saturate(heightFade);

                float alpha = n * edgeFade * heightFade * _Opacity;

                float3 lightDir = normalize(_MainLightPosition.xyz);
                float ndotl = dot(float3(0, 1, 0), lightDir) * 0.5 + 0.5;
                float3 cloudLit = lerp(_CloudShadowColor.rgb, _CloudColor.rgb, ndotl);
                cloudLit *= (1.0 - _ShadowIntensity);

                return half4(cloudLit, alpha);
            }
            ENDHLSL
        }
    }
}
