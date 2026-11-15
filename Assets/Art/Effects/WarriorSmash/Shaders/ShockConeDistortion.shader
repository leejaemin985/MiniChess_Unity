// 부채꼴 충격파 왜곡(파티클 Mesh = Quad, 바닥과 나란히 눕혀 쓴다). 쿼드 중심에서 +V(앞) 방향 부채꼴 안의 링만 그린다.
//   - 링 반지름은 파티클 크기(Size over Lifetime)로 키우고, 세기는 정점 색 알파로 줄인다.
//   - 노이즈로 링을 강하게 끊고(_NoiseAmount), 뒤 배경을 일그러뜨리며(_Strength), 무채색 명암 결(_NoiseVisible)과 바깥 가장자리 먹색(_DarkEdge)을 얹는다.
//   Opaque Texture(URP 설정 또는 카메라의 Opaque Texture)가 켜져 있어야 한다.
Shader "MiniChess/ShockConeDistortion"
{
    Properties
    {
        _NoiseTex ("Noise (RG)", 2D) = "gray" {}
        _Strength ("Distortion Strength", Range(0, 0.1)) = 0.04
        _Radius ("Ring Radius (UV)", Range(0.05, 0.5)) = 0.42
        _Width ("Ring Width (UV)", Range(0.01, 0.4)) = 0.1
        _Spread ("Spread (부채꼴 반각, 도)", Range(5, 90)) = 40
        _NoiseAmount ("Noise Breakup", Range(0, 1)) = 0.85
        _NoiseVisible ("Noise Visible (무채색 명암)", Range(0, 0.6)) = 0.3
        _DarkEdge ("Dark Edge (바깥 먹색)", Range(0, 1)) = 0.25
        [HDR] _Tint ("Tint (옅은 색, 알파 = 세기)", Color) = (0.55, 0.62, 0.85, 0.15)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ShockConeDistortion"
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
                float _Strength;
                float _Radius;
                float _Width;
                float _Spread;
                float _NoiseAmount;
                float _NoiseVisible;
                float _DarkEdge;
                half4 _Tint;
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
                float2 p = input.uv - 0.5;
                float dist = length(p);
                float angle = degrees(atan2(p.x, p.y)); // 0 = 앞(+V)

                // 부채꼴 안, 링 반지름 근처만. 가장자리 각도는 부드럽게.
                float cone = 1.0 - smoothstep(_Spread * 0.6, _Spread, abs(angle));
                float offsetR = (dist - _Radius) / _Width;
                float ring = saturate(1.0 - abs(offsetR));
                ring *= ring;

                // 노이즈: 각도 방향 결(바람 줄기)과 바깥으로 흐르는 결.
                float2 nuv = float2(angle / _Spread * 1.3, dist * 3.0 - _Time.y * 1.5);
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nuv).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nuv * float2(3.0, 0.7) + 0.37).g;
                half noise = saturate(n1 * 0.6 + n2 * 0.6);
                half breakup = lerp(1.0, smoothstep(0.3, 0.75, noise) * 1.6, _NoiseAmount);

                half mask = ring * cone * breakup * input.color.a;

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 offset = (half2(n1, n2) * 2.0 - 1.0) * _Strength * mask;
                half3 scene = SampleSceneColor(saturate(screenUV + offset));

                // 무채색 결 + 옅은 색 + 바깥 가장자리 먹색.
                scene *= 1.0 + (noise - 0.5) * 2.0 * _NoiseVisible * mask;
                scene = lerp(scene, scene * 0.4 + _Tint.rgb * 0.6, _Tint.a * mask);
                half outer = saturate(offsetR) * ring * cone * input.color.a;
                scene *= 1.0 - _DarkEdge * outer;

                return half4(scene, saturate(mask * 4.0 + outer * _DarkEdge * 2.0));
            }
            ENDHLSL
        }
    }
}
