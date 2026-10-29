// 보드 바깥 배경(절벽, 숲, 먼 산; BoardBackdrop). 정점 색으로 칠하고 가까운 절벽에만 돌 텍스처를 얹는다:
//   RGB = 색, A = 돌 텍스처를 얹는 정도(월드 좌표로 투영, 면 방향에 따라 xz / zy / xy 평면).
// 햇빛(그림자 받음) + 주변광. 멀수록, 그리고 보드보다 낮을수록 안개 색에 묻혀 멀게 보인다(BoardArt 가 넣는 전역 값):
//   _BackdropHazeColor = 안개 색(하늘 지평선 색과 같음, a = 1 이면 설정됨)
//   _BackdropHaze = (안개 시작 거리, 완전히 묻히는 거리, 절벽 깊이, 절벽 바닥의 낮은 안개 진하기)
Shader "MiniChess/Backdrop"
{
    Properties
    {
        _BaseMap ("Stone Albedo (detail)", 2D) = "grey" {}
        _Tiling ("Detail Tiling", Float) = 0.45
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.8
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Tiling;
                half _DetailStrength;
                half4 _Tint;
            CBUFFER_END

            half4 _BackdropHazeColor;
            float4 _BackdropHaze;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                float3 positionWS = input.positionWS;

                // 돌 텍스처: 면이 향한 축에 맞춰 평면 투영(돌 알베도 평균이 약 0.6 이라 나눠서 밝기를 유지)
                half3 axis = abs(normalWS);
                float2 uv = axis.y > max(axis.x, axis.z) ? positionWS.xz : (axis.x > axis.z ? positionWS.zy : positionWS.xy);
                half3 detail = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _Tiling).rgb / 0.6;
                half3 albedo = input.color.rgb * _Tint.rgb * lerp(1.0, detail, input.color.a * _DetailStrength);

                Light sun = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half diffuse = saturate(dot(normalWS, sun.direction));
                half3 lit = albedo * (sun.color * diffuse * sun.shadowAttenuation + SampleSH(normalWS));

                // 안개: 거리(먼 산일수록) + 높이(절벽 아래일수록)
                if (_BackdropHazeColor.a > 0.5)
                {
                    float distance = length(positionWS - _WorldSpaceCameraPos);
                    half far = saturate((distance - _BackdropHaze.x) / max(_BackdropHaze.y - _BackdropHaze.x, 0.001));
                    half low = saturate(-positionWS.y / max(_BackdropHaze.z, 0.001)) * _BackdropHaze.w;
                    half haze = 1.0 - (1.0 - far) * (1.0 - low);
                    lit = lerp(lit, _BackdropHazeColor.rgb, haze);
                }

                return half4(lit, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Tiling;
                half _DetailStrength;
                half4 _Tint;
            CBUFFER_END

            float4 vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
