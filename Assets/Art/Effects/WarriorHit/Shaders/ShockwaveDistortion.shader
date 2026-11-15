// 충격파 왜곡(파티클 빌보드용). 색 없이 링 모양으로 뒤 배경을 바깥쪽으로 밀어 일그러뜨리고, 무채색 노이즈 결을 옅게 얹는다.
//   링 크기는 파티클 크기(Size over Lifetime)로 키우고, 세기는 정점 색 알파(Color over Lifetime)로 줄인다.
//   Opaque Texture(URP 설정 또는 카메라의 Opaque Texture)가 켜져 있어야 한다.
Shader "MiniChess/ShockwaveDistortion"
{
    Properties
    {
        _NoiseTex ("Noise (RG)", 2D) = "gray" {}
        _Strength ("Distortion Strength", Range(0, 0.08)) = 0.02
        _Radius ("Ring Radius (UV)", Range(0.05, 0.5)) = 0.36
        _Width ("Ring Width (UV)", Range(0.01, 0.4)) = 0.12
        _NoiseAmount ("Noise Breakup (링을 노이즈로 끊기)", Range(0, 1)) = 0.6
        _NoiseVisible ("Noise Visible (무채색 노이즈 결 명암)", Range(0, 0.5)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ShockwaveDistortion"
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
                float _NoiseAmount;
                float _NoiseVisible;
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
                float2 dir = dist > 1e-4 ? p / dist : float2(0, 0);

                // 링: 반지름 _Radius 근처만 세다. 노이즈로 군데군데 끊어 바람이 터지는 결을 낸다.
                float ring = saturate(1.0 - abs(dist - _Radius) / _Width);
                ring *= ring;
                float angle = atan2(p.y, p.x) / (2.0 * PI) + 0.5;
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(angle * 3.0, dist * 2.0 + _Time.y * 0.5)).r;
                half mask = ring * lerp(1.0, noise * 1.6, _NoiseAmount) * input.color.a;

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 scene = SampleSceneColor(saturate(screenUV + dir * _Strength * mask));
                scene *= 1.0 + (noise - 0.5) * 2.0 * _NoiseVisible * mask;
                return half4(scene, saturate(mask * 4.0));
            }
            ENDHLSL
        }
    }
}
