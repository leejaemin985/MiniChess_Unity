// 캐릭터 툰 셰이딩(셀 셰이딩). 조명 없이 뽑은 색 텍스처(Tripo "조명 제거")를 쓴다.
//   - 명암: 맵의 해(메인 라이트) 방향과 면 방향으로 밝기(하프 램버트 × 그림자 받기)를 구하고, 문턱 두 개로 밝은 면 / 중간 톤 / 그림자 3단계로 끊어 칠한다.
//     중간 톤 색 = 그림자 색으로 두면 2단계. 경계 부드러움(_ShadeSoftness)을 조금 주면 AI 메시의 고르지 않은 얼굴 법선에서도 경계가 덜 지글거린다.
//   - 그림자 색은 단순히 어둡게가 아니라 따로 정한 색을 곱한다(애니풍).
//   - 림 라이트: 시선과 비스듬한 가장자리를 밝게(밝은 면에서만).
//   - 주변광(SH)을 약하게 더해 그림자 면이 새까맣게 죽지 않게 한다. 안개 적용.
//   - 외곽선: 메시를 법선 방향으로 부풀려 뒷면만 그린다(Outline 패스). 굵기(1080p 기준 픽셀)는 기준 거리에서의 값이고, 거리에 반비례해 가늘어진다
//     (캐릭터 크기 대비 선 비율 유지). 사라지는 거리 구간에서 0 으로 줄여 아주 멀리서는 선을 뺀다.
//     부풀리는 방향은 같은 위치 정점들의 평균 법선(CharacterOutlineNormals 가 임포트 때 UV7 에 탄젠트 공간으로 구워 둠)이라 법선이 갈라진 경계에서도 선이 끊기지 않는다.
//     탄젠트 공간 값이라 스킨 애니메이션에도 따라 돈다. UV7 이 없으면 정점 법선을 쓴다.
//   - 발광(_EMISSION 키워드, 켠 머티리얼만): 정점 색 R 을 마스크로 그 영역을 _EmissionColor(HDR)로 명암과 상관없이 칠한다(블룸용).
//     마스크는 임포트 때 구워 둔다(무기 날 끝: WeaponEdgeGlow). 정점 색이 없는 메시에 켜면 전체가 빛난다.
//   - 순간이동 노이즈(_Blink 0~1): 화면 기준 세로 줄무늬마다 무작위로 몸을 지운다(옛 애니 순간이동 느낌). 0 = 멀쩡, 1 = 완전히 사라짐.
//     줄무늬 값은 _BlinkRate 로 계속 바뀌어 지지직거리고, 지워지기 직전 줄은 _BlinkEdgeColor 로 칠하고(기본 검정, HDR 로 두면 번쩍), 일부 줄은 어둡게 눌린다.
//     몸 전체가 좌우로 조금 떨린다(_BlinkJitter, 픽셀). 모든 패스(외곽선/깊이/그림자)가 같은 줄무늬로 지워 구멍이 생기지 않는다.
//     _BLINK 키워드를 켠 변형에서만 동작한다. 재생하는 유닛만 머티리얼을 복제해 키워드를 켜고 _Blink/_BlinkSeed 를 넣으며, 끝나면 원래 머티리얼로 되돌린다
//     (평소 변형에는 clip 이 없어 다른 캐릭터에 비용이 없다).
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
        _OutlineWidth ("Outline Width (px at 1080p, at Ref Distance)", Range(0, 6)) = 1
        _OutlineRefDistance ("Outline Ref Distance", Float) = 8
        _OutlineFadeStart ("Outline Fade Start Distance", Float) = 30
        _OutlineFadeEnd ("Outline Fade End Distance", Float) = 45
        _OutlineColor ("Outline Color", Color) = (0.12, 0.08, 0.1, 1)
        _OutlineAlbedo ("Outline Albedo Mix", Range(0, 1)) = 0.5

        [Header(Emission)]
        [Toggle(_EMISSION)] _EmissionEnabled ("Emission (Vertex Color R Mask)", Float) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        [Header(Blink Noise)]
        _Blink ("Blink (0 보임 ~ 1 사라짐)", Range(0, 1)) = 0
        _BlinkSeed ("Blink Seed", Float) = 0
        _BlinkStripeWidth ("Stripe Width (px at 1080p)", Range(1, 32)) = 5
        _BlinkSegment ("Stripe Segment Length (px at 1080p)", Range(8, 400)) = 90
        _BlinkRate ("Flicker Rate (Hz)", Range(1, 60)) = 24
        _BlinkEdgeWidth ("Edge Width", Range(0, 0.5)) = 0.18
        [HDR] _BlinkEdgeColor ("Edge Color", Color) = (0, 0, 0, 1)
        _BlinkDarken ("Dark Stripes", Range(0, 1)) = 0.55
        _BlinkJitter ("Jitter (px at 1080p)", Range(0, 30)) = 8

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
            float _OutlineRefDistance;
            float _OutlineFadeStart;
            float _OutlineFadeEnd;
            half4 _OutlineColor;
            half _OutlineAlbedo;
            half4 _EmissionColor;
            half _Blink;
            float _BlinkSeed;
            float _BlinkStripeWidth;
            float _BlinkSegment;
            float _BlinkRate;
            half _BlinkEdgeWidth;
            half4 _BlinkEdgeColor;
            half _BlinkDarken;
            float _BlinkJitter;
            half _Cull;
        CBUFFER_END

        // 순간이동 노이즈 ---------------------------------------------------------------
        float BlinkHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        // 화면 픽셀 위치(SV_POSITION.xy)의 줄무늬 값(0~1). 세로 줄(폭 _BlinkStripeWidth)마다, 세로로는 긴 토막(_BlinkSegment)마다 다르고,
        // _BlinkRate 로 시간이 바뀔 때마다 새로 뽑는다. 이 값이 _Blink 보다 작으면 지운다.
        float BlinkNoise(float2 pixel, out float dark)
        {
            float scale = _ScreenParams.y / 1080.0;
            float column = floor(pixel.x / max(_BlinkStripeWidth * scale, 1.0));
            float segment = floor(pixel.y / max(_BlinkSegment * scale, 1.0));
            float tick = floor(_Time.y * _BlinkRate) + _BlinkSeed * 17.0;
            float columnValue = BlinkHash(float2(column, tick));
            float segmentValue = BlinkHash(float2(column + segment * 31.0, tick + 7.0));
            dark = step(0.6, BlinkHash(float2(column + 13.0, tick + 3.0)));
            return columnValue * 0.7 + segmentValue * 0.3;
        }

        // _BLINK 키워드가 꺼진 평소 변형에는 clip 이 아예 없다(모바일 GPU 의 가려진 픽셀 미리 버리기 최적화를 지킨다).
        #if _BLINK
            // 깊이/그림자/외곽선 패스용: 지워질 픽셀이면 버린다.
            void BlinkClip(float2 pixel)
            {
                float dark;
                clip(BlinkNoise(pixel, dark) - _Blink * (1.0 + _BlinkEdgeWidth));
            }

            // 몸 전체를 좌우로 떤다(클립 공간 x 를 픽셀만큼 민다).
            float4 BlinkJitter(float4 positionCS)
            {
                float tick = floor(_Time.y * _BlinkRate) + _BlinkSeed * 17.0;
                float shake = (BlinkHash(float2(tick, 5.0)) - 0.5) * 2.0 * _BlinkJitter * (_ScreenParams.y / 1080.0);
                positionCS.x += shake * 2.0 / _ScreenParams.x * positionCS.w * saturate(_Blink * 3.0);
                return positionCS;
            }
        #else
            void BlinkClip(float2 pixel) { }
            float4 BlinkJitter(float4 positionCS) { return positionCS; }
        #endif
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _BLINK

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _EMISSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                #if _EMISSION
                    half4 color : COLOR;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                #if _EMISSION
                    half emission : TEXCOORD4;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = BlinkJitter(position.positionCS);
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                #if _EMISSION
                    output.emission = input.color.r;
                #endif
                return output;
            }

            half4 frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 순간이동 노이즈: 문턱 아래 줄은 버리고, 문턱 바로 위 줄(edge)은 선 색, 일부 줄은 어둡게.
                half blinkEdge = 0;
                half blinkDark = 0;
                #if _BLINK
                    float dark;
                    float noise = BlinkNoise(input.positionCS.xy, dark);
                    float threshold = _Blink * (1.0 + _BlinkEdgeWidth);
                    clip(noise - threshold);
                    blinkEdge = 1.0 - saturate((noise - threshold) / max(_BlinkEdgeWidth, 0.001));
                    blinkDark = dark * _BlinkDarken * saturate(_Blink * 4.0);
                #endif

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

                // 발광: 마스크 영역을 명암과 상관없이 발광 색으로 칠한다(더하면 밝은 바탕색과 섞여 분홍으로 뜬다). HDR 이면 블룸.
                #if _EMISSION
                    color = lerp(color, _EmissionColor.rgb, input.emission);
                #endif

                color *= 1.0 - blinkDark;
                color = lerp(color, _BlinkEdgeColor.rgb, blinkEdge * blinkEdge);

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
            #pragma multi_compile_local _ _BLINK
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

                // 굵기(픽셀): 기준 거리 값을 거리에 반비례로 줄이고(오브젝트 원점 거리라 몸 전체가 같은 굵기),
                // 사라지는 구간에서 0 까지 줄인다. 1080p 기준값을 해상도에 맞춘다.
                float cameraDistance = length(GetCameraPositionWS() - TransformObjectToWorld(float3(0, 0, 0)));
                float pixels = _OutlineWidth * _OutlineRefDistance / max(cameraDistance, 0.01);
                pixels *= 1.0 - smoothstep(_OutlineFadeStart, max(_OutlineFadeEnd, _OutlineFadeStart + 0.01), cameraDistance);
                pixels *= _ScreenParams.y / 1080.0;

                // 화면에서 법선 방향으로 그 픽셀만큼 민다.
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(smoothOS));
                float2 direction = normalize(normalCS.xy + 1e-5);
                positionCS.xy += direction * pixels * 2.0 / _ScreenParams.xy * positionCS.w;

                output.positionCS = BlinkJitter(positionCS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                BlinkClip(input.positionCS.xy);
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
            #pragma multi_compile_local _ _BLINK
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

            // 그림자 맵 화면은 줄무늬 좌표가 달라 절반쯤 사라졌을 때 그림자를 통째로 뺀다.
            half4 frag() : SV_Target
            {
                #if _BLINK
                    clip(0.5 - _Blink);
                #endif
                return 0;
            }
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
            #pragma multi_compile_local _ _BLINK
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return BlinkJitter(TransformObjectToHClip(input.positionOS.xyz));
            }

            // 색 패스와 같은 줄무늬로 버린다(깊이만 남으면 지워진 자리 뒤 배경이 가려진다).
            half frag(float4 positionCS : SV_POSITION) : SV_Target
            {
                BlinkClip(positionCS.xy);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
