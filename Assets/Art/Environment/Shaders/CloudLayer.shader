// 섬 아래에 깔리는 구름바다 한 층(크게 눕힌 판 한 장). 높이를 달리해 2~3장 겹쳐 깔면 깊이감이 생긴다(모바일: 볼류메트릭/파티클 대신).
//   - 무늬: 구름 노이즈(T_SkyClouds, R = 큰 덩어리, G = 잔 명암)를 월드 xz 로 두 크기로 읽어 서로 다른 방향/속도로 천천히 흘린다.
//     둘을 섞은 밀도가 _Coverage 보다 높은 곳만 구름이 된다(_Softness 로 가장자리를 부드럽게).
//   - 색: 덩어리 윗면(밀도 높은 곳)은 _LightColor, 얇은 곳은 _ShadeColor. 멀리서는 거리 안개 색으로 묻힌다.
//   - 판 가장자리: _Center 에서 _FadeRadius.x ~ .y 사이에서 사라진다(판의 네모난 끝이 보이지 않게).
//   - 부드러운 경계(_SOFT_EDGES): 바위 기둥/섬 벽이 구름을 뚫고 나오는 곳을 화면 깊이로 흐리게 한다(URP Depth Texture 필요).
//     끄면 깊이 텍스처 없이 그리지만 맞닿는 곳에 선이 생긴다.
// 투명(알파 블렌드), 깊이 쓰기 없음. 텍스처 샘플 2번(+ 깊이 1번).
Shader "MiniChess/CloudLayer"
{
    Properties
    {
        [NoScaleOffset] _NoiseMap ("Cloud Noise (R = billows, G = detail)", 2D) = "gray" {}
        _LightColor ("Light (thick tops)", Color) = (0.78, 0.72, 0.82, 1)
        _ShadeColor ("Shade (thin parts)", Color) = (0.52, 0.47, 0.60, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _Coverage ("Coverage (higher = fewer clouds)", Range(0, 1)) = 0.42
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.18

        [Header(Pattern)]
        _Scale ("Scale (world m per tile)", Float) = 40
        _DetailScale ("Detail Scale (fraction of Scale)", Range(0.1, 1)) = 0.35
        _Speed ("Drift (xz, m/s)", Vector) = (0.25, 0.08, 0, 0)
        _DetailSpeed ("Detail Drift (xz, m/s)", Vector) = (-0.12, 0.2, 0, 0)

        [Header(Fade)]
        _Center ("Fade Center (world xz)", Vector) = (3, 3, 0, 0)
        _FadeRadius ("Fade Radius (start, end)", Vector) = (30, 55, 0, 0)

        [Header(Soft Edges)]
        [Toggle(_SOFT_EDGES)] _SoftEdges ("Soft Edges (needs Depth Texture)", Float) = 1
        _SoftDistance ("Soft Distance (m)", Range(0.1, 10)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _SOFT_EDGES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if defined(_SOFT_EDGES)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _LightColor;
                half4 _ShadeColor;
                half _Opacity;
                half _Coverage;
                half _Softness;
                float _Scale;
                float _DetailScale;
                float4 _Speed;
                float4 _DetailSpeed;
                float4 _Center;
                float4 _FadeRadius;
                float _SoftDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.screenPosition = ComputeScreenPos(position.positionCS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 world = input.positionWS.xz;
                float t = _Time.y;

                // 큰 덩어리 + 잔 무늬를 다른 방향/속도로 흘린다
                float2 uvBillow = (world + _Speed.xy * t) / _Scale;
                float2 uvDetail = (world + _DetailSpeed.xy * t) / (_Scale * _DetailScale);
                half billow = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uvBillow).r;
                half detail = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uvDetail).g;

                // 노이즈 값은 대략 0.2 ~ 0.8 → 0 ~ 1 로 펴서 문턱과 비교
                half density = saturate(((billow * 0.7 + detail * 0.3) - 0.2) / 0.6);
                half cloud = smoothstep(_Coverage, _Coverage + _Softness, density);

                // 판의 네모난 끝이 보이지 않게 중심에서 멀어질수록 사라진다
                half fade = 1.0 - smoothstep(_FadeRadius.x, _FadeRadius.y, distance(world, _Center.xy));
                half alpha = cloud * fade * _Opacity;

                #if defined(_SOFT_EDGES)
                    // 바위/섬 벽과 맞닿는 곳: 화면 깊이와 이 판 깊이의 차이가 작을수록 옅게
                    float2 screenUV = input.screenPosition.xy / input.screenPosition.w;
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float surfaceDepth = -TransformWorldToView(input.positionWS).z;
                    alpha *= saturate((sceneDepth - surfaceDepth) / _SoftDistance);
                #endif

                // 두꺼운 덩어리일수록 밝게(햇빛 받는 윗면)
                half3 color = lerp(_ShadeColor.rgb, _LightColor.rgb, saturate((density - _Coverage) * 2.0 + billow * 0.3));
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
