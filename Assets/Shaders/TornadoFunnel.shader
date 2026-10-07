// Dark, streaky funnel wall that spins around the tornado. UVs wrap around the
// funnel in U and run from the ground to the cloud base in V.
Shader "TornadoChaser/TornadoFunnel"
{
    Properties
    {
        _StreakTex ("Streak Texture", 2D) = "gray" {}
        _LightColor ("Streak Color", Color) = (0.45, 0.45, 0.45, 1)
        _DarkColor ("Base Color", Color) = (0.06, 0.06, 0.07, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.35
        _Contrast ("Streak Contrast", Float) = 1.8
        _TilingU ("Streak Repeats Around (whole number)", Float) = 2
        _TilingV ("Streak Repeats Up", Float) = 3
        _SpinSpeed ("Spin Speed", Float) = 0.25
        _Twist ("Twist", Float) = 0.6
        _FogStrength ("Fog Strength", Range(0, 1)) = 0.35
        _FadeBottom ("Bottom Fade (fraction of height)", Range(0.01, 0.5)) = 0.04
        _FadeTop ("Top Fade (fraction of height)", Range(0.01, 0.5)) = 0.1
        _Raggedness ("Ragged Edges", Range(0, 1)) = 0
        _MinDensity ("Minimum Density (opacity of dark streaks)", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Funnel"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _LightningFlash;  // global, set by LightningController

            TEXTURE2D(_StreakTex);
            SAMPLER(sampler_StreakTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _StreakTex_ST;
                half4 _LightColor;
                half4 _DarkColor;
                half _Opacity;
                half _EdgeSoftness;
                half _Contrast;
                float _TilingU;
                float _TilingV;
                float _SpinSpeed;
                float _Twist;
                half _FogStrength;
                half _FadeBottom;
                half _FadeTop;
                half _Raggedness;
                half _MinDensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                Varyings output;
                output.positionCS = position.positionCS;
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(position.positionWS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float v = input.uv.y;
                // A whole number of repeats around the funnel keeps the texture seamless.
                float repeats = max(1.0, round(_TilingU));

                // Two layers spinning at different speeds make the streaks churn.
                float2 uv1 = float2((input.uv.x + t * _SpinSpeed) * repeats + v * _Twist, v * _TilingV);
                float2 uv2 = float2((input.uv.x + t * _SpinSpeed * 1.7) * repeats * 2.0 + v * _Twist * 2.0 + 0.37,
                                    v * _TilingV * 2.3 + 0.21);
                half streaks = SAMPLE_TEXTURE2D(_StreakTex, sampler_StreakTex, uv1).r * 0.6
                             + SAMPLE_TEXTURE2D(_StreakTex, sampler_StreakTex, uv2).r * 0.4;
                streaks = saturate((streaks - 0.5) * _Contrast + 0.5);

                half3 normal = normalize(input.normalWS);
                half lighting = 0.55 + 0.45 * saturate(dot(normal, GetMainLight().direction));
                half3 color = lerp(_DarkColor.rgb, _LightColor.rgb, streaks) * lighting;
                color += _LightningFlash * half3(0.3, 0.32, 0.38) * (0.4 + streaks);

                // Wispy silhouette: edges thin out, more so where the streaks are dark.
                half facing = saturate(dot(normal, normalize(input.viewDirWS)));
                half edge = smoothstep(0.0, _EdgeSoftness, facing);
                // Fade out at the top and bottom, breaking the edge up along the streaks.
                half fadeV = v + (streaks - 0.5) * _Raggedness;
                half heightFade = smoothstep(0.0, _FadeBottom, fadeV) * (1.0 - smoothstep(1.0 - _FadeTop, 1.0, fadeV));
                half alpha = _Opacity * heightFade * saturate(edge * lerp(_MinDensity, 1.4, streaks));

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
