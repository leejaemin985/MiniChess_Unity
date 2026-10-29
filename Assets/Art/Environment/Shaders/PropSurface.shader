// 외부에서 가져온 소품(Meshy 등, PropImporter 로 정리). 에셋 자체 텍스처(색, 노멀, 거칠기)를 그대로 쓰되 맵 분위기에 맞춰 색을 보정한다:
//   - 돌/잎 구분: 색 텍스처의 채도와 색상(노랑~초록이면 잎)으로 나눈다.
//   - 돌: 채도를 낮추고(_StoneSaturation) 맵 돌 색조/밝기로. 잎: 밝기 변화만 살려 맵 잎 색(_LeafDark → _LeafLight)으로 다시 칠한다.
//   - 이끼: 위를 향한 면에 맵 돌과 같은 이끼 얼룩(돌 마스크 텍스처 A, 월드 xz 로 투영)을 덮는다.
// URP PBR 조명(그림자, 주변광, 안개). 모바일 기준: 텍스처 샘플 4번(색, 노멀, 거칠기, 이끼 얼룩).
Shader "MiniChess/PropSurface"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Albedo", 2D) = "white" {}
        [NoScaleOffset][Normal] _NormalMap ("Normal", 2D) = "bump" {}
        [NoScaleOffset] _RoughnessMap ("Roughness (R)", 2D) = "white" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _Smoothness ("Smoothness Scale", Range(0, 1)) = 0.5

        [Header(Stone)]
        _StoneTint ("Stone Tint", Color) = (1, 0.98, 1, 1)
        _StoneSaturation ("Stone Saturation", Range(0, 1)) = 0.6
        _StoneBrightness ("Stone Brightness", Range(0, 2)) = 1.1

        [Header(Leaves)]
        _LeafDetect ("Leaf Detect (saturation threshold)", Range(0, 1)) = 0.25
        _LeafDark ("Leaf Dark", Color) = (0.29, 0.41, 0.25, 1)
        _LeafLight ("Leaf Light", Color) = (0.53, 0.63, 0.35, 1)
        _LeafRecolor ("Leaf Recolor", Range(0, 1)) = 0.85

        [Header(Moss)]
        [NoScaleOffset] _MossMap ("Moss Patches (A)", 2D) = "black" {}
        _MossTiling ("Moss Tiling", Float) = 0.8
        _MossColor ("Moss Color", Color) = (0.32, 0.37, 0.16, 1)
        _Moss ("Moss Amount", Range(0, 1)) = 0.32

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _NormalStrength;
            half _Smoothness;
            half4 _StoneTint;
            half _StoneSaturation;
            half _StoneBrightness;
            half _LeafDetect;
            half4 _LeafDark;
            half4 _LeafLight;
            half _LeafRecolor;
            float _MossTiling;
            half4 _MossColor;
            half _Moss;
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);    SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_MossMap);      SAMPLER(sampler_MossMap);

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

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);

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

                half3 source = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalStrength);
                half roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).r;

                // 돌/잎 구분: 채도가 문턱보다 높고 색상이 노랑~초록이면 잎.
                // 초록이 파랑보다 확실히 크고, 빨강과 비슷하거나 더 커야 한다(베이지/분홍 돌은 빨강 > 초록이라 빠진다).
                half maxChannel = max(source.r, max(source.g, source.b));
                half minChannel = min(source.r, min(source.g, source.b));
                half saturation = (maxChannel - minChannel) / max(maxChannel, 0.001);
                half leaf = smoothstep(_LeafDetect, _LeafDetect + 0.12, saturation)
                    * smoothstep(0.0, 0.06, source.g - source.b * 1.1)
                    * smoothstep(-0.03, 0.02, source.g - source.r * 0.95);

                half luminance = dot(source, half3(0.299, 0.587, 0.114));
                half3 stone = lerp(luminance.xxx, source, _StoneSaturation) * _StoneTint.rgb * _StoneBrightness;
                half3 leafColor = lerp(_LeafDark.rgb, _LeafLight.rgb, saturate((luminance - 0.2) * 2.5));
                half3 leafRecolored = lerp(source, leafColor, _LeafRecolor);
                half3 albedo = lerp(stone, leafRecolored, leaf);

                float sgn = input.tangentWS.w;
                half3 vertexNormal = input.normalWS * (frontFace ? 1.0 : -1.0); // 양면(잎)일 때 뒷면도 바르게
                half3 bitangentWS = sgn * cross(vertexNormal, input.tangentWS.xyz);

                // 이끼: 위를 향한 돌 면에만(잎 제외)
                half up = saturate((vertexNormal.y - 0.5) * 3.0);
                half patches = SAMPLE_TEXTURE2D(_MossMap, sampler_MossMap, input.positionWS.xz * _MossTiling).a;
                half moss = smoothstep(1.0 - _Moss, 1.0 - _Moss + 0.3, patches * up) * (1.0 - leaf);
                albedo = lerp(albedo, _MossColor.rgb * (0.8 + 0.4 * patches), moss);
                normalTS.xy *= 1.0 - moss * 0.6;

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
                surface.smoothness = saturate((1.0 - roughness) * _Smoothness * (1.0 - moss * 0.7));
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
