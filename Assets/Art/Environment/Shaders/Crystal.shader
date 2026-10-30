// 빛나는 마법 크리스탈(외부 모델, 각진 면). 텍스처 없이 색과 밝기만으로 그린다(모바일: 텍스처 샘플 0번).
//   - 면 밝기: 면 방향이 햇빛/시선을 향할수록 밝은 코어 색, 반대면은 짙은 바깥 색 → 깎인 면이 한 장씩 읽힌다.
//   - 테두리: 시선과 비스듬한 면(윤곽)일수록 밝게(프레넬). 반짝임: 햇빛 반사가 시선과 맞는 면에 작은 하이라이트.
//   - 몸체: 위 색에 _Emission 을 곱한다(짙은 색 그대로 보이게 1 근처).
//   - 빛남: 윤곽 테두리와 속 코어(시선을 정면으로 향한 면)에만 _GlowColor 를 _GlowIntensity 배로 더해 HDR 로 내보낸다
//     → 이 부분만 Bloom 임계값(1)을 확실히 넘어 번지고, 짙은 몸체 색은 번지지 않는다. 크리스탈마다 위상이 다르게 천천히 맥동.
// 그림자는 받지 않는다(스스로 빛나므로). 안개는 섞는다.
// 떠다니는 파편(_FLOATING): 여러 조각을 합친 메시(CrystalFloatMeshBuilder)를 조각마다 자기 중심(UV1.xyz)에서 위아래로 둥실거리며
// 세로축으로 천천히 돌린다. 조각마다 위상(UV1.w)이 달라 따로 움직인다. 정점 셰이더에서만 움직이므로 CPU 비용이 없다.
Shader "MiniChess/Crystal"
{
    Properties
    {
        [Header(Color)]
        _DeepColor ("Deep (facing away)", Color) = (0.05, 0.12, 0.45, 1)
        _CoreColor ("Core (facing light/view)", Color) = (0.35, 0.75, 1, 1)
        _RimColor ("Rim", Color) = (0.7, 0.95, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _Glint ("Glint", Range(0, 4)) = 1.2

        [Header(Glow)]
        _Emission ("Body Brightness", Range(0, 4)) = 1.2
        _GlowColor ("Glow (rim/core)", Color) = (0.66, 0.93, 1, 1)
        _GlowIntensity ("Glow Intensity (HDR, blooms)", Range(0, 10)) = 4
        _CoreGlow ("Core Glow", Range(0, 1)) = 0.35
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.2
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.2

        [Header(Floating Shards)]
        [Toggle(_FLOATING)] _Floating ("Floating (per-piece bob/spin)", Float) = 0
        _BobHeight ("Bob Height (object units)", Range(0, 0.5)) = 0.08
        _BobSpeed ("Bob Speed", Range(0, 5)) = 1.1
        _SpinSpeed ("Spin Speed", Range(0, 3)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor;
            half4 _CoreColor;
            half4 _RimColor;
            half _RimPower;
            half _Glint;
            half _Emission;
            half4 _GlowColor;
            half _GlowIntensity;
            half _CoreGlow;
            half _PulseAmount;
            half _PulseSpeed;
            half _BobHeight;
            half _BobSpeed;
            half _SpinSpeed;
        CBUFFER_END

        // 떠다니는 파편: 조각 중심(pivot) 기준으로 세로축 회전 + 위아래 흔들림
        void FloatPiece(inout float3 positionOS, inout float3 normalOS, float4 piece)
        {
            float3 pivot = piece.xyz;
            float phase = piece.w;
            float angle = _Time.y * _SpinSpeed + phase;
            float s = sin(angle), c = cos(angle);
            float3 local = positionOS - pivot;
            local = float3(local.x * c - local.z * s, local.y, local.x * s + local.z * c);
            normalOS = float3(normalOS.x * c - normalOS.z * s, normalOS.y, normalOS.x * s + normalOS.z * c);
            float bob = sin(_Time.y * _BobSpeed + phase * 1.7) * _BobHeight;
            positionOS = pivot + local + float3(0, bob, 0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _FLOATING

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 piece : TEXCOORD1; // 떠다니는 파편: xyz = 조각 중심, w = 위상
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half2 fogAndPulse : TEXCOORD2; // x = 안개, y = 맥동 밝기 배율
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                #ifdef _FLOATING
                    FloatPiece(positionOS, normalOS, input.piece);
                #endif

                VertexPositionInputs position = GetVertexPositionInputs(positionOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.fogAndPulse.x = ComputeFogFactor(position.positionCS.z);

                // 크리스탈마다 위상이 다르게(오브젝트 위치로) 천천히 밝아졌다 어두워진다
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                float phase = dot(origin, float3(1.7, 0.0, 2.3));
                output.fogAndPulse.y = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed + phase);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight();

                // 면 밝기: 햇빛 쪽(반쯤 감싸기) + 시선 쪽을 섞어, 어느 각도에서 봐도 면마다 밝기가 갈린다
                half towardLight = dot(normalWS, sun.direction) * 0.5 + 0.5;
                half towardView = saturate(dot(normalWS, viewWS));
                half facet = saturate(towardLight * 0.6 + towardView * 0.5);
                half3 color = lerp(_DeepColor.rgb, _CoreColor.rgb, facet * facet);

                // 윤곽 테두리 + 햇빛 반짝임(몸체)
                half rim = pow(1.0 - towardView, _RimPower);
                color += _RimColor.rgb * rim * 0.5;
                half glint = pow(saturate(dot(reflect(-viewWS, normalWS), sun.direction)), 24.0) * _Glint;
                color += sun.color * glint;
                color *= _Emission;

                // 빛남(HDR): 윤곽 테두리 + 속 코어. 이 부분만 Bloom 으로 번진다
                half core = pow(towardView, 4.0) * _CoreGlow;
                color += _GlowColor.rgb * (rim + core) * _GlowIntensity;

                color *= input.fogAndPulse.y;
                color = MixFog(color, input.fogAndPulse.x);
                return half4(color, 1);
            }
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
            #pragma shader_feature_local _FLOATING

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 piece : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionOS = input.positionOS.xyz;
                #ifdef _FLOATING
                    float3 normalOS = input.normalOS;
                    FloatPiece(positionOS, normalOS, input.piece);
                #endif
                return TransformObjectToHClip(positionOS);
            }

            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
