// 실물 객체 위치에 배치되는 보이지 않는 오클루더.
// 색상은 전혀 쓰지 않고(ColorMask 0) 깊이값만 기록해서
// 이 메시 뒤에 있는 가상 객체가 depth test에 걸려 렌더링되지 않게 합니다.
Shader "Custom/DepthOccluder"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry-1"   // 가상 객체(Geometry=2000)보다 먼저 렌더
        }

        Pass
        {
            Name "DepthOccluder"

            ZWrite    On
            ZTest     LEqual
            ColorMask 0         // 색상 버퍼에는 아무것도 쓰지 않음 → 완전 투명
            Cull      Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing   // VR single-pass instancing 지원

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
