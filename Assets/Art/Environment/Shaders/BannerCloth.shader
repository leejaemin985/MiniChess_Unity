// 바람에 흔들리는 매달린 천(깃발/현수막, 외부 모델을 BannerClothBuilder 로 정리). 스프링본/애니메이션 없이 정점 셰이더로만 움직인다(CPU 비용 0).
//   - 메시 기준: 원점 = 천 윗변 중앙(매다는 곳), 천은 -Y 로 늘어지고 앞뒤가 ±Z. 원점에서 아래로 내려갈수록(_Length 기준 비율의 제곱) 크게 움직인다
//     → 윗변은 거의 고정, 아랫단이 가장 많이 흔들린다.
//   - 흔들림: 느린 앞뒤 흔들림(두 사인파를 섞어 일정하지 않게) + 위에서 아래로 흘러내리는 물결 + 약한 좌우 흔들림 + 바람에 밀린 기울기.
//     천마다 위치로 위상을 달리해 여러 장이 따로 움직인다. 물결은 정점이 있는 곳에서만 휘므로 메시 분할이 촘촘할수록 부드럽다.
//   - 법선: 움직인 모양에 맞게 기울여(가까운 두 점의 차이) 접힌 면의 명암이 같이 움직인다.
//   - 양면: 뒷면은 법선을 뒤집어 그린다. 그림자/깊이 패스도 같은 흔들림을 적용한다.
// URP PBR 조명(그림자, 주변광, 안개). 모바일 기준: 텍스처 샘플 3번(색, 노멀, 거칠기).
Shader "MiniChess/BannerCloth"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Albedo", 2D) = "white" {}
        [NoScaleOffset][Normal] _NormalMap ("Normal", 2D) = "bump" {}
        [NoScaleOffset] _RoughnessMap ("Roughness (R)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _Smoothness ("Smoothness Scale", Range(0, 1)) = 0.35

        [Header(Wind)]
        _Length ("Cloth Length (object units, set by builder)", Float) = 1
        _SwayAmount ("Sway (fraction of length)", Range(0, 0.5)) = 0.08
        _SwaySpeed ("Sway Speed", Range(0, 5)) = 1.2
        _WaveAmount ("Wave (fraction of length)", Range(0, 0.2)) = 0.025
        _WaveFrequency ("Wave Count (along length)", Range(0, 4)) = 1.5
        _WaveSpeed ("Wave Speed", Range(0, 8)) = 2.6
        _SideAmount ("Side Sway (fraction of length)", Range(0, 0.2)) = 0.02
        _Lean ("Wind Lean (fraction of length, +Z)", Range(-0.3, 0.3)) = 0.04
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _NormalStrength;
            half _Smoothness;
            float _Length;
            float _SwayAmount;
            float _SwaySpeed;
            float _WaveAmount;
            float _WaveFrequency;
            float _WaveSpeed;
            float _SideAmount;
            float _Lean;
        CBUFFER_END

        // 한 점이 바람에 밀린 양(오브젝트 공간). phase = 천마다 다른 위상
        float3 ClothOffset(float3 positionOS, float phase)
        {
            float drop = saturate(-positionOS.y / max(_Length, 0.0001));
            float weight = drop * drop;
            float t = _Time.y;

            float sway = sin(t * _SwaySpeed + phase) * 0.7 + sin(t * _SwaySpeed * 2.27 + phase * 1.31) * 0.3;
            float wave = sin(t * _WaveSpeed - drop * _WaveFrequency * 6.2832 + positionOS.x * 4.0 + phase);
            float side = sin(t * _SwaySpeed * 0.63 + phase * 0.7);

            float push = _Lean + sway * _SwayAmount + wave * _WaveAmount;
            return float3(side * _SideAmount, 0, push) * (weight * _Length);
        }

        float ClothPhase()
        {
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            return dot(origin, float3(1.7, 0.0, 2.3));
        }

        // 흔들린 위치와, 그 모양에 맞게 기울인 법선
        void ApplyWind(inout float3 positionOS, inout float3 normalOS)
        {
            float phase = ClothPhase();
            float delta = 0.02 * _Length;
            float3 offset = ClothOffset(positionOS, phase);
            float3 offsetX = ClothOffset(positionOS + float3(delta, 0, 0), phase);
            float3 offsetY = ClothOffset(positionOS + float3(0, delta, 0), phase);

            // 앞뒤(Z)로 밀린 양의 기울기만큼 Z 를 향한 법선을 눕힌다
            float2 slope = float2(offsetX.z - offset.z, offsetY.z - offset.z) / delta;
            normalOS = normalize(normalOS - float3(slope, 0) * normalOS.z);
            positionOS += offset;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);    SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half4 fogAndVertexLight : TEXCOORD4; // x = 안개, yzw = 정점 보조광
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                ApplyWind(positionOS, normalOS);

                VertexPositionInputs position = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normal = GetVertexNormalInputs(normalOS, input.tangentOS);

                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                output.fogAndVertexLight.x = ComputeFogFactor(position.positionCS.z);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    output.fogAndVertexLight.yzw = VertexLighting(position.positionWS, normal.normalWS);
                #endif
                return output;
            }

            half4 frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _Tint.rgb;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalStrength);
                half roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).r;

                float sgn = input.tangentWS.w;
                half3 vertexNormal = input.normalWS * (frontFace ? 1.0 : -1.0); // 뒷면도 바르게
                half3 bitangentWS = sgn * cross(vertexNormal, input.tangentWS.xyz);
                half3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, vertexNormal));
                normalWS = NormalizeNormalPerPixel(normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogAndVertexLight.x;
                inputData.vertexLighting = input.fogAndVertexLight.yzw;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0;
                surface.smoothness = saturate((1.0 - roughness) * _Smoothness);
                surface.normalTS = normalTS;
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1);
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
                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                ApplyWind(positionOS, normalOS);

                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
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

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                ApplyWind(positionOS, normalOS);
                return TransformObjectToHClip(positionOS);
            }

            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
