// The rotating storm cloud layer the tornado feeds into. UVs: U wraps around
// the tornado, V runs from the funnel (0) out to the deck's edge (1). Spiral
// arms of cloud wind inward, spinning faster near the funnel.
Shader "TornadoChaser/StormCloudDeck"
{
    Properties
    {
        _CloudTex ("Cloud Texture", 2D) = "gray" {}
        _DarkColor ("Dark Color", Color) = (0.05, 0.055, 0.06, 1)
        _LightColor ("Light Color", Color) = (0.3, 0.32, 0.34, 1)
        _Contrast ("Contrast", Float) = 1.6
        _Repeats ("Repeats Around (whole number)", Float) = 6
        _RadialTiling ("Radial Tiling", Float) = 3
        _Swirl ("Spiral Tightness", Float) = 2.5
        _SpinSpeed ("Spin Speed", Float) = 0.015
        _CenterDarkening ("Center Darkening", Range(0, 1)) = 0.5
        _EdgeFade ("Outer Edge Fade", Range(0.01, 1)) = 0.3
        _FogStrength ("Fog Strength", Range(0, 1)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudDeck"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _LightningFlash;  // global, set by LightningController

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudTex_ST;
                half4 _DarkColor;
                half4 _LightColor;
                half _Contrast;
                float _Repeats;
                float _RadialTiling;
                float _Swirl;
                float _SpinSpeed;
                half _CenterDarkening;
                half _EdgeFade;
                half _FogStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y;
                float repeats = max(1.0, round(_Repeats));
                // Inner clouds circle faster than outer ones, like the funnel.
                float spin = _Time.y * _SpinSpeed * (1.6 - v);

                float2 arms = float2((u + spin) * repeats + v * _Swirl, v * _RadialTiling);
                float2 clumps = float2((u + spin * 0.7) * 3.0 + v * _Swirl * 0.5 + 0.43, v * 1.2 + 0.17);
                half detail = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, arms).r * 0.45
                            + SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, arms * 2.3 + 0.61).r * 0.2
                            + SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, clumps).r * 0.35;
                detail = saturate((detail - 0.5) * _Contrast + 0.5);

                half3 color = lerp(_DarkColor.rgb, _LightColor.rgb, detail);
                color *= lerp(1.0 - _CenterDarkening, 1.0, smoothstep(0.0, 0.45, v));
                color += _LightningFlash * half3(0.25, 0.27, 0.32) * (0.3 + detail);

                half alpha = smoothstep(0.0, 0.06, v) * (1.0 - smoothstep(1.0 - _EdgeFade, 1.0, v))
                           * lerp(0.8, 1.0, detail);

                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    half visibility = lerp(1.0, ComputeFogIntensity(input.fogFactor), _FogStrength);
                    color = lerp(unity_FogColor.rgb, color, visibility);
                #endif

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
