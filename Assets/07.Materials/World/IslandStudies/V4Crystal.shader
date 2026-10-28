Shader "Lighthouse/World/PrismaticCrystalV4"
{
    Properties
    {
        [HDR] _CoreColor("Core Glow", Color) = (0.08,0.9,1.7,1)
        [HDR] _EdgeColor("Prismatic Edge", Color) = (1.2,0.15,2.5,1)
        _Opacity("Transmission", Range(0,1)) = 0.48
        _Pulse("Shimmer", Range(0,1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
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
            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _EdgeColor;
                float _Opacity;
                float _Pulse;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0 - abs(dot(normal,view)), 2.2);
                half band = sin(input.positionWS.y * 2.4 + input.positionWS.x * 1.3 + _Time.y * 0.55) * 0.5 + 0.5;
                half3 glow = lerp(_CoreColor.rgb, _EdgeColor.rgb, saturate(fresnel * 0.7 + band * 0.18));
                glow *= 0.55 + fresnel * 1.4 + band * _Pulse;
                return half4(MixFog(glow,input.fog), saturate(_Opacity + fresnel * 0.35));
            }
            ENDHLSL
        }
    }
}
