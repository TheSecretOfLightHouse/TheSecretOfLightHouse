Shader "Lighthouse/Player/SearchlightBeam"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 0.91, 0.65, 0.065)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // A decorative beam, not volumetric lighting or gameplay visibility.
                half fade = smoothstep(0.0, 0.06, input.uv.y)
                    * (1.0 - smoothstep(0.35, 1.0, input.uv.y));
                return half4(_Tint.rgb, _Tint.a * fade);
            }
            ENDHLSL
        }
    }
}
