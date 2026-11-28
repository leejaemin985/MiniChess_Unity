// 무기 잔상(방패 등). 가산 혼합으로 가장자리(프레넬)가 밝게 빛나고 안쪽은 옅게 채운다.
//   세기(_Alpha)는 스크립트가 MaterialPropertyBlock 으로 시간에 따라 줄인다. 양면을 그린다.
Shader "MiniChess/WeaponGhostRim"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.45, 0.75, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 2.3
        _RimPower ("Rim Power (가장자리 폭, 클수록 얇다)", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 4)) = 1.5
        _Fill ("Fill (안쪽 채움)", Range(0, 1)) = 0.35
        _Alpha ("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WeaponGhostRim"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _RimPower;
                half _RimStrength;
                half _Fill;
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
                float3 viewWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 양면이라 법선 방향과 상관없이 시선과 이루는 각만 본다.
                half facing = abs(dot(normalize(input.normalWS), normalize(input.viewWS)));
                half rim = pow(1.0h - saturate(facing), _RimPower);
                half3 color = _Color.rgb * _Intensity * (_Fill + rim * _RimStrength) * _Alpha;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
