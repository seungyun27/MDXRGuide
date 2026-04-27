// Quest 3 / Android Vulkan water shader - OPAQUE mode
// Transparent blend with passthrough alpha causes invisible rendering.
// Using Opaque queue: water surface is solid, passthrough shows everywhere else.
// Wave formula identical to WaterVolumeHelper.GetHeight() for C# sync.
Shader "Custom/WaterQuest3"
{
    Properties
    {
        [Header(Colors)]
        _ShallowColor   ("Shallow Color",   Color) = (0.15, 0.55, 0.70, 1.0)
        _DeepColor      ("Deep Color",      Color) = (0.04, 0.18, 0.42, 1.0)
        _FoamColor      ("Foam Color",      Color) = (0.88, 0.94, 1.00, 1.0)
        _SpecularColor  ("Specular Color",  Color) = (0.90, 0.96, 1.00, 1.0)

        [Header(Waves)]
        _WaveFrequency  ("Wave Frequency",  Float)       = 20.0
        _WaveScale      ("Wave Scale",      Float)       = 0.008
        _WaveSpeed      ("Wave Speed",      Float)       = 1.50

        [Header(Normal Map)]
        _NormalMap      ("Normal Map",      2D)          = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2))  = 0.60
        _ScrollSpeed    ("Scroll Speed",    Float)       = 1.00

        [Header(Foam)]
        _FoamWidth      ("Foam Width",      Range(0,1))  = 0.40
        _FoamNoise      ("Foam Noise",      Float)       = 2.50

        [Header(Appearance)]
        _Smoothness     ("Smoothness",      Range(0,1))  = 0.85
        _FresnelPower   ("Fresnel Power",   Range(0.5,8))= 2.00

        [Header(Circle Fade)]
        _CircleFadeStart("Circle Fade Start", Range(0.1, 1.0)) = 0.60
        _CircleFadeEnd  ("Circle Fade End",   Range(0.1, 1.0)) = 0.95
    }

    SubShader
    {
        // Opaque - no alpha blend issue on Quest 3 passthrough
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest  LEqual
            Cull   Back
            // No Blend = fully opaque

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _SpecularColor;
                float4 _NormalMap_ST;
                float  _WaveFrequency;
                float  _WaveScale;
                float  _WaveSpeed;
                float  _NormalStrength;
                float  _ScrollSpeed;
                float  _FoamWidth;
                float  _FoamNoise;
                float  _Smoothness;
                float  _FresnelPower;
                float  _CircleFadeStart;
                float  _CircleFadeEnd;
            CBUFFER_END

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float2 uv          : TEXCOORD1;
                float4 vertexColor : TEXCOORD2;
                float3 normalWS    : TEXCOORD3;
                float3 tangentWS   : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // ── Vertex wave displacement (3-wave Gerstner-style sine) ──────
                // Frequencies tuned for tabletop scale (0.6–1 m plane).
                // Three waves at different angles for organic, non-repeating motion.
                float spd = _Time.y * _WaveSpeed;
                float f   = _WaveFrequency;

                float w1 = sin(posWS.x *  f           + spd          ) * _WaveScale;
                float w2 = sin(posWS.z *  f * 0.73f   + spd * 1.27f  ) * _WaveScale * 0.65f;
                float w3 = sin((posWS.x + posWS.z) * f * 0.53f + spd * 0.85f) * _WaveScale * 0.45f;
                posWS.y += w1 + w2 + w3;

                // Analytic surface normal from wave gradient (for correct lighting)
                float dydx = cos(posWS.x *  f           + spd          ) * _WaveScale * f
                           + cos((posWS.x + posWS.z) * f * 0.53f + spd * 0.85f) * _WaveScale * 0.45f * f * 0.53f;
                float dydz = cos(posWS.z *  f * 0.73f   + spd * 1.27f  ) * _WaveScale * 0.65f * f * 0.73f
                           + cos((posWS.x + posWS.z) * f * 0.53f + spd * 0.85f) * _WaveScale * 0.45f * f * 0.53f;
                float3 waveNormalWS = normalize(float3(-dydx, 1.0f, -dydz));

                output.positionCS   = TransformWorldToHClip(posWS);
                output.positionWS   = posWS;
                output.uv           = input.uv;
                output.vertexColor  = input.color;

                // Use wave-derived world normal; keep mesh tangent/bitangent for normal map
                output.normalWS    = waveNormalWS;
                VertexNormalInputs ni = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.tangentWS   = ni.tangentWS;
                output.bitangentWS = ni.bitangentWS;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Dual-scroll normal map
                float2 s    = float2(_Time.x * _ScrollSpeed, _Time.x * _ScrollSpeed);
                float2 uv1  = input.uv + float2( s.x * 0.50,  s.y * 0.37);
                float2 uv2  = input.uv + float2(-s.x * 0.37,  s.y * 0.50);

                float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1));
                float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2));

                // Blend and normalize - prevent zero-length vector on mobile
                float3 blendN = float3(n1.xy + n2.xy, max(n1.z + n2.z, 0.01));
                blendN = normalize(blendN);
                blendN = lerp(float3(0.0, 0.0, 1.0), blendN, _NormalStrength);

                // Tangent-to-world transform (explicit, avoids matrix issues on Vulkan)
                float3 T = normalize(input.tangentWS);
                float3 B = normalize(input.bitangentWS);
                float3 N = normalize(input.normalWS);
                float3 normalWS = normalize(blendN.x * T + blendN.y * B + blendN.z * N);

                // View direction
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));

                // Fresnel for shallow/deep color blend
                float NdotV  = saturate(dot(normalWS, viewDir));
                float fresnel = pow(max(1.0 - NdotV, 0.0), _FresnelPower);

                // Water color (Fresnel: shallow near edges, deep at center)
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, fresnel);

                // Foam disabled: Unity primitive Cube/Plane have white vertex colors (1,1,1),
                // which makes foamMask=1 and produces full-coverage hash noise regardless of
                // _FoamWidth. Skip foam entirely for a clean tabletop water look.
                float3 finalRGB = waterColor;

                // Lighting (diffuse + soft specular)
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction)) * 0.65 + 0.35;
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
                // Reduced specular coefficient (0.25 vs 0.6) to prevent white washout
                float spec  = pow(max(NdotH, 0.0), _Smoothness * 128.0 + 2.0)
                              * _Smoothness * 0.25;

                finalRGB = finalRGB * mainLight.color * NdotL
                         + _SpecularColor.rgb * spec * mainLight.color;

                // ── Circular radial fade (dithered clip — passthrough 호환) ────
                // UV 중심에서 거리를 구해 원형 페이드 마스크를 만듭니다.
                // Alpha blend 대신 hash 기반 픽셀 폐기를 사용해 passthrough와 호환됩니다.
                float2 centeredUV  = input.uv - 0.5f;
                float  radialDist  = length(centeredUV) * 2.0f;  // 0=중심, 1=모서리
                float  radialAlpha = 1.0f - smoothstep(_CircleFadeStart, _CircleFadeEnd, radialDist);

                // 스크린 픽셀 좌표 기반 pseudo-random hash → ordered dithering
                float2 sp   = input.positionCS.xy;
                float  hash = frac(sin(dot(sp, float2(12.9898f, 78.233f))) * 43758.5453f);
                clip(radialAlpha - hash);

                return float4(finalRGB, 1.0);
            }
            ENDHLSL
        }

        // Shadow casting pass for proper depth with virtual objects
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _SpecularColor;
                float4 _NormalMap_ST;
                float  _WaveFrequency;
                float  _WaveScale;
                float  _WaveSpeed;
                float  _NormalStrength;
                float  _ScrollSpeed;
                float  _FoamWidth;
                float  _FoamNoise;
                float  _Smoothness;
                float  _FresnelPower;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesShadow
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsShadow
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsShadow vertShadow(AttributesShadow input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                VaryingsShadow output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS   = TransformObjectToWorld(input.positionOS.xyz);

                // Match vertex displacement from ForwardLit pass
                float spd = _Time.y * _WaveSpeed;
                float f   = _WaveFrequency;
                float w1 = sin(posWS.x * f          + spd         ) * _WaveScale;
                float w2 = sin(posWS.z * f * 0.73f  + spd * 1.27f) * _WaveScale * 0.65f;
                float w3 = sin((posWS.x + posWS.z) * f * 0.53f + spd * 0.85f) * _WaveScale * 0.45f;
                posWS.y += w1 + w2 + w3;

                float dydx = cos(posWS.x * f          + spd         ) * _WaveScale * f
                           + cos((posWS.x + posWS.z) * f * 0.53f + spd * 0.85f) * _WaveScale * 0.45f * f * 0.53f;
                float dydz = cos(posWS.z * f * 0.73f  + spd * 1.27f) * _WaveScale * 0.65f * f * 0.73f
                           + cos((posWS.x + posWS.z) * f * 0.53f + spd * 0.85f) * _WaveScale * 0.45f * f * 0.53f;
                float3 normalWS = normalize(float3(-dydx, 1.0f, -dydz));

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDir));
                return output;
            }

            float4 fragShadow(VaryingsShadow input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
