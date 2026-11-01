Shader "Lighthouse/Boss/WaterEmergenceFoam"
{
    Properties
    {
        _Tint ("Foam tint", Color) = (0.6, 0.78, 0.9, 1)
        _Center ("World center", Vector) = (0, 0, 0, 0)
        _Radius ("Radius", Float) = 8
        _Opacity ("Opacity", Float) = 0
        _Inner ("Inner radius fraction", Float) = 0.55
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/07.Materials/World/Shaders/Include/OceanGerstner.hlsl"
            #include "Assets/07.Materials/World/Shaders/Include/OceanInteraction.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _Center;
                float _Radius;
                float _Opacity;
                float _Inner;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 ring : TEXCOORD0;
                float2 rest : TEXCOORD1;
                float fog : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 rest = float3(_Center.x + input.positionOS.x * _Radius, 0,
                    _Center.z + input.positionOS.z * _Radius);
                float3 position;
                float3 normal;
                float4 coast;
                float lagoon;
                // Match the production surface, including shore attenuation and contact ripples.
                OceanGerstnerFieldLagoon_float(rest, position, normal, coast, lagoon);
                position.y += OceanInteraction(position.xz).x + 0.065;
                output.positionCS = TransformWorldToHClip(position);
                output.ring = input.positionOS.xz;
                output.rest = rest.xz;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.ring);
                float warp = (FoamNoise(input.rest * 0.43 + _OceanTime * 0.08) - 0.5) * 0.09;
                float band = smoothstep(_Inner, _Inner + 0.13, radius + warp)
                    * (1 - smoothstep(0.88, 1, radius + warp));
                float detail = FoamNoise(input.rest * 1.9 + float2(_OceanTime * 0.18, 0));
                float micro = FoamNoise(input.rest * 5.3 - _OceanTime * 0.12);
                float lace = smoothstep(0.4, 0.67, detail) * lerp(0.5, 1, micro);
                float alpha = band * lace * _Opacity;
                return half4(MixFog(_Tint.rgb, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
