// 무기 궤적 리본(WeaponTrail) 셰이더. 빛나는 색(Glow)과 어두운 연기(Dark)를 한 번에 그린다.
//   UV: x = 나이(0 = 방금 휘두른 자리, 1 = 사라질 때), y = 0 날 안쪽 ~ 1 날 끝. 정점 색 알파 = 그 순간의 세기.
//   - Glow: 날 끝 쪽과 막 지나간 앞머리가 가장 밝고, 결 텍스처가 뒤로 흐르며 나이가 들수록 노이즈로 부서진다(가산).
//   - Dark: 날 안쪽과 꼬리 쪽에 깔리는 검은 연기(알파로 뒤를 어둡게 한다). 빛보다 조금 오래 남는다.
//   블렌드는 One / OneMinusSrcAlpha: rgb 는 더해지고(빛), 알파만큼 뒤가 어두워진다(연기). 그래서 검은색과 빛나는 색이 한 장에 섞인다.
// 색만 바꾸면 다른 무기/팀 색으로 돌려쓸 수 있다.
Shader "MiniChess/WeaponTrail"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (1.6, 0.45, 3.2, 1)
        [HDR] _CoreColor ("Core Color (앞머리 날 선)", Color) = (2.4, 1.6, 3.6, 1)
        _DarkColor ("Dark Color (연기, 알파 = 진하기)", Color) = (0.03, 0.0, 0.06, 0.85)

        _StreakTex ("Streak (A, 결)", 2D) = "white" {}
        _StreakTiling ("Streak Tiling (xy) / Scroll (z)", Vector) = (1.2, 1, 1.5, 0)
        _NoiseTex ("Noise (R, 부서짐)", 2D) = "grey" {}
        _NoiseTiling ("Noise Tiling (xy) / Scroll (zw)", Vector) = (2.5, 1.2, -2.0, 0)

        _TipBias ("Glow Tip Bias (클수록 날 끝에만)", Range(0.2, 6)) = 1.8
        _GlowTail ("Glow Tail Power (클수록 꼬리가 빨리 사라짐)", Range(0.3, 6)) = 1.6
        _DarkTail ("Dark Tail Power", Range(0.1, 6)) = 0.8
        _DarkInner ("Dark Inner Bias (클수록 날 안쪽에만)", Range(0, 4)) = 0.6
        _CoreWidth ("Core Width (앞머리 날 선 길이, 나이)", Range(0.01, 0.5)) = 0.08
        _Dissolve ("Dissolve (나이에 따른 부서짐)", Range(0, 2)) = 1.1
        _DissolveSoftness ("Dissolve Softness", Range(0.01, 1)) = 0.25
        _EdgeSoftness ("Edge Softness (안쪽/끝 가장자리)", Range(0.01, 0.5)) = 0.12
        _Intensity ("Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WeaponTrail"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_StreakTex); SAMPLER(sampler_StreakTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half4 _CoreColor;
                half4 _DarkColor;
                float4 _StreakTex_ST;
                float4 _StreakTiling;
                float4 _NoiseTex_ST;
                float4 _NoiseTiling;
                float _TipBias;
                float _GlowTail;
                float _DarkTail;
                float _DarkInner;
                float _CoreWidth;
                float _Dissolve;
                float _DissolveSoftness;
                float _EdgeSoftness;
                float _Intensity;
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
                float t = _Time.y;

                // 결과 부서짐: 결은 뒤로 흐르고(나이 방향), 노이즈는 나이가 들수록 더 많이 깎아 낸다.
                half streak = SAMPLE_TEXTURE2D(_StreakTex, sampler_StreakTex, float2(age * _StreakTiling.x - t * _StreakTiling.z, across * _StreakTiling.y)).a;
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(age * _NoiseTiling.x + t * _NoiseTiling.z, across * _NoiseTiling.y + t * _NoiseTiling.w)).r;
                half dissolve = smoothstep(age * _Dissolve - _DissolveSoftness, age * _Dissolve + _DissolveSoftness, noise);

                // 날 안쪽/끝 가장자리를 부드럽게.
                half edge = smoothstep(0.0, _EdgeSoftness, across) * smoothstep(1.0, 1.0 - _EdgeSoftness * 0.5, across);

                half tip = pow(across, _TipBias);
                half glowFade = pow(1.0 - age, _GlowTail);
                half core = saturate(1.0 - age / _CoreWidth) * tip;
                half3 glow = (_GlowColor.rgb * (tip * glowFade * lerp(0.55, 1.0, streak)) + _CoreColor.rgb * core) * dissolve * edge;

                half darkFade = pow(1.0 - age, _DarkTail);
                half inner = pow(1.0 - across * 0.85, _DarkInner);
                half darkA = _DarkColor.a * darkFade * inner * edge * lerp(0.6, 1.0, noise) * dissolve;

                half strength = input.color.a;
                half3 rgb = (glow * _Intensity + _DarkColor.rgb * darkA) * strength;
                return half4(rgb, saturate(darkA * strength));
            }
            ENDHLSL
        }
    }
}
