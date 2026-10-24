// 보호막 구체. 가장자리일수록 밝게 빛나는(프레넬) 가산 투명 막 + 아래에서 위로 훑고 지나가는 빛 띠.
// _Alpha(전체 세기)와 _Sweep(빛 띠 위치, 0 = 아래 끝, 1 = 위 끝)은 이펙트 스크립트가 MaterialPropertyBlock 으로 움직인다.
Shader "MiniChess/ShieldBubble"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.4, 1.6, 1.1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _Fill ("Center Fill", Range(0, 1)) = 0.08
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Sweep ("Sweep Position", Range(-0.2, 1.2)) = -0.2
        _SweepWidth ("Sweep Width", Range(0.01, 0.5)) = 0.12
        _SweepStrength ("Sweep Strength", Range(0, 4)) = 1.5
        _CellScale ("Cell Pattern Scale", Range(1, 20)) = 7
        _CellStrength ("Cell Pattern Strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ShieldBubble"
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
                half _Fill;
                half _Alpha;
                half _Sweep;
                half _SweepWidth;
                half _SweepStrength;
                half _CellScale;
                half _CellStrength;
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
                float3 positionOS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positions.positionWS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            // 육각 격자에 가까운 세포 무늬: 가장 가까운 격자점까지의 거리로 경계선을 만든다.
            half CellEdge(float3 p)
            {
                float2 uv = float2(atan2(p.z, p.x) / 6.2831853 * 2.0, p.y) * _CellScale;
                uv.x += floor(uv.y) * 0.5;
                float2 f = frac(uv) - 0.5;
                float d = max(abs(f.x), abs(f.y));
                return smoothstep(0.38, 0.5, d);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                half rim = pow(1.0 - saturate(dot(normal, viewDir)), _RimPower);

                // 물체 공간 y(-0.5 ~ 0.5)를 0 ~ 1 로 바꿔 빛 띠 위치와 비교한다.
                half height = input.positionOS.y + 0.5;
                half sweep = saturate(1.0 - abs(height - _Sweep) / _SweepWidth);
                sweep *= sweep;

                half cell = CellEdge(input.positionOS) * _CellStrength * (0.4 + rim);

                half intensity = _Fill + rim + cell + sweep * _SweepStrength;
                return half4(_Color.rgb * intensity, saturate(intensity) * _Alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
