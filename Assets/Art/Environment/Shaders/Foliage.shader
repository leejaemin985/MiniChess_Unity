// 풀(줄눈/벽 둘레의 작은 덤불). 텍스처 없이 잎 메시의 정점 색으로 칠한다(FoliageMeshBuilder):
//   RGB = 잎 색(뿌리 어둡게 → 끝 밝게), A = 뿌리에서 끝까지 0 → 1(바람에 흔들리는 정도).
// 햇빛(그림자 받음) + 주변광, 얇은 잎이라 뒤에서 오는 빛도 조금 비친다. 양면으로 그리고 그림자는 드리우지 않는다(작고 많아서).
// 바람: 월드 위치에 따라 박자가 다른 사인 두 개로 끝 쪽만 흔든다.
Shader "MiniChess/Foliage"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _WindStrength ("Wind Strength", Range(0, 0.05)) = 0.012
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.6
        _Translucency ("Back Light", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _WindStrength;
                half _WindSpeed;
                half _Translucency;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                float t = _Time.y * _WindSpeed;
                float bend = input.color.a * input.color.a;
                float2 wind = float2(sin(t + positionWS.x * 1.7 + positionWS.z * 0.9), sin(t * 1.3 + positionWS.z * 1.9 + 1.7) * 0.6);
                positionWS.xz += wind * (_WindStrength * bend);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS) * (frontFace ? 1.0 : -1.0);
                half3 albedo = input.color.rgb * _Tint.rgb;

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = sun.shadowAttenuation;
                half facing = dot(normalWS, sun.direction);
                half diffuse = saturate(facing * 0.6 + 0.4);           // 잎은 넓게 감싸듯 밝게
                half back = saturate(-facing) * _Translucency;          // 뒤에서 비치는 빛

                half3 lit = albedo * (sun.color * shadow * (diffuse + back) * 0.85 + SampleSH(half3(0, 1, 0)));
                lit = MixFog(lit, input.fog);
                return half4(lit, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
