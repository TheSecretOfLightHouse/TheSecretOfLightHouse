Shader "Lighthouse/Player/WaterShieldParticle"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half intensity = max(sample.r, max(sample.g, sample.b));
                return half4(intensity.xxx * input.color.rgb, sample.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
