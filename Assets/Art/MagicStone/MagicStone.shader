// 마법석: 유백색 대리석 + 금빛 광맥(광맥만 발광, 느린 맥동).
// 텍스처 없이 오브젝트 공간 3D 절차적 무늬라 UV 가 없는 메시에도 이음매 없이 붙고, 오브젝트를 움직여도 무늬는 고정된다.
Shader "MiniChess/MagicStone"
{
    Properties
    {
        [Header(Stone)]
        _BaseColor("Base Color", Color) = (0.96, 0.92, 0.85, 1)
        _CloudColor("Cloud Color", Color) = (0.70, 0.69, 0.68, 1)
        _PatternScale("Stone Pattern Scale", Float) = 1.6
        _CloudStrength("Cloud Strength", Range(0, 1)) = 0.35
        _GrainStrength("Grain Strength", Range(0, 1)) = 0.25
        _Smoothness("Smoothness", Range(0, 1)) = 0.45

        [Header(Gold Veins)]
        _VeinColor("Vein Color", Color) = (0.86, 0.64, 0.28, 1)
        _VeinDensity("Vein Density", Float) = 3.2
        _VeinWidth("Vein Width", Range(0.005, 0.2)) = 0.025
        _VeinWidthVariation("Vein Width Variation", Range(0, 1)) = 0.6
        _VeinCoverage("Vein Coverage", Range(0, 1)) = 0.8
        _VeinIrregularity("Vein Irregularity", Range(0, 1)) = 0.4
        _VeinMetallic("Vein Metallic", Range(0, 1)) = 0.8
        _VeinSmoothness("Vein Smoothness", Range(0, 1)) = 0.7

        [Header(Glow)]
        [HDR] _GlowCoreColor("Glow Core Color", Color) = (1.0, 0.88, 0.6, 1)
        [HDR] _GlowEdgeColor("Glow Edge Color", Color) = (0.8, 0.52, 0.14, 1)
        _GlowIntensity("Glow Intensity", Range(0, 10)) = 1.2
        _GlowHalo("Glow Halo", Range(0, 1)) = 0.15
        _PulseSpeed("Pulse Speed", Range(0, 3)) = 0.35
        _PulseAmount("Pulse Amount", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _CloudColor;
            float _PatternScale;
            half _CloudStrength;
            half _GrainStrength;
            half _Smoothness;
            half4 _VeinColor;
            float _VeinDensity;
            float _VeinWidth;
            half _VeinWidthVariation;
            half _VeinCoverage;
            half _VeinIrregularity;
            half _VeinMetallic;
            half _VeinSmoothness;
            half4 _GlowCoreColor;
            half4 _GlowEdgeColor;
            half _GlowIntensity;
            half _GlowHalo;
            half _PulseSpeed;
            half _PulseAmount;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ---- 노이즈 ----

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash13(i + float3(0, 0, 0)), Hash13(i + float3(1, 0, 0)), u.x),
                         lerp(Hash13(i + float3(0, 1, 0)), Hash13(i + float3(1, 1, 0)), u.x), u.y),
                    lerp(lerp(Hash13(i + float3(0, 0, 1)), Hash13(i + float3(1, 0, 1)), u.x),
                         lerp(Hash13(i + float3(0, 1, 1)), Hash13(i + float3(1, 1, 1)), u.x), u.y),
                    u.z);
            }

            float Fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll] for (int octave = 0; octave < 4; octave++)
                {
                    value += amplitude * ValueNoise(p);
                    p = p * 2.03 + 17.1;
                    amplitude *= 0.5;
                }
                return value / 0.9375;
            }

            // 보로노이 셀 경계까지의 거리(x)와 그 경계의 Id(0~1, y). 같은 경계는 양쪽 셀에서 같은 Id 가 나온다.
            float2 VoronoiEdge(float3 x)
            {
                float3 n = floor(x);
                float3 f = frac(x);

                float3 nearestCell = 0;
                float3 nearestOffset = 0;
                float nearestDist = 8.0;
                [unroll] for (int k = -1; k <= 1; k++)
                [unroll] for (int j = -1; j <= 1; j++)
                [unroll] for (int i = -1; i <= 1; i++)
                {
                    float3 g = float3(i, j, k);
                    float3 r = g + Hash33(n + g) - f;
                    float d = dot(r, r);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearestOffset = r;
                        nearestCell = g;
                    }
                }

                float nearestCellHash = Hash13(n + nearestCell);
                float edgeDist = 8.0;
                float edgeId = 0.0;
                [unroll] for (int k2 = -1; k2 <= 1; k2++)
                [unroll] for (int j2 = -1; j2 <= 1; j2++)
                [unroll] for (int i2 = -1; i2 <= 1; i2++)
                {
                    float3 g = nearestCell + float3(i2, j2, k2);
                    float3 r = g + Hash33(n + g) - f;
                    float3 diff = r - nearestOffset;
                    if (dot(diff, diff) > 1e-5)
                    {
                        float id = frac(nearestCellHash + Hash13(n + g));
                        float d = dot(0.5 * (nearestOffset + r), normalize(diff));
                        if (d < edgeDist)
                        {
                            edgeDist = d;
                            edgeId = id;
                        }
                    }
                }

                return float2(edgeDist, edgeId);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pos = input.positionOS;

                // ---- 대리석 ----
                float3 stonePos = pos * _PatternScale;
                float cloud = smoothstep(0.42, 0.78, Fbm(stonePos + Fbm(stonePos * 0.7) * 1.5));
                float streak = 1.0 - abs(sin((stonePos.x + stonePos.y * 0.6 + Fbm(stonePos * 1.7 + 3.1) * 4.0) * 1.3));
                float marble = saturate(cloud * 0.8 + pow(streak, 10.0) * 0.5);
                half3 stone = lerp(_BaseColor.rgb, _CloudColor.rgb, marble * _CloudStrength);
                float grain = (ValueNoise(stonePos * 28.0) - 0.5) * 0.6 + (ValueNoise(stonePos * 61.0) - 0.5) * 0.4;
                stone *= 1.0 + grain * _GrainStrength * 0.25;

                // ---- 광맥 ----
                float3 veinPos = pos * _VeinDensity;
                float3 warp = float3(Fbm(veinPos * 0.6), Fbm(veinPos * 0.6 + 5.2), Fbm(veinPos * 0.6 + 9.7)) - 0.5;
                veinPos += warp * _VeinIrregularity * 1.6;
                veinPos += (float3(ValueNoise(veinPos * 5.0), ValueNoise(veinPos * 5.0 + 2.7), ValueNoise(veinPos * 5.0 + 6.1)) - 0.5) * _VeinIrregularity * 0.12;

                float2 edge = VoronoiEdge(veinPos);
                float edgeWidthScale = 0.45 + 1.1 * frac(edge.y * 7.13);
                float alongWidthScale = 0.3 + 1.4 * ValueNoise(veinPos * 2.3 + 11.0);
                float width = _VeinWidth * lerp(1.0, edgeWidthScale * alongWidthScale, _VeinWidthVariation);

                // Coverage 밖의 경계는 광맥을 두지 않아 이웃 셀이 합쳐진 넓은 면을 만든다(모든 셀을 감싸지 않게).
                float kept = step(edge.y, _VeinCoverage);

                // 작은 화면에서도 결이 사라지지 않게 최소 굵기를 화면 픽셀 기준으로 보장하고, 경계는 픽셀 단위로 부드럽게 한다.
                float aa = max(fwidth(edge.x), 1e-4);
                width = max(width, aa * 0.75) * kept;
                float vein = (1.0 - smoothstep(width - aa, width + aa, edge.x)) * kept;
                float core = 1.0 - smoothstep(0.0, width * 0.6 + aa * 0.5, edge.x);
                float halo = (1.0 - smoothstep(width, width * 4.0 + aa, edge.x)) * (1.0 - vein) * kept;

                // ---- 표면 ----
                half3 seepTint = lerp(stone, stone * _VeinColor.rgb * 1.25, 0.35);
                half3 albedo = lerp(stone, seepTint, halo * _GlowHalo * 2.0);
                albedo = lerp(albedo, _VeinColor.rgb, vein);

                // 맥동: 무늬는 고정, 밝기만 천천히 오르내린다(위치마다 위상을 살짝 어긋나게).
                float phase = ValueNoise(pos * 0.8) * 1.5;
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 6.2831853 + phase);
                half3 glow = lerp(_GlowEdgeColor.rgb, _GlowCoreColor.rgb, core) * vein + _GlowEdgeColor.rgb * halo * _GlowHalo;

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = _VeinMetallic * vein;
                surface.smoothness = lerp(_Smoothness, _VeinSmoothness, vein);
                surface.normalTS = half3(0, 0, 1);
                surface.emission = glow * _GlowIntensity * pulse;
                surface.occlusion = 1.0;
                surface.alpha = 1.0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
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
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
