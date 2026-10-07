// Storm skybox: dark overhead, matching the fog at the horizon, with streaky
// clouds drifting across a flat cloud layer.
Shader "TornadoChaser/StormSky"
{
    Properties
    {
        _CloudTex ("Cloud Texture", 2D) = "gray" {}
        _HorizonColor ("Horizon Color (match fog)", Color) = (0.36, 0.4, 0.4, 1)
        _ZenithColor ("Zenith Color", Color) = (0.1, 0.11, 0.12, 1)
        _CloudScale ("Cloud Scale", Float) = 0.15
        _CloudContrast ("Cloud Contrast", Range(0, 1)) = 0.6
        _WindSpeed ("Wind Speed", Float) = 0.01
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _LightningFlash;  // global, set by LightningController

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudTex_ST;
                half4 _HorizonColor;
                half4 _ZenithColor;
                float _CloudScale;
                half _CloudContrast;
                float _WindSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float up = saturate(direction.y);
                half3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, smoothstep(0.0, 0.45, up));

                // Project onto a flat cloud layer overhead, fading out toward the horizon.
                float2 uv = direction.xz / max(up, 0.08) * _CloudScale + _Time.y * _WindSpeed * float2(1.0, 0.3);
                half clouds = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv).r * 0.65
                            + SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv * 2.7 + 0.31).r * 0.35;
                half cloudAmount = smoothstep(0.02, 0.3, up) * _CloudContrast;
                color *= lerp(1.0, lerp(0.55, 1.35, clouds), cloudAmount);
                // Lightning lights the clouds overhead. At the horizon it matches the fog's boost.
                color += _LightningFlash * half3(0.18, 0.19, 0.22) * lerp(1.0, 1.0 + clouds * 2.0, cloudAmount);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
