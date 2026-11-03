// 캐릭터 툰 셰이딩(셀 셰이딩). 조명 없이 뽑은 색 텍스처(Tripo "조명 제거")를 쓴다.
//   - 명암: 맵의 해(메인 라이트) 방향과 면 방향으로 밝기(하프 램버트 × 그림자 받기)를 구하고, 문턱 두 개로 밝은 면 / 중간 톤 / 그림자 3단계로 끊어 칠한다.
//     중간 톤 색 = 그림자 색으로 두면 2단계. 경계 부드러움(_ShadeSoftness)을 조금 주면 AI 메시의 고르지 않은 얼굴 법선에서도 경계가 덜 지글거린다.
//   - 그림자 색은 단순히 어둡게가 아니라 따로 정한 색을 곱한다(애니풍).
//   - 림 라이트: 시선과 비스듬한 가장자리를 밝게(밝은 면에서만).
//   - 주변광(SH)을 약하게 더해 그림자 면이 새까맣게 죽지 않게 한다. 안개 적용.
//   - 외곽선: 메시를 법선 방향으로 부풀려 뒷면만 그린다(Outline 패스). 굵기는 화면 픽셀 기준(1080p 기준값을 해상도에 맞춰)이라 카메라 거리와 상관없이 일정하다.
//     부풀리는 방향은 같은 위치 정점들의 평균 법선(CharacterOutlineNormals 가 임포트 때 UV7 에 탄젠트 공간으로 구워 둠)이라 법선이 갈라진 경계에서도 선이 끊기지 않는다.
//     탄젠트 공간 값이라 스킨 애니메이션에도 따라 돈다. UV7 이 없으면 정점 법선을 쓴다.
// 모바일 기준: 텍스처 샘플 1번(외곽선 1번 더), 보조광은 쓰지 않는다. 그림자 캐스트/깊이 패스 포함(SRP Batcher 호환).
Shader "MiniChess/CharacterToon"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)

        [Header(Shading)]
        _LitThreshold ("Lit Threshold (밝은 면 / 중간)", Range(0, 1)) = 0.55
        _ShadowThreshold ("Shadow Threshold (중간 / 그림자)", Range(0, 1)) = 0.35
        _ShadeSoftness ("Edge Softness", Range(0.001, 0.2)) = 0.02
        _MidColor ("Mid Tone Color", Color) = (0.86, 0.82, 0.9, 1)
        _ShadowColor ("Shadow Color", Color) = (0.58, 0.48, 0.66, 1)
        _LightColorInfluence ("Light Hue Influence", Range(0, 1)) = 0.6
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.25

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 0.92, 0.88, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.25
        _RimPower ("Rim Power", Range(1, 8)) = 4

        [Header(Outline)]
        _OutlineWidth ("Outline Width (px at 1080p)", Range(0, 6)) = 1.5
        _OutlineColor ("Outline Color", Color) = (0.12, 0.08, 0.1, 1)
        _OutlineAlbedo ("Outline Albedo Mix", Range(0, 1)) = 0.5

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _LitThreshold;
            half _ShadowThreshold;
            half _ShadeSoftness;
            half4 _MidColor;
            half4 _ShadowColor;
            half _LightColorInfluence;
            half _AmbientStrength;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half _OutlineWidth;
            half4 _OutlineColor;
            half _OutlineAlbedo;
            half _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS * (frontFace ? 1.0 : -1.0));
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // 밝기: 하프 램버트(0~1) × 그림자 받기. 문턱 두 개로 3단계.
                half brightness = (dot(normalWS, mainLight.direction) * 0.5 + 0.5) * mainLight.shadowAttenuation;
                half lit = smoothstep(_LitThreshold - _ShadeSoftness, _LitThreshold + _ShadeSoftness, brightness);
                half mid = smoothstep(_ShadowThreshold - _ShadeSoftness, _ShadowThreshold + _ShadeSoftness, brightness);
                half3 shade = lerp(_ShadowColor.rgb, _MidColor.rgb, mid);
                shade = lerp(shade, half3(1, 1, 1), lit);

                // 빛은 색조만 반영한다(강도는 빼서 밝은 면 = 텍스처 원래 색). 맵 해 강도(2 이상)에 하얗게 날아가지 않게.
                half3 lightHue = mainLight.color / max(max(mainLight.color.r, mainLight.color.g), max(mainLight.color.b, 0.001));
                half3 lightColor = lerp(half3(1, 1, 1), lightHue, _LightColorInfluence);
                half3 color = albedo * shade * lightColor;
                color += albedo * SampleSH(normalWS) * _AmbientStrength;

                // 림: 밝은 면 가장자리만
                half rim = pow(1.0 - saturate(dot(normalWS, viewWS)), _RimPower) * lit;
                color += _RimColor.rgb * rim * _RimStrength;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float3 smoothNormalTS : TEXCOORD7; // CharacterOutlineNormals 가 구운 평균 법선(탄젠트 공간)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // 평균 법선: 탄젠트 공간 → 오브젝트 공간(스킨된 법선/탄젠트 기준). 구운 값이 없으면 정점 법선.
                float3 bitangentOS = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w;
                float3 smoothOS = input.smoothNormalTS.x * input.tangentOS.xyz + input.smoothNormalTS.y * bitangentOS + input.smoothNormalTS.z * input.normalOS;
                if (dot(input.smoothNormalTS, input.smoothNormalTS) < 0.25)
                    smoothOS = input.normalOS;

                // 화면에서 법선 방향으로 일정한 픽셀만큼 민다(카메라 거리와 무관). 1080p 기준 굵기를 해상도에 맞춘다.
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(smoothOS));
                float2 direction = normalize(normalCS.xy + 1e-5);
                float pixels = _OutlineWidth * (_ScreenParams.y / 1080.0);
                positionCS.xy += direction * pixels * 2.0 / _ScreenParams.xy * positionCS.w;

                output.positionCS = positionCS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 color = _OutlineColor.rgb * lerp(half3(1, 1, 1), albedo, _OutlineAlbedo);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
