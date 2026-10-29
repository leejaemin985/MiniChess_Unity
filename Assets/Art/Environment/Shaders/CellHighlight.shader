// 칸 하이라이트(선택/이동/공격/스킬). 판 두 종류를 같은 셰이더로 그린다(_Curtain 으로 고름, 칸마다 MaterialPropertyBlock).
//   - 바닥 판(_Curtain = 0): 칸 위에 눕힌 유리 같은 판. 가운데가 흰빛인 가는 테두리 선 + 바깥 번짐, 안쪽은 테두리에서
//     가운데로 갈수록 연해지는 빛과 옅은 채움(_Fill), 대각선 빛 반사, 가끔 반짝이는 작은 점.
//   - 세로 막(_Curtain = 1): 하이라이트 영역의 바깥 경계에 세운 판. 아랫단(테두리와 이어지는 흰빛 띠)이 가장 밝고,
//     위로 갈수록 연해져 윗단은 불꽃처럼 들쭉날쭉하게 사라진다. 안은 오로라 같은 세로 줄무늬가 천천히 일렁이고
//     작은 빛 알갱이가 올라간다.
// 텍스처 없이 수식(값 노이즈)으로만 그린다. 테두리/아랫단은 1 보다 밝게 내보내 블룸으로 번지게 한다.
// 미리 곱한 알파로 섞어 밝은 돌 위에서도 빛나 보이게 한다(Blend One OneMinusSrcAlpha).
Shader "MiniChess/CellHighlight"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.3, 0.95, 0.85, 1)
        _Border ("Border Intensity", Range(0, 4)) = 1
        _Fill ("Fill", Range(0, 1)) = 0.2
        _Curtain ("Curtain (0 = floor, 1 = wall)", Float) = 0
        _Corner ("Corner Radius", Range(0, 0.5)) = 0.14
        _Inset ("Border Inset", Range(0.5, 1)) = 0.92
        _Pulse ("Pulse", Range(0, 0.5)) = 0.06
        _Aspect ("Curtain Height / Width", Float) = 0.65
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "CellHighlight"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Border;
                half _Fill;
                half _Curtain;
                half _Corner;
                half _Inset;
                half _Pulse;
                float _Aspect;
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
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 0~1 값 노이즈(부드러운 보간)
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

            // 격자 칸마다 하나씩 놓인 작은 빛 점(일부 칸만). 반지름 radius(격자 칸 비율), 켜지는 비율 1 - threshold.
            float Sparkles(float2 g, float radius, float threshold)
            {
                float2 id = floor(g);
                float2 f = frac(g);
                float2 spot = float2(Hash(id + 3.1), Hash(id + 7.7)) * 0.7 + 0.15;
                return smoothstep(radius, 0.0, length(f - spot)) * step(threshold, Hash(id));
            }

            half4 Floor(float2 uv, float3 positionWS, float t)
            {
                // 둥근 사각형까지의 거리(안쪽 음수). p 는 칸 중심 기준 -1~1.
                float2 p = (uv - 0.5) * 2.0;
                float2 q = abs(p) - (_Inset - _Corner);
                float sd = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _Corner;
                float inside = 1.0 - smoothstep(-0.02, 0.01, sd);
                float pulse = 1.0 + _Pulse * sin(t * 2.4);

                // 테두리: 흰빛 가운데 선 + 색 번짐(바깥으로 조금 더 넓게)
                float core = exp(-abs(sd) * 70.0);
                float edge = exp(-abs(sd) * 26.0);
                float halo = exp(-max(sd, 0.0) * 9.0) * (1.0 - inside) * 0.5;

                // 안쪽: 테두리에서 가운데로 연해지는 빛 + 옅은 채움 + 대각선 빛 반사 + 아주 느린 얼룩
                float innerGlow = exp(-max(-sd, 0.0) * 5.5) * inside;
                float sheen = smoothstep(0.35, 0.0, abs(p.x + p.y - 0.6 + 0.25 * sin(t * 0.4))) * 0.35 * inside;
                float mottle = 0.8 + 0.4 * Noise(positionWS.xz * 3.0 + t * 0.15);
                float fill = inside * _Fill * mottle;

                // 판 위의 작은 반짝임(깜빡임)
                float twinkle = Sparkles(positionWS.xz * 9.0, 0.08, 0.86) * (0.5 + 0.5 * sin(t * 3.0 + Hash(floor(positionWS.xz * 9.0)) * 6.28)) * inside;

                half3 tint = _Color.rgb;
                half3 white = lerp(tint, half3(1, 1, 1), 0.5);
                half3 rgb = white * core * 2.2 * _Border * pulse
                          + tint * (edge * 1.4 * _Border * pulse + halo * _Border)
                          + tint * (innerGlow * 0.3 * _Border + fill * 0.8 + sheen * _Fill * 0.8)
                          + white * twinkle * 0.8;
                half alpha = saturate(fill * 0.7 + innerGlow * 0.1 + edge * 0.5 * _Border + sheen * _Fill * 0.5);
                return half4(rgb, alpha);
            }

            half4 Curtain(float2 uv, float3 positionWS, float t)
            {
                // 이웃 막과 무늬가 이어지도록 가로 위치는 월드 좌표(막이 선 방향의 x 또는 z 합)로 잡는다.
                float u = positionWS.x + positionWS.z;
                float y = uv.y;

                // 위로 흐르며 좌우로 흔들리는 일렁임
                float sway = (Noise(float2(u * 3.0, y * 2.0 - t * 0.6)) - 0.5) * 0.25;
                float x = u + sway * y;

                // 오로라 같은 세로 줄무늬(굵은 줄 + 가는 줄)
                float streaks = Noise(float2(x * 9.0, t * 0.35)) * 0.6 + Noise(float2(x * 23.0, t * 0.6 + 4.0)) * 0.4;
                streaks = 0.35 + 0.9 * streaks;

                // 윗단: 불꽃처럼 들쭉날쭉한 높이에서 사라진다
                float top = 0.74 + 0.26 * Noise(float2(x * 4.0, t * 0.5));
                float fade = pow(saturate(1.0 - y / top), 1.25);

                // 아랫단: 테두리와 이어지는 흰빛 띠
                float base = exp(-y * 40.0);
                float baseGlow = exp(-y * 9.0) * 0.6;

                // 위로 올라가는 작은 빛 알갱이(세로/가로 비율을 맞춰 동그랗게)
                float dots = Sparkles(float2(x * 8.0, y * 8.0 * _Aspect - t * 0.4), 0.1, 0.8) * saturate(1.0 - y) * (0.4 + 0.6 * fade);

                float body = fade * streaks;
                half3 tint = _Color.rgb;
                half3 white = lerp(tint, half3(1, 1, 1), 0.5);
                half3 rgb = (tint * (body * 2.3 + baseGlow) + white * (base * 2.2 + dots * 1.6)) * _Border;
                half alpha = saturate(body * 0.85 + baseGlow * 0.4 + base * 0.5);
                return half4(rgb, alpha);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                return _Curtain > 0.5 ? Curtain(input.uv, input.positionWS, t) : Floor(input.uv, input.positionWS, t);
            }
            ENDHLSL
        }
    }
}
