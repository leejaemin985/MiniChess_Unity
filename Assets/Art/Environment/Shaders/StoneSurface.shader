// 돌 표면(바닥 판석, 이후 벽/장애물). URP 의 PBR 조명(주광, 그림자, 주변광, 보조광, 안개)을 그대로 쓰고, 표면만 직접 만든다.
//   - 텍스처 3장(색, 노멀, 마스크)을 메시 UV 로 읽는다. 마스크: R = 금, G = 움푹한 곳, B = 매끈함 변화, A = 이끼 얼룩.
//   - 이끼: 마스크 A 얼룩이 윗면/아랫단(정점 B)/모서리(정점 R) 중 머티리얼이 정한 자리에서 문턱을 넘으면 이끼 색으로 덮는다.
//   - 칸마다 다르게: 월드 위치가 속한 칸(_CellSize)으로 난수를 만들어 텍스처 위치/90도 회전, 색조, 밝기, 금 유무를 흔든다.
//     칸 중심이 (X * _CellSize, Y * _CellSize) 라는 보드 좌표 규칙(BoardCoordinates)을 따른다.
//   - 모서리: 메시의 정점 색을 쓴다(StoneSlabMeshBuilder, StoneMeshKit). R = 모서리 닳음, G = 깨진 자리/갈라진 면, B = 아래쪽 때, A = 조각 밝기.
//   - 칸별 색: 보드 칸 색 텍스처(BoardCellTints, 칸 좌표로 읽음)의 지형 색조(시작 위치, 점령 칸, 벽)를 곱하고 장판 덧칠(a = 세기)을 섞는다.
//     렌더러마다 MaterialPropertyBlock 을 걸지 않아 SRP Batcher 로 묶인다. _BaseColor / _OverlayColor 는 머티리얼 전체 값.
// 모바일 기준: 텍스처 샘플 3번 + 칸 색 텍셀 읽기 2번, 노이즈 계산 없음(텍스처는 StoneTextureBaker 로 미리 굽는다).
Shader "MiniChess/StoneSurface"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _BaseMap ("Albedo", 2D) = "white" {}
        [NoScaleOffset][Normal] _NormalMap ("Normal", 2D) = "bump" {}
        [NoScaleOffset] _MaskMap ("Mask (R crack, G cavity, B smooth, A moss)", 2D) = "black" {}
        _Tiling ("Texture Tiling", Float) = 0.7
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1

        [Header(Per Cell Variation)]
        _CellSize ("Cell Size", Float) = 1
        _TintA ("Variation Tint A", Color) = (1.04, 0.99, 0.94, 1)
        _TintB ("Variation Tint B", Color) = (0.95, 0.95, 1.0, 1)
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.8
        _BrightnessVariation ("Brightness Variation", Range(0, 0.5)) = 0.18

        [Header(Wear)]
        _EdgeColor ("Edge Wear Color", Color) = (1.12, 1.07, 1.0, 1)
        _EdgeWear ("Edge Wear", Range(0, 1)) = 0.45
        _ChipColor ("Chip Color", Color) = (0.66, 0.62, 0.58, 1)
        _DirtColor ("Dirt Color", Color) = (0.45, 0.43, 0.43, 1)
        _Dirt ("Dirt", Range(0, 1)) = 0.6
        _CrackDarkness ("Crack Darkness", Range(0, 1)) = 0.55
        _CrackAmount ("Crack Amount (share of cells)", Range(0, 1)) = 0.4

        [Header(Surface)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.22
        _Occlusion ("Cavity Occlusion", Range(0, 1)) = 0.75

        [Header(Moss)]
        _MossColor ("Moss Color", Color) = (0.22, 0.28, 0.12, 1)
        _MossColor2 ("Moss Color 2", Color) = (0.34, 0.38, 0.17, 1)
        _Moss ("Moss Amount", Range(0, 1)) = 0.3
        _MossTop ("Moss On Top Faces", Range(0, 1)) = 0.1
        _MossBottom ("Moss At Bottom (vertex B)", Range(0, 1)) = 0.6
        _MossEdge ("Moss On Edges (vertex R)", Range(0, 1)) = 0.6

        [Header(Overlay)]
        _OverlayColor ("Overlay (a = strength)", Color) = (0, 0, 0, 0)
        _OverlayGlow ("Overlay Glow", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Tiling;
            half _NormalStrength;
            float _CellSize;
            half4 _TintA;
            half4 _TintB;
            half _TintStrength;
            half _BrightnessVariation;
            half4 _EdgeColor;
            half _EdgeWear;
            half4 _ChipColor;
            half4 _DirtColor;
            half _Dirt;
            half _CrackDarkness;
            half _CrackAmount;
            half _Smoothness;
            half _Occlusion;
            half4 _MossColor;
            half4 _MossColor2;
            half _Moss;
            half _MossTop;
            half _MossBottom;
            half _MossEdge;
            half4 _OverlayColor;
            half _OverlayGlow;
        CBUFFER_END
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

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap);    SAMPLER(sampler_MaskMap);

            // 칸별 색(BoardCellTints 가 전역으로 넣음). W × 2H: 아래 절반 = 지형 색조, 위 절반 = 덧칠. _BoardCellCount = (W, H), 0 이면 보드 없음.
            TEXTURE2D(_BoardCellData);
            float4 _BoardCellCount;

            void BoardCellColors(float2 cell, out half3 terrain, out half4 overlay)
            {
                terrain = half3(1, 1, 1);
                overlay = half4(0, 0, 0, 0);
                if (all(cell >= 0.0) && all(cell < _BoardCellCount.xy))
                {
                    uint2 texel = (uint2)cell;
                    terrain = LOAD_TEXTURE2D(_BoardCellData, texel).rgb;
                    overlay = LOAD_TEXTURE2D(_BoardCellData, texel + uint2(0, (uint)_BoardCellCount.y));
                }
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half4 color : TEXCOORD4;
                half4 fogAndVertexLight : TEXCOORD5; // x = 안개, yzw = 정점 보조광
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 칸 좌표 → 0~1 난수 4개(Dave Hoskins hash).
            float4 CellHash(float2 cell)
            {
                float4 p4 = frac(cell.xyxy * float4(0.1031, 0.1030, 0.0973, 0.1099));
                p4 += dot(p4, p4.wzxy + 33.33);
                return frac((p4.xxyz + p4.yzzw) * p4.zywx);
            }

            // 90도 단위 회전(k = 0~3). uv 와 노멀 xy 를 같은 회전으로 돌린다.
            float2 Rotate90(float2 v, float k)
            {
                float2 r = v;
                if (k >= 1) r = float2(-r.y, r.x);
                if (k >= 2) r = float2(-r.y, r.x);
                if (k >= 3) r = float2(-r.y, r.x);
                return r;
            }

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
                output.color = input.color;
                output.fogAndVertexLight.x = ComputeFogFactor(position.positionCS.z);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    output.fogAndVertexLight.yzw = VertexLighting(position.positionWS, normal.normalWS);
                #endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 칸마다 다른 난수: x = 회전, y = 색조, z = 밝기, w = 금 유무. 텍스처 위치는 두 번째 난수(hash2)로 정한다.
                float2 cell = floor(input.positionWS.xz / _CellSize + 0.5);
                float4 hash = CellHash(cell);
                float4 hash2 = CellHash(cell + 17.0);
                float rotation = floor(hash.x * 4.0);

                float2 uv = Rotate90(input.uv - 0.5, rotation) * _Tiling + hash2.xy * 8.0;
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalStrength);
                // uv 를 돌렸으니 노멀의 방향도 원래 uv 축으로 되돌린다(역회전 = 4 - k 번 회전).
                normalTS.xy = Rotate90(normalTS.xy, fmod(4.0 - rotation, 4.0));

                // 판석마다 색조/밝기
                half3 tint = lerp(_TintA.rgb, _TintB.rgb, hash.y);
                albedo *= lerp(half3(1, 1, 1), tint, _TintStrength);
                albedo *= 1.0 + (hash.z - 0.5) * 2.0 * _BrightnessVariation;

                // 금: 일부 칸에만
                half crack = mask.r * saturate((_CrackAmount - hash.w) * 6.0);
                albedo *= 1.0 - crack * _CrackDarkness;

                // 모서리 닳음(밝게), 깨진 자리(새 돌 색), 움푹한 곳/아래쪽 때(어둡게)
                half edge = input.color.r;
                half chip = input.color.g;
                half dirt = saturate(mask.g * 0.7 + input.color.b);
                albedo = lerp(albedo, albedo * _EdgeColor.rgb, edge * _EdgeWear);
                albedo = lerp(albedo, _ChipColor.rgb * (0.85 + 0.3 * mask.b), chip);
                albedo = lerp(albedo, albedo * _DirtColor.rgb, dirt * _Dirt);
                albedo *= 0.75 + 0.25 * input.color.a; // 조각(벽돌)마다 밝기. 1 = 그대로
                half3 cellTerrain;
                half4 cellOverlay;
                BoardCellColors(cell, cellTerrain, cellOverlay);
                albedo *= _BaseColor.rgb * cellTerrain;

                // 이끼: 마스크 A(얼룩) × 자랄 자리(윗면 / 아랫단 때 / 모서리)가 문턱(1 - _Moss)을 넘는 곳. 칸마다 양을 조금씩 흔든다.
                // 바닥은 윗면 세기를 낮게 두어 판석 가장자리에만, 벽은 윗면과 아랫단에 많이 낀다(머티리얼 값).
                half up = saturate((input.normalWS.y - 0.55) * 3.0);
                half grow = up * _MossTop + input.color.b * _MossBottom + edge * _MossEdge;
                half mossMask = mask.a * saturate(grow) + (hash2.z - 0.5) * 0.25;
                half moss = smoothstep(1.0 - _Moss, 1.0 - _Moss + 0.35, mossMask);
                half3 mossColor = lerp(_MossColor.rgb, _MossColor2.rgb, mask.b) * (0.8 + 0.4 * mask.a);
                albedo = lerp(albedo, mossColor, moss);
                normalTS.xy *= 1.0 - moss * 0.6; // 이끼는 돌 결을 덮어 덜 울퉁불퉁하게

                // 덧칠(칸 하이라이트/장판): 돌 색을 덧칠 색 쪽으로 섞고, 그림자 안에서도 보이게 약하게 빛나게 한다.
                half4 overlay = cellOverlay.a >= _OverlayColor.a ? cellOverlay : _OverlayColor;
                albedo = lerp(albedo, overlay.rgb, overlay.a);
                half3 emission = overlay.rgb * (overlay.a * _OverlayGlow);

                float sgn = input.tangentWS.w;
                half3 bitangentWS = sgn * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));
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
                surface.specular = half3(0, 0, 0);
                surface.smoothness = saturate(_Smoothness * (0.6 + 0.8 * mask.b) * (1.0 - dirt * 0.5) * (1.0 - chip * 0.5) * (1.0 - moss * 0.7));
                surface.normalTS = normalTS;
                surface.occlusion = 1.0 - saturate(mask.g + crack) * _Occlusion;
                surface.emission = emission;
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

        // SSAO 등 화면 공간 효과용. 판석 굴곡만 쓰고 노멀 맵은 쓰지 않는다(가볍게).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                    return half4(PackFloat2To888(remapped), 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
