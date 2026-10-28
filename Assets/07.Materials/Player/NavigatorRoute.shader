Shader "Lighthouse/Player/NavigatorRoute"
{
    Properties
    {
        [HDR] _BaseColor ("Mint Light", Color) = (0.2, 1, 0.85, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.8
        _FlowSpeed ("Flow Speed", Float) = 1.4
        _FlowDensity ("Flow Density", Float) = 0.18
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Opacity;
                float _FlowSpeed;
                float _FlowDensity;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float edge = abs(input.uv.y * 2.0 - 1.0);
                float softness = 1.0 - smoothstep(0.12, 1.0, edge);
                float core = 1.0 - smoothstep(0.0, 0.32, edge);
                float phase = frac(input.uv.x * _FlowDensity - _Time.y * _FlowSpeed * 0.25);
                float pulse = smoothstep(0.0, 0.16, phase) * (1.0 - smoothstep(0.16, 0.65, phase));
                half3 tint = _BaseColor.rgb;
                half3 color = lerp(tint, half3(0.85, 1.0, 0.97), core * (0.25 + pulse * 0.65));
                float alpha = softness * _Opacity * input.color.a * _BaseColor.a * (0.28 + pulse * 0.72);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
