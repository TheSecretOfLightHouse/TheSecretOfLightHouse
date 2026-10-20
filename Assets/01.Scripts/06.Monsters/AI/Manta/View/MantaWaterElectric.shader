Shader "Lighthouse/Monsters/MantaWaterElectric"
{
    Properties
    {
        _GlowIntensity ("Glow Intensity", Range(0, 8)) = 2.5
        _FlowSpeed ("Current Flow Speed", Range(0, 10)) = 2
        _DetailScale ("Current Detail Per Metre", Range(0.1, 10)) = 2.5
        _Distortion ("Filament Tremble", Range(0, 0.2)) = 0.12
        _CoreWidth ("Core Width", Range(0.01, 0.15)) = 0.045
        _GlowWidth ("Glow Width", Range(0.05, 0.5)) = 0.26
        _SparkIntensity ("Spark Intensity", Range(0, 8)) = 2.8
        _SparkDensity ("Spark Density", Range(1, 20)) = 7
        [HideInInspector] _Activity ("Discharging", Float) = 1
        [HideInInspector] _EffectTime ("Network Effect Time", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "WaterElectric"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _GlowIntensity;
                float _FlowSpeed;
                float _DetailScale;
                float _Distortion;
                float _CoreWidth;
                float _GlowWidth;
                float _SparkIntensity;
                float _SparkDensity;
                float _Activity;
                float _EffectTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float Hash(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453);
            }

            float Noise(float value)
            {
                float cell = floor(value);
                float fraction = frac(value);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                return lerp(Hash(cell), Hash(cell + 1.0), fraction);
            }

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
                float activity = saturate(_Activity);
                float time = _EffectTime * lerp(0.35, 1.0, activity);
                float along = input.uv.x * _DetailScale;
                float across = input.uv.y - 0.5;
                float flow = along + time * _FlowSpeed;
                float jitter = Noise(along * 2.7 + floor(time * 18.0) * 0.37) * 2.0 - 1.0;
                float center = jitter * _Distortion * lerp(0.2, 1.0, activity);
                float distanceToCore = abs(across - center);
                float antialias = max(fwidth(distanceToCore), 0.003);
                float core = 1.0 - smoothstep(_CoreWidth, _CoreWidth + antialias, distanceToCore);
                float halo = exp(-distanceToCore * distanceToCore / max(_GlowWidth * _GlowWidth, 0.001));
                float edge = 1.0 - smoothstep(0.35, 0.5, abs(across));

                float branchCenter = sin(along * 3.1 - time * 11.0) * 0.2;
                float branch = (1.0 - smoothstep(0.012, 0.012 + antialias, abs(across - branchCenter)))
                    * smoothstep(0.45, 0.85, Noise(flow * 2.0)) * activity;
                float sparks = pow(saturate(sin(flow * _SparkDensity)), 28.0)
                    * halo * _SparkIntensity * activity;
                float ripple = 0.8 + 0.2 * sin(along * 4.0 - time * 3.0 + across * 28.0);
                float flicker = lerp(0.85, 1.15, Noise(time * 35.0));
                float energy = (core + halo * 0.28 * ripple + branch * 0.45 + sparks)
                    * _GlowIntensity * lerp(0.6, 1.0, activity) * flicker;
                half3 tint = lerp(input.color.rgb, half3(1.0, 1.0, 1.0),
                    saturate(core * 0.8 + sparks * 0.3) * activity);
                half alpha = saturate(core + halo * 0.45 + branch + sparks) * edge * input.color.a;
                return half4(tint * energy, alpha);
            }
            ENDHLSL
        }
    }
}
