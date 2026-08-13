Shader "JayFos/Foliage/WindFoliage"
{
    Properties
    {
        _BaseMap ("Base Map (RGB), Alpha (A)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.2, 0.6, 0.15, 1)
        _WindStrength ("Wind Sway Strength", Range(0, 2)) = 0.5
        _WindSpeed ("Wind Speed", Range(0.5, 5)) = 1.5
        _WindDirection ("Wind Direction (XY)", Vector) = (1, 0, 0, 0)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "WindFoliageForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _WindStrength;
                float _WindSpeed;
                float4 _WindDirection;
                float _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;       // R = height influence (taper)
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float3 normalWS     : TEXCOORD1;
                float3 positionWS   : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldDir(v.normalOS);

                // Vertex displacement: wind sway
                // R channel = height influence (1.0 = top, 0.0 = base) — set by foliage prefab vertex data.
                float heightFactor = saturate(v.color.r * 2.0);

                // Two phase-shifted sine/cosine waves for natural lateral sway.
                float windOffset = sin(_Time.y * _WindSpeed + positionWS.y * 0.5) * _WindStrength * heightFactor;
                float windOffsetZ = cos(_Time.y * _WindSpeed + positionWS.y * 0.3) * _WindStrength * heightFactor * 0.5;

                // Wind direction (XY) modulates the sway axis.
                float dirLength = max(length(_WindDirection.xy), 1e-4);
                float dirX = _WindDirection.x / dirLength;
                float dirZ = _WindDirection.y / dirLength;

                positionWS.x += windOffset * dirX;
                positionWS.z += windOffsetZ * dirZ;

                o.positionWS = positionWS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.positionCS = TransformWorldToHClip(positionWS);

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half alpha = tex.a;
                clip(alpha - _Cutoff);
                return half4(tex.rgb * _BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}