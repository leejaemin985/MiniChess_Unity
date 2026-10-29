// 거대한 성벽(성소 아래로 내려가는 석축, BoardBackdrop). 벽돌을 메시로 만들지 않고 월드 좌표로 벽돌 무늬를 그린다(모바일):
//   - 옆면: 가로 = 면을 따라가는 축(x 또는 z), 세로 = y. 층마다 반 장씩 어긋나게 쌓고(줄눈), 벽돌마다 밝기/색조를 흔든다.
//   - 윗면(턱/망루 꼭대기): xz 로 같은 무늬.
//   - 줄눈 근처는 어둡게, 벽돌 가운데는 조금 밝게(모서리가 둥근 느낌). 돌 텍스처를 얹어 표면 결을 준다.
//   - 위를 향한 면과 줄눈 아래에 이끼, 멀수록/낮을수록 배경 안개(Backdrop 셰이더와 같은 전역 값).
// 정점 색: RGB = 색조(아래로 갈수록 어둡게 등), A = 벽돌 무늬 세기(0 이면 줄눈 바닥처럼 평평한 어두운 면).
Shader "MiniChess/Masonry"
{
    Properties
    {
        _BaseMap ("Stone Albedo (detail)", 2D) = "grey" {}
        _Tiling ("Detail Tiling", Float) = 0.6
        _StoneColor ("Stone Color", Color) = (0.60, 0.56, 0.54, 1)
        _MortarColor ("Mortar Color", Color) = (0.33, 0.30, 0.30, 1)
        _BrickSize ("Brick Size (length, height)", Vector) = (1.25, 0.55, 0, 0)
        _Mortar ("Mortar Width", Range(0.005, 0.08)) = 0.018
        _Variation ("Brick Variation", Range(0, 0.5)) = 0.25
        _MossColor ("Moss Color", Color) = (0.24, 0.30, 0.13, 1)
        _Moss ("Moss Amount", Range(0, 1)) = 0.35
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
                half4 _StoneColor;
                half4 _MortarColor;
                float4 _BrickSize;
                half _Mortar;
                half _Variation;
                half4 _MossColor;
                half _Moss;
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

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

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
                half3 axis = abs(normalWS);
                bool top = axis.y > max(axis.x, axis.z);

                // 벽돌 좌표: 옆면은 (면을 따라가는 축, y), 윗면은 (x, z)
                float2 p = top ? positionWS.xz : float2(axis.x > axis.z ? positionWS.z : positionWS.x, positionWS.y);
                float row = floor(p.y / _BrickSize.y);
                float shift = frac(row * 0.5) + (Hash(float2(row, 7.1)) - 0.5) * 0.3; // 반 장씩 어긋나고, 층마다 조금 흔들림
                float2 brick = float2(p.x / _BrickSize.x + shift, p.y / _BrickSize.y);
                float2 id = floor(brick);
                float2 f = frac(brick);

                // 줄눈까지의 거리(월드 단위)
                float edgeX = min(f.x, 1.0 - f.x) * _BrickSize.x;
                float edgeY = min(f.y, 1.0 - f.y) * _BrickSize.y;
                float edge = min(edgeX, edgeY);
                half mortar = 1.0 - smoothstep(_Mortar * 0.5, _Mortar, edge);
                half bevel = smoothstep(_Mortar, _Mortar * 6.0, edge); // 줄눈에서 벽돌 안쪽으로 밝아짐(모서리가 둥글게 닳은 느낌)

                half variation = Hash(id + 31.7);
                half3 stone = _StoneColor.rgb * (1.0 + (variation - 0.5) * 2.0 * _Variation);
                stone *= lerp(half3(1.02, 0.99, 0.96), half3(0.96, 0.97, 1.02), Hash(id + 5.3)); // 벽돌마다 따뜻/차가운 기
                stone *= 0.78 + 0.22 * bevel;

                // 돌 결(텍스처, 평균 약 0.6 이라 나눠서 밝기 유지)
                float2 uv = top ? positionWS.xz : (axis.x > axis.z ? positionWS.zy : positionWS.xy);
                half3 detail = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _Tiling).rgb / 0.6;
                stone *= detail;

                half pattern = input.color.a;
                half3 albedo = lerp(_MortarColor.rgb * detail, stone, (1.0 - mortar) * pattern) * input.color.rgb;

                // 이끼: 벽돌마다 정한 양으로, 위를 향한 면(턱)과 벽돌 아랫단 줄눈 가까이(습한 곳)에 낀다
                half patches = Hash(id + 9.1);
                half lower = 1.0 - smoothstep(0.0, 0.35, f.y);
                half grow = saturate(normalWS.y) * 0.8 + lower * 0.6;
                half moss = smoothstep(1.0 - _Moss, 1.0, patches * grow + (detail.g - 0.6) * 0.5) * (1.0 - mortar * 0.5);
                albedo = lerp(albedo, _MossColor.rgb * (0.8 + 0.4 * detail.r), moss * pattern);

                Light sun = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half diffuse = saturate(dot(normalWS, sun.direction));
                half3 lit = albedo * (sun.color * diffuse * sun.shadowAttenuation + SampleSH(normalWS));

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
                half4 _StoneColor;
                half4 _MortarColor;
                float4 _BrickSize;
                half _Mortar;
                half _Variation;
                half4 _MossColor;
                half _Moss;
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
