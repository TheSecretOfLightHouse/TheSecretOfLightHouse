Shader "Lighthouse/World/LightningBolt"
{
    Properties
    {
        _Intensity ("Intensity", Range(1, 8)) = 3
        _Soft ("Soft Edge", Range(0, 1)) = 0
        _SoftPower ("Soft Edge Power", Range(0.5, 6)) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+30"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Soft;
                float _SoftPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float across = abs(input.uv.y * 2.0 - 1.0);
                float falloff = pow(saturate(1.0 - across), _SoftPower);
                float alpha = input.color.a * lerp(1.0, falloff, _Soft);
                return float4(input.color.rgb * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
}
