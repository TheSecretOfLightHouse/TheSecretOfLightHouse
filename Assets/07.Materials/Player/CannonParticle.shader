Shader "Lighthouse/Player/CannonParticle"
{
    Properties
    {
        [Enum(Smoke,0,Flash,1)] _EffectType("Effect Type", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _EffectType;
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

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 weight = frac(p);
                weight = weight * weight * (3.0 - 2.0 * weight);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), weight.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1.0), weight.x), weight.y);
            }

            float Cloud(float2 p)
            {
                return Noise(p) * 0.58 + Noise(p * 2.1 + 9.7) * 0.28
                    + Noise(p * 4.3 + 27.1) * 0.14;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float alpha;
                float3 color;
                if (_EffectType > 0.5)
                {
                    float2 stretched = p * float2(0.82, 1.25);
                    float radius = length(stretched);
                    float angle = atan2(stretched.y, stretched.x);
                    float rays = pow(abs(cos(angle * 3.0)), 14.0);
                    float boundary = 0.19 + rays * 0.63;
                    alpha = 1.0 - smoothstep(boundary - 0.055, boundary + 0.025, radius);
                    float core = 1.0 - smoothstep(0.02, 0.28, radius);
                    color = lerp(float3(1.0, 0.28, 0.015), float3(2.2, 1.95, 1.2), core);
                }
                else
                {
                    float noise = Cloud(p * 3.1 + float2(4.7, 1.3));
                    float radius = length(p);
                    float boundary = 0.65 + (noise - 0.5) * 0.55;
                    alpha = (1.0 - smoothstep(boundary - 0.18, boundary + 0.13, radius)) * 0.92;
                    float shade = 0.67 + noise * 0.33 + (p.y + 1.0) * 0.055;
                    color = shade.xxx;
                }
                alpha *= input.color.a;
                color *= input.color.rgb;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
