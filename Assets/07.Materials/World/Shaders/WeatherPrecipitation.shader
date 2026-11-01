Shader "Lighthouse/World/WeatherPrecipitation"
{
    Properties { _IsSplash ("Water Ring", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _IsSplash;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float fog : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float radius = length(p);
                float drop = saturate(1 - p.x * p.x) * saturate(1 - p.y * p.y);
                float ring = (1 - smoothstep(0.7, 0.95, radius)) * smoothstep(0.4, 0.65, radius);
                float alpha = lerp(drop * drop, ring, _IsSplash) * input.color.a;
                return half4(MixFog(input.color.rgb, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
