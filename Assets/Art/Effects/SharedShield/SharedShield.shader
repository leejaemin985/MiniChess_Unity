// 공용 실드 막. 무늬 없이 가장자리만 은은하게 빛나는(프레넬) 가산 투명 막.
// 실드 출처와 상관없이 실드가 남아 있는 동안 유닛을 감싼다. 색은 _Color 로 바꾼다.
// _Alpha(전체 세기)는 SharedShield 스크립트가 MaterialPropertyBlock 으로 움직인다(나타남/사라짐).
Shader "MiniChess/SharedShield"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimGlow ("Rim Glow", Range(0, 4)) = 1.4
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.08
        _Fill ("Center Fill", Range(0, 0.5)) = 0.03
        _BottomFade ("Bottom Fade", Range(0.001, 0.5)) = 0.15
        _PulseSpeed ("Pulse Speed", Range(0, 4)) = 1.2
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.12
        _Alpha ("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SharedShield"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _RimPower;
                half _RimGlow;
                half _EdgeSoftness;
                half _Fill;
                half _BottomFade;
                half _PulseSpeed;
                half _PulseAmount;
                half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float heightOS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positions.positionWS);
                output.heightOS = input.positionOS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                half facing = saturate(dot(normal, viewDir));

                // 가장자리로 갈수록 밝아지되, 윤곽 바로 위는 살짝 투명하게 눌러 메시의 다각형 윤곽이 드러나지 않게 한다.
                half rim = pow(1.0 - facing, _RimPower) * smoothstep(0.0, _EdgeSoftness, facing);

                // 아주 느린 숨쉬기 같은 밝기 변화.
                half pulse = 1.0 + sin(_Time.y * _PulseSpeed * 6.2831853) * _PulseAmount;

                // 바닥에 닿는 아래 끝(물체 공간 y = -0.5)은 서서히 사라지게 해 바닥과 잘리는 선을 숨긴다.
                half bottom = smoothstep(-0.5, -0.5 + _BottomFade, input.heightOS);

                half intensity = (_Fill + rim * _RimGlow) * pulse * bottom;
                return half4(_Color.rgb * intensity, saturate(intensity) * _Alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
