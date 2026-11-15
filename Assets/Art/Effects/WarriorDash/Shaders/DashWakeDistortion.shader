// 돌진 경로 왜곡(바닥과 나란히 눕힌 띠). UV x = 경로 방향(0 = 출발, 1 = 도착), y = 폭.
//   - _Head(0~1): 돌진 머리 위치. 머리가 지나간 곳만 보이고, 머리 쪽이 가장 세며 꼬리 쪽으로 약해진다.
//   - _Fade(0~1): 전체 세기(도착 뒤 서서히 0 으로).
//   색 없이 뒤 배경을 일그러뜨리고(_Strength), 무채색 명암 결(_NoiseVisible)과 옅은 먹색(_Dark)을 얹는다.
//   Opaque Texture(URP 설정 또는 카메라의 Opaque Texture)가 켜져 있어야 한다.
Shader "MiniChess/DashWakeDistortion"
{
    Properties
    {
        _NoiseTex ("Noise (RG)", 2D) = "gray" {}
        _Head ("Head (0~1)", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _TailLength ("Tail Length (경로 대비)", Range(0.05, 1.5)) = 0.9
        _Strength ("Distortion Strength", Range(0, 0.08)) = 0.03
        _NoiseVisible ("Noise Visible (무채색 명암)", Range(0, 0.6)) = 0.3
        _Dark ("Dark (먹색)", Range(0, 1)) = 0.25
        _EdgeSoftness ("Edge Softness", Range(0.05, 1)) = 0.6
        _NoiseScale ("Noise Scale (경로, 폭)", Vector) = (3, 1.5, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "DashWakeDistortion"
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
                float _Head;
                float _Fade;
                float _TailLength;
                float _Strength;
                float _NoiseVisible;
                float _Dark;
                float _EdgeSoftness;
                float4 _NoiseScale;
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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float along = input.uv.x;
                float across = abs(input.uv.y - 0.5) * 2.0;

                // 머리가 지나간 곳만. 머리 쪽이 가장 세고 꼬리로 갈수록 약해진다. 폭 가장자리는 부드럽게.
                float behind = _Head - along;
                float tail = saturate(1.0 - behind / _TailLength) * step(0.0, behind);
                float head = smoothstep(0.0, 0.04, behind);
                float edge = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, across);
                float shape = tail * head * edge * _Fade;
                clip(shape - 0.001);

                // 경로를 따라 뒤로 흐르는 노이즈.
                float2 nuv = float2(along * _NoiseScale.x + _Time.y * 2.0, input.uv.y * _NoiseScale.y);
                half2 n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nuv).rg;
                half breakup = smoothstep(0.25, 0.7, n.r);
                half mask = shape * lerp(0.35, 1.0, breakup);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 scene = SampleSceneColor(saturate(screenUV + (n * 2.0 - 1.0) * _Strength * mask));
                scene *= 1.0 + (n.g - 0.5) * 2.0 * _NoiseVisible * mask;
                scene *= 1.0 - _Dark * mask * (1.0 - breakup * 0.5);
                return half4(scene, saturate(mask * 4.0));
            }
            ENDHLSL
        }
    }
}
