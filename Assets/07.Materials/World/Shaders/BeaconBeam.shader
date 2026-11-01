Shader "Lighthouse/World/BeaconBeam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.0, 0.82, 0.45, 1.0)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _EdgePower ("Edge Softness", Range(0.2, 8)) = 2.5
        _LengthFalloff ("Length Falloff", Range(0, 6)) = 1.4
        _ApexFade ("Apex Fade", Range(0.001, 0.5)) = 0.06
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgePower;
                half _LengthFalloff;
                half _ApexFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float along = input.uv.y;
                float facing = abs(dot(normalize(input.normalWS), normalize(GetWorldSpaceViewDir(input.positionWS))));
                float core = pow(saturate(facing), _EdgePower);
                float falloff = pow(saturate(1.0 - along), _LengthFalloff) * smoothstep(0.0, _ApexFade, along);
                float amount = core * falloff * _Intensity;
                return half4(_Color.rgb * amount, amount);
            }
            ENDHLSL
        }
    }
}
