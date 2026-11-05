// 불 정령(텍스처 없음). 카메라를 정면으로 보는 판(Quad) 한 장에 몸과 불꽃을 한 번에 그린다. 얼굴은 추후 디자인.
//   - 몸과 불꽃은 하나의 세기 값(가운데 1 → 가장자리 0)에서 나온다. 아래는 둥근 몸, 둘레(위쪽일수록 길게)로 뾰족한 혀가 솟는다.
//     색도 같은 램프(심지 → 안쪽 → 가운데 → 가장자리)로 칠하므로 몸과 불꽃 사이에 경계가 생기지 않는다.
//   - 팔: 몸 양옆의 작은 불꽃 혓바닥(세기를 합친다).
//   - 움직임: 위아래로 둥실, 늘었다 납작해지기(부피 유지), 불꽃은 위로 흐르는 노이즈. 오브젝트 위치로 시점을 어긋나게 한다.
//   - 판 크기는 오브젝트 스케일 x, 원점 = 판 가운데. 조명·그림자 없음, 투명(깊이 안 씀).
// 모바일 기준: 텍스처 샘플 없음, 값 노이즈 3번.
Shader "MiniChess/FireSpirit"
{
    Properties
    {
        [Header(Color Ramp)]
        [HDR] _CoreColor ("Core (심지)", Color) = (1.3, 1.0, 0.32, 1)
        [HDR] _InnerColor ("Inner (안쪽)", Color) = (1.2, 0.82, 0.18, 1)
        [HDR] _MidColor ("Mid (가운데)", Color) = (1.2, 0.6, 0.08, 1)
        [HDR] _EdgeColor ("Edge (가장자리)", Color) = (1.1, 0.25, 0.03, 1)

        [Header(Body)]
        _BodyRadius ("Body Radius", Range(0.1, 0.5)) = 0.26
        _BodyCenterY ("Body Center Y", Range(-0.5, 0.2)) = -0.22
        _ArmSize ("Arm Size", Range(0, 0.3)) = 0.075

        [Header(Flame)]
        _FlameHeight ("Flame Height (위쪽 혀 길이)", Range(0, 3)) = 1.5
        _Taper ("Top Focus (위로 몰림)", Range(0.5, 6)) = 1.6
        _SideFlame ("Side Flame (옆쪽 혀 길이)", Range(0, 1)) = 0.5
        _Bend ("Bend (옆 혀가 위로 휨)", Range(0, 2)) = 0.9
        _TongueCount ("Tongue Count (둘레 혀 개수)", Range(2, 16)) = 5
        _Sharpness ("Tongue Sharpness (끝 뾰족함)", Range(1, 8)) = 2.6
        _Tear ("Tear (끝 찢어짐)", Range(0, 1)) = 0.25
        _NoiseScale ("Noise Scale", Float) = 6.5
        _Speed ("Rise Speed", Float) = 1.8
        _Wobble ("Side Wobble", Range(0, 0.4)) = 0.12

        [Header(Motion)]
        _Bob ("Bob Height (판 대비)", Range(0, 0.1)) = 0.025
        _Squash ("Squash", Range(0, 0.2)) = 0.06
        _BobSpeed ("Bob Speed", Float) = 2.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Spirit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _InnerColor;
                half4 _MidColor;
                half4 _EdgeColor;
                half _BodyRadius;
                half _BodyCenterY;
                half _ArmSize;
                half _FlameHeight;
                half _Taper;
                half _SideFlame;
                half _Bend;
                half _TongueCount;
                half _Sharpness;
                half _Tear;
                float _NoiseScale;
                float _Speed;
                half _Wobble;
                half _Bob;
                half _Squash;
                float _BobSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 p : TEXCOORD0;      // 판 좌표(-0.5~0.5)
                float phase : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                float size = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;

                float3 positionWS = originWS + (right * input.positionOS.x + up * input.positionOS.y) * size;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.p = input.positionOS.xy;
                output.phase = Hash(originWS.xz) * 100.0;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float t = _Time.y + input.phase;

                // 둥실 + 늘었다 납작해지기(위로 갈 때 늘어남, 부피 유지).
                float bob = sin(t * _BobSpeed);
                float stretch = 1.0 + bob * _Squash;
                float2 center = float2(0.0, _BodyCenterY + bob * _Bob);
                float2 q = input.p - center;
                q /= float2(rsqrt(stretch), stretch);
                q /= _BodyRadius;                      // 몸 반지름 = 1

                // 불꽃: 둥근 몸 둘레(각도)마다 뾰족한 혀를 세운다. 위쪽일수록 길고, 아래쪽은 거의 없어 몸이 둥글게 남는다.
                //   혀 = 각도 방향으로 흐르는 능선 노이즈(1 - |2n - 1|)를 거듭제곱해 끝을 뾰족하게 모은 것. 위로 갈수록 좌우로 흔든다.
                float fy = t * _Speed;
                float upward = saturate(q.y);
                float sway = (Noise(float2(q.y * 1.2 - fy, input.phase)) - 0.5) * 2.0 * _Wobble * upward * 3.0;
                float2 f = float2(q.x - sway, q.y);
                float r = length(f);
                // 몸 밖으로 나갈수록 각도를 위쪽으로 당겨 옆 혀가 위로 휘어 오르게 한다.
                float2 bent = float2(f.x, f.y - max(r - 1.0, 0.0) * _Bend);
                float angle = atan2(bent.x, bent.y);                       // 0 = 위, ±π = 아래
                float top = saturate(cos(angle) * 0.5 + 0.5);              // 위 1 → 아래 0
                float ridge = 1.0 - abs(Noise(float2(angle * _TongueCount * 0.32, -fy + input.phase)) * 2.0 - 1.0);
                float ridge2 = 1.0 - abs(Noise(float2(angle * _TongueCount * 0.64 + 3.1, -fy * 1.3)) * 2.0 - 1.0);
                float spikes = pow(ridge, _Sharpness) * 0.75 + pow(ridge2, _Sharpness) * 0.25;
                float side = 1.0 - abs(cos(angle));                         // 옆 1 → 위·아래 0(아래 각도 이음매에서 혀가 끊기지 않게)
                float reach = 1.0 + _FlameHeight * pow(top, _Taper) * (0.35 + spikes)    // 위로 길게
                                  + _SideFlame * spikes * side * 0.5;                    // 옆 짧은 혀
                float heat = 1.0 - r / reach;

                // 팔: 몸 양옆 작은 혓바닥(위쪽으로 살짝 들림).
                if (_ArmSize > 0.0)
                {
                    float2 armL = (q - float2(-0.9, -0.35)) / (_ArmSize / _BodyRadius);
                    float2 armR = (q - float2(0.9, -0.35)) / (_ArmSize / _BodyRadius);
                    armL.y /= 1.0 + saturate(armL.y) * 1.2;
                    armR.y /= 1.0 + saturate(armR.y) * 1.2;
                    heat = max(heat, max(1.0 - length(armL), 1.0 - length(armR)) * 0.6);
                }

                // 혀 모양: 위로 흐르는 난류를 뺀다(아래 가운데 몸은 거의 그대로, 가장자리·위쪽일수록 세게).
                float2 n = float2(q.x * _NoiseScale * 0.5, q.y * _NoiseScale * 0.4 - fy * 1.6);
                float turbulence = Noise(n) * 0.65 + Noise(n * 2.1 + 7.3) * 0.35;
                float rim = saturate(1.0 - heat * 2.0);
                heat -= turbulence * _Tear * (0.08 + upward * 0.7 + rim * 0.25);

                // 하나의 램프: 가장자리 → 가운데 → 안쪽 → 심지.
                half3 color = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.22, heat));
                color = lerp(color, _InnerColor.rgb, smoothstep(0.18, 0.45, heat));
                color = lerp(color, _CoreColor.rgb, smoothstep(0.45, 0.85, heat));
                float aa = max(fwidth(heat), 1e-4);
                half alpha = smoothstep(0.0, aa * 1.5, heat);

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
