Shader "Lighthouse/World/IslandTerrainV8"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _DetailScale("Surface Detail Scale", Float) = 2.4
        _Variation("Surface Variation", Range(0,0.4)) = 0.16
        _WetLevel("Wet Shore Height", Float) = 0.7
        _SandMap("Synty Shore Texture", 2D) = "white" {}
        _GrassMap("Synty Inland Texture", 2D) = "white" {}
        _RockMap("Synty Rock Texture", 2D) = "white" {}
        _SandTint("Shore Tint", Color) = (1,1,1,1)
        _GrassTint("Inland Tint", Color) = (1,1,1,1)
        _RockTint("Rock Tint", Color) = (1,1,1,1)
        _TextureScale("World Texture Scale", Float) = 0.22
        _ShoreGlow("Crystal Shore Shimmer", Float) = 0
        _WorkMap("Packed Soil Texture", 2D) = "white" {}
        _WorkTint("Packed Soil Tint", Color) = (0.8,0.65,0.45,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _DetailScale;
                float _Variation;
                float _WetLevel;
                float4 _SandTint;
                float4 _GrassTint;
                float4 _RockTint;
                float _TextureScale;
                float _ShoreGlow;
                float4 _WorkTint;
            CBUFFER_END
            TEXTURE2D(_WorkMap); SAMPLER(sampler_WorkMap);
            TEXTURE2D(_SandMap); SAMPLER(sampler_SandMap);
            TEXTURE2D(_GrassMap); SAMPLER(sampler_GrassMap);
            TEXTURE2D(_RockMap); SAMPLER(sampler_RockMap);
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float4 shadowCoord : TEXCOORD2;
                half fog : TEXCOORD3;
            };
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453);
            }
            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f*f*(3.0-2.0*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.shadowCoord = GetShadowCoord(p);
                output.fog = ComputeFogFactor(p.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                float broad = Noise(input.positionWS.xz * 0.42);
                float fine = Noise(input.positionWS.xz * _DetailScale);
                float strata = sin(input.positionWS.y * 3.2 + broad * 2.0) * 0.035;
                half3 weights = max(input.color.rgb,0);
                weights /= max(dot(weights,half3(1,1,1)),0.001);
                float3 uv = input.positionWS * _TextureScale;
                half3 tri = pow(abs(n),4);
                tri /= max(dot(tri,half3(1,1,1)),0.001);
                half3 sand = SAMPLE_TEXTURE2D(_SandMap,sampler_SandMap,frac(uv.xz)).rgb * _SandTint.rgb;
                half3 grass = SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,frac(uv.xz)).rgb * _GrassTint.rgb;
                half3 rock = (SAMPLE_TEXTURE2D(_RockMap,sampler_RockMap,frac(uv.zy)).rgb * tri.x
                    + SAMPLE_TEXTURE2D(_RockMap,sampler_RockMap,frac(uv.xz)).rgb * tri.y
                    + SAMPLE_TEXTURE2D(_RockMap,sampler_RockMap,frac(uv.xy)).rgb * tri.z) * _RockTint.rgb;
                half rockValue = dot(rock / max(_RockTint.rgb,0.01),half3(0.2126,0.7152,0.0722));
                rock = rockValue * _RockTint.rgb * 1.6;
                half3 albedo = (sand*weights.r + grass*weights.g + rock*weights.b) * _BaseColor.rgb;
                half3 work = SAMPLE_TEXTURE2D(_WorkMap,sampler_WorkMap,frac(uv.xz)).rgb * _WorkTint.rgb * _BaseColor.rgb;
                albedo = lerp(albedo,work,1-saturate(input.color.a));
                albedo *= 1.0 + (broad - 0.5) * _Variation + (fine - 0.5) * _Variation * 0.42;
                albedo *= 1.0 + strata * (1.0 - saturate(n.y));
                float wet = 1.0-smoothstep(_WetLevel-0.3,_WetLevel+0.9,input.positionWS.y);
                albedo *= lerp(1.0,0.65,wet);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS + n * 0.035));
                half diffuse = saturate(dot(n,light.direction));
                half3 ambient = max(SampleSH(n),half3(0.12,0.14,0.16));
                half3 result = albedo * (ambient * 0.8 + light.color * diffuse * 0.65 * light.shadowAttenuation * light.distanceAttenuation);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 h = normalize(light.direction + view);
                result += light.color * pow(saturate(dot(n,h)),48) * wet * 0.16 * light.shadowAttenuation;
                half glint = pow(saturate(1-abs(sin(input.positionWS.x*2.1 + input.positionWS.z*1.4 + broad*6))),32);
                result += half3(0.12,0.45,0.65)*glint*weights.b*wet*_ShoreGlow;
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light localLight = GetAdditionalLight(lightIndex,input.positionWS);
                    result += albedo * localLight.color * saturate(dot(n,localLight.direction)) * localLight.distanceAttenuation * 0.8;
                LIGHT_LOOP_END
                #endif
                return half4(MixFog(result,input.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    Fallback "Universal Render Pipeline/Lit"
}

