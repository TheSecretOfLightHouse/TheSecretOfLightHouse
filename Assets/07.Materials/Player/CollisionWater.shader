Shader "Lighthouse/Player/CollisionWater"
{
    Properties
    {
        _Foam ("Foam", Range(0,1)) = 0
        _Ribbon ("Water ribbon", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Foam;
                float _Ribbon;
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
                float fogFactor : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float radius = length(p);
                float foamEdge = 0.78 + 0.04 * sin(p.x * 17 + p.y * 11);
                float edge = lerp(0.94, foamEdge, _Foam);
                float alpha = 1 - smoothstep(edge - 0.35, edge, radius);
                float highlight = exp(-dot(p - float2(-0.26, 0.3), p - float2(-0.26, 0.3)) * 28);
                float3 water = input.color.rgb * lerp(0.88, 1.02, saturate(1 - radius));
                water = lerp(water, saturate(input.color.rgb * 1.12), highlight * 0.35);
                water = lerp(water, input.color.rgb, _Foam);
                float ribbonAlpha = (1 - smoothstep(0.65, 1, abs(p.x)))
                    * smoothstep(0, 0.13, input.uv.y) * (1 - smoothstep(0.85, 1, input.uv.y));
                float3 ribbonColor = lerp(input.color.rgb, saturate(input.color.rgb * 1.12), pow(abs(p.x), 3));
                Light mainLight = GetMainLight();
                float3 illumination = clamp(SampleSH(float3(0,1,0)) * 0.7
                    + mainLight.color * 0.35, 0.75, 1);
                float3 color = MixFog(lerp(water, ribbonColor, _Ribbon) * illumination, input.fogFactor);
                return half4(color,
                    lerp(alpha, ribbonAlpha, _Ribbon) * input.color.a);
            }
            ENDHLSL
        }
    }
}
