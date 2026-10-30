// 빛나는 물체(크리스탈 등) 주변의 빛 무리. 실시간 조명 없이 반투명 판 한 장을 더해(가산) 빛이 퍼진 것처럼 보이게 한다(모바일: 텍스처 0번).
//   - 빛 무리(_Billboard = 1): 판이 항상 카메라를 향하고, 가운데서 가장자리로 부드럽게 사라진다. 물체 안에 묻히지 않게 카메라 쪽으로 조금 당긴다.
//   - 바닥 번짐(_Billboard = 0): 판을 놓인 방향 그대로(바닥에 눕혀) 그려, 빛이 주변 돌에 비친 것처럼 보이게 한다.
// 크기는 오브젝트 크기(판 1 = 오브젝트 크기 1)를 따른다. Crystal 셰이더와 같은 식으로 위치마다 위상이 다르게 맥동한다. 안개 속에서는 옅어진다.
//   - 빛 알갱이(_VERTEX_COLOR, 파티클): 파티클이 이미 카메라를 향해 그리므로 _Billboard 는 끄고, 파티클 색(정점 색 RGB × A)을 곱해 나타나고 사라지게 한다.
Shader "MiniChess/GlowHalo"
{
    Properties
    {
        _Color ("Color", Color) = (0.16, 0.5, 1, 1)
        _Intensity ("Intensity (HDR)", Range(0, 8)) = 1.2
        _Falloff ("Falloff (higher = tighter core)", Range(0.5, 6)) = 2.2
        [Toggle] _Billboard ("Face Camera", Float) = 1
        _PushToCamera ("Push To Camera", Range(0, 2)) = 0.3
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.2
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.2
        [Toggle(_VERTEX_COLOR)] _UseVertexColor ("Use Vertex Color (particles)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _VERTEX_COLOR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Falloff;
                half _Billboard;
                half _PushToCamera;
                half _PulseAmount;
                half _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half2 fogAndPulse : TEXCOORD1; // x = 안개, y = 맥동 밝기 배율
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS;
                if (_Billboard > 0.5)
                {
                    // 카메라를 향한 판: 오브젝트 크기만큼 카메라의 오른쪽/위쪽으로 펼친다
                    float size = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up = UNITY_MATRIX_V[1].xyz;
                    float3 toCamera = normalize(GetCameraPositionWS() - center);
                    positionWS = center + toCamera * (_PushToCamera * size)
                        + (right * input.positionOS.x + up * input.positionOS.y) * size;
                }
                else
                {
                    positionWS = TransformObjectToWorld(input.positionOS.xyz);
                }

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.fogAndPulse.x = ComputeFogFactor(output.positionCS.z);
                float phase = dot(center, float3(1.7, 0.0, 2.3));
                output.fogAndPulse.y = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed + phase);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 가운데(1) → 가장자리(0)로 부드럽게
                half distance = length(input.uv - 0.5) * 2.0;
                half glow = pow(saturate(1.0 - distance), _Falloff);
                half3 color = _Color.rgb * (glow * _Intensity * input.fogAndPulse.y);
                #ifdef _VERTEX_COLOR
                    color *= input.color.rgb * input.color.a;
                #endif

                // 가산이므로 안개 속에서는 검정(= 더하지 않음)으로 옅어진다
                color = MixFogColor(color, half3(0, 0, 0), input.fogAndPulse.x);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
