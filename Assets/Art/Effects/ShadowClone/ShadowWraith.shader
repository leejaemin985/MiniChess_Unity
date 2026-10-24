// 그림자 분신(낫 캐릭터 일러스트의 보라 그림자 형체): 짙은 보라 반투명 몸 + 가장자리 보라 빛 + 안에서 피어오르는 연기 무늬,
// 아래로 갈수록 연기처럼 흐려져 풀린다. 높이 기준(_BottomY/_TopY, 월드)은 UnitBody 가 몸체 범위로 넣어 준다(모델/캡슐 공용).
// _FillHeight(월드 Y) 위는 그리지 않는다: 바닥에서 솟아오르며 형체를 갖추는 연출(UnitBody.Appear)에 쓴다. 기본값이면 영향 없음.
Shader "MiniChess/ShadowWraith"
{
    Properties
    {
        _BaseColor("Body Color", Color) = (0.07, 0.02, 0.12, 0.82)
        [HDR] _RimColor("Rim Color", Color) = (0.75, 0.28, 1.0, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 2.4
        _RimIntensity("Rim Intensity", Range(0, 10)) = 2.2
        [HDR] _SmokeColor("Smoke Color", Color) = (0.55, 0.15, 0.9, 1)
        _SmokeScale("Smoke Scale", Float) = 3.2
        _SmokeSpeed("Smoke Speed", Float) = 0.6
        _SmokeIntensity("Smoke Intensity", Range(0, 5)) = 1.2
        _FadeStart("Bottom Fade Start (0-1 height)", Range(0, 1)) = 0.02
        _FadeEnd("Bottom Fade End (0-1 height)", Range(0, 1)) = 0.45
        _BottomY("Bottom Y (World)", Float) = 0
        _TopY("Top Y (World)", Float) = 1
        _FillHeight("Fill Height (World Y)", Float) = 100000
        [HDR] _FillEdgeColor("Fill Edge Color", Color) = (1.2, 0.5, 2.0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ShadowWraith"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half4 _SmokeColor;
                float _SmokeScale;
                float _SmokeSpeed;
                half _SmokeIntensity;
                half _FadeStart;
                half _FadeEnd;
                float _BottomY;
                float _TopY;
                float _FillHeight;
                half4 _FillEdgeColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash13(i), Hash13(i + float3(1, 0, 0)), u.x), lerp(Hash13(i + float3(0, 1, 0)), Hash13(i + float3(1, 1, 0)), u.x), u.y),
                    lerp(lerp(Hash13(i + float3(0, 0, 1)), Hash13(i + float3(1, 0, 1)), u.x), lerp(Hash13(i + float3(0, 1, 1)), Hash13(i + float3(1, 1, 1)), u.x), u.y),
                    u.z);
            }

            float Fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll] for (int octave = 0; octave < 3; octave++)
                {
                    value += amplitude * ValueNoise(p);
                    p = p * 2.07 + 13.1;
                    amplitude *= 0.5;
                }
                return value / 0.875;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 피어오르는 연기 무늬(월드 기준이라 크기가 달라도 무늬 굵기가 같다)
                float3 smokePos = input.positionWS * _SmokeScale + float3(0, -_Time.y * _SmokeSpeed, 0);
                float smoke = Fbm(smokePos + Fbm(smokePos * 0.7 + 2.3) * 1.4);

                // 솟아오르는 경계: 위는 그리지 않는다
                float fill = _FillHeight - input.positionWS.y + (smoke - 0.5) * 0.1;
                clip(fill);
                float fillEdge = 1.0 - smoothstep(0.0, 0.08, fill);

                // 아래로 갈수록 연기로 풀려 흐려짐
                float height = saturate((input.positionWS.y - _BottomY) / max(_TopY - _BottomY, 0.01));
                float fade = smoothstep(_FadeStart, _FadeEnd, height + (smoke - 0.5) * 0.35);

                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim = pow(1.0 - saturate(dot(normal, view)), _RimPower);

                half3 color = _BaseColor.rgb
                    + _RimColor.rgb * rim * _RimIntensity
                    + _SmokeColor.rgb * pow(smoke, 3.0) * _SmokeIntensity
                    + _FillEdgeColor.rgb * fillEdge;
                half alpha = saturate(_BaseColor.a + rim * 0.4 + fillEdge) * fade;
                clip(alpha - 0.01);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
