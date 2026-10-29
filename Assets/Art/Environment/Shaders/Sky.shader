// 하늘 스카이박스(BoardArt.skyMaterial). 텍스처 없이 방향만으로 그린다:
//   지평선 → 위 하늘 → 천정 세로 그라데이션, 지평선 아래는 안개 색, 해 쪽 번짐(+작은 해), 천천히 흐르는 구름.
// 지평선 색은 배경 안개 색(_BackdropHazeColor, BoardArt 가 전역으로 넣음)을 써서 먼 산/숲이 하늘에 자연스럽게 묻힌다.
// 전역 값이 없으면(에디터 미리보기 등) _HorizonColor 를 쓴다. 나중에 그린 스카이박스로 바꾸려면 머티리얼만 바꾸면 된다.
Shader "MiniChess/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.20, 0.21, 0.40, 1)
        _UpperColor ("Upper Sky", Color) = (0.40, 0.36, 0.58, 1)
        _HorizonColor ("Horizon (fallback)", Color) = (0.62, 0.52, 0.64, 1)
        _HorizonBand ("Horizon Band", Range(0.02, 1)) = 0.28
        _SunGlowColor ("Sun Glow", Color) = (1.0, 0.70, 0.50, 1)
        _SunGlowSize ("Sun Glow Size", Range(1, 64)) = 5
        _SunGlowStrength ("Sun Glow Strength", Range(0, 2)) = 0.55
        _SunDisc ("Sun Disc", Range(0, 4)) = 1.5
        _CloudColor ("Cloud", Color) = (0.86, 0.74, 0.80, 1)
        _CloudShade ("Cloud Shade", Color) = (0.46, 0.40, 0.56, 1)
        _Clouds ("Cloud Amount", Range(0, 1)) = 0.45
        _CloudScale ("Cloud Scale", Float) = 1.6
        _CloudSpeed ("Cloud Speed", Float) = 0.04
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

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _UpperColor;
                half4 _HorizonColor;
                half _HorizonBand;
                half4 _SunGlowColor;
                half _SunGlowSize;
                half _SunGlowStrength;
                half _SunDisc;
                half4 _CloudColor;
                half4 _CloudShade;
                half _Clouds;
                float _CloudScale;
                float _CloudSpeed;
            CBUFFER_END

            // BoardArt.ApplyLighting 이 넣는 전역 값(a = 1 이면 설정됨)
            half4 _BackdropHazeColor;

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

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                return Noise(p) * 0.55 + Noise(p * 2.1 + 3.7) * 0.3 + Noise(p * 4.3 + 7.1) * 0.15;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                half3 horizon = _BackdropHazeColor.a > 0.5 ? _BackdropHazeColor.rgb : _HorizonColor.rgb;
                half up = saturate(direction.y);

                // 세로 그라데이션: 지평선 → 위 하늘 → 천정. 지평선 아래는 안개 색(조금 어둡게)
                half3 sky = lerp(horizon, _UpperColor.rgb, smoothstep(0.0, _HorizonBand, up));
                sky = lerp(sky, _ZenithColor.rgb, smoothstep(_HorizonBand, 1.0, up));
                half3 below = horizon * lerp(1.0, 0.85, saturate(-direction.y * 3.0));
                sky = lerp(below, sky, smoothstep(-0.02, 0.02, direction.y));

                // 해 쪽 번짐과 작은 해
                half sun = saturate(dot(direction, _MainLightPosition.xyz));
                half glow = pow(sun, _SunGlowSize) * _SunGlowStrength;
                sky += _SunGlowColor.rgb * (glow + pow(sun, 600.0) * _SunDisc);

                // 구름: 하늘을 높은 평면에 투영해 흐르는 노이즈. 지평선 근처와 천정에서는 옅게.
                float2 cloudUV = direction.xz / (up + 0.12) * _CloudScale + _Time.y * _CloudSpeed;
                half density = Fbm(cloudUV);
                half cloud = smoothstep(1.0 - _Clouds, 1.0 - _Clouds + 0.3, density);
                cloud *= smoothstep(0.0, 0.12, direction.y) * (1.0 - 0.5 * smoothstep(0.5, 0.95, up));
                half3 cloudColor = lerp(_CloudShade.rgb, _CloudColor.rgb, saturate(Fbm(cloudUV * 1.7 + 11.0) * 1.4 - 0.2));
                cloudColor += _SunGlowColor.rgb * glow * 0.8;
                sky = lerp(sky, cloudColor, cloud * 0.85);

                return half4(sky, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
