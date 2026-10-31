Shader "Lighthouse/World/ReefFoam"
{
    Properties
    {
        _BaseMap("Foam Noise", 2D) = "white" {}
        _BaseColor("Foam Color", Color) = (0.82,0.95,0.94,0.64)
        _NoiseScale("Noise Scale", Float) = 0.85
        _DriftSpeed("Drift Speed", Float) = 0.16
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+65" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _NoiseScale;
                float _DriftSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.uv = input.uv;
                output.fog = ComputeFogFactor(p.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // U follows the shoreline; V is distance away from the rock in metres.
                // Increasing time moves equal-phase crests towards increasing V.
                float along = input.uv.x;
                float distance = input.uv.y;
                float bend = sin(along*0.63)*0.009 + sin(along*1.71)*0.004;
                float phase = (distance + bend - _Time.y*_DriftSpeed) / 0.34;
                float crestDistance = abs(frac(phase)-0.5) * 0.34;
                half crest = exp(-pow(crestDistance/0.023,2));
                half wash = exp(-pow(crestDistance/0.052,2)) * 0.16;
                half arcNoise = sin(along*0.87 + sin(along*0.29)*1.7 + floor(phase)*1.8)*0.5+0.5;
                half arc = smoothstep(0.28,0.62,arcNoise);
                half edge = smoothstep(-0.04,0.08,distance) * (1-smoothstep(0.62,1.04,distance));
                half grain = 0.83 + 0.17*sin(along*23 + sin(along*9));
                half alpha = _BaseColor.a * (crest+wash) * arc * edge * grain;
                return half4(MixFog(_BaseColor.rgb,input.fog),alpha);
            }
            ENDHLSL
        }
    }
}
