// 무기 궤적(WeaponTrail) 왜곡 셰이더(전사). 색 없이 뒤 배경만 살짝 일그러뜨리고 무채색 노이즈 결을 옅게 얹어 "휘두른 바람"만 느끼게 한다.
//   UV: x = 나이(0 = 방금 휘두른 자리, 1 = 사라질 때), y = 0 날 안쪽 ~ 1 날 끝. 정점 색 알파 = 그 순간의 세기.
//   Opaque Texture(URP 설정 또는 카메라의 Opaque Texture)가 켜져 있어야 한다.
Shader "MiniChess/WeaponTrailDistortion"
{
    Properties
    {
        _NoiseTex ("Noise (RG)", 2D) = "gray" {}
        _NoiseTiling ("Noise Tiling (xy) / Scroll (zw)", Vector) = (2, 1, -3, 0)
        _Strength ("Distortion Strength", Range(0, 0.05)) = 0.012
        _Brighten ("Brighten (아주 옅은 밝힘)", Range(0, 0.3)) = 0.04
        _NoiseVisible ("Noise Visible (무채색 노이즈 결 명암)", Range(0, 0.5)) = 0.12
        _TipBias ("Tip Bias (클수록 날 끝에만)", Range(0.2, 6)) = 1.2
        _TailPower ("Tail Power (클수록 꼬리가 빨리 사라짐)", Range(0.3, 6)) = 1.5
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "TrailDistortion"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                float4 _NoiseTiling;
                float _Strength;
                float _Brighten;
                float _NoiseVisible;
                float _TipBias;
                float _TailPower;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float age = saturate(input.uv.x);
                float across = saturate(input.uv.y);

                // 세기: 날 끝 쪽, 막 지나간 앞머리가 가장 세고 꼬리로 갈수록 약해진다. 안쪽/끝 가장자리는 부드럽게.
                half edge = smoothstep(0.0, _EdgeSoftness, across) * smoothstep(1.0, 1.0 - _EdgeSoftness, across);
                half mask = pow(across, _TipBias) * pow(1.0 - age, _TailPower) * edge * input.color.a;

                // 뒤로 흐르는 노이즈로 화면 좌표를 밀어 배경을 일그러뜨린다.
                float2 noiseUV = float2(age * _NoiseTiling.x, across * _NoiseTiling.y) + _Time.y * _NoiseTiling.zw;
                half2 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).rg;
                float2 offset = (noise * 2.0 - 1.0) * _Strength * mask;

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 scene = SampleSceneColor(saturate(screenUV + offset));
                // 무채색 노이즈 결: 색 없이 밝게/어둡게만 흔들어 바람 결이 보이게 한다.
                scene *= 1.0 + (noise.r - 0.5) * 2.0 * _NoiseVisible * mask;
                scene += _Brighten * mask;
                return half4(scene, saturate(mask * 4.0));
            }
            ENDHLSL
        }
    }
}
