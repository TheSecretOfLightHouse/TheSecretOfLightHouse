Shader "Lighthouse/World/NightSky"
{
    Properties
    {
        [Header(Day)]
        _DayZenithColor ("Day Zenith", Color) = (0.22, 0.45, 0.78, 1)
        _DayHorizonColor ("Day Horizon", Color) = (0.72, 0.84, 0.93, 1)

        [Header(Night)]
        _NightZenithColor ("Night Zenith", Color) = (0.008, 0.014, 0.04, 1)
        _NightHorizonColor ("Night Horizon", Color) = (0.03, 0.05, 0.09, 1)

        [Header(Twilight)]
        _TwilightColor ("Twilight Glow", Color) = (1.0, 0.42, 0.22, 1)
        _TwilightWidth ("Twilight Width", Range(0.05, 1)) = 0.35

        [Header(Horizon)]
        _HorizonFalloff ("Horizon Falloff", Range(0.1, 2)) = 0.55
        _GroundColor ("Below Horizon", Color) = (0.03, 0.05, 0.08, 1)

        [Header(Sun)]
        _SunColor ("Sun Color", Color) = (1, 0.93, 0.78, 1)
        _SunSize ("Sun Size", Range(0.0005, 0.02)) = 0.004
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.6

        [Header(Debug)]
        _DebugNightBlend ("Night Blend Override (-1 = use global)", Range(-1, 1)) = -1
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "NightSky"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DayZenithColor;
                float4 _DayHorizonColor;
                float4 _NightZenithColor;
                float4 _NightHorizonColor;
                float4 _TwilightColor;
                float _TwilightWidth;
                float _HorizonFalloff;
                float4 _GroundColor;
                float4 _SunColor;
                float _SunSize;
                float _SunGlow;
                float _DebugNightBlend;
            CBUFFER_END

            float _SkyNightBlend;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float GetNightBlend()
            {
                return saturate(_DebugNightBlend >= 0.0 ? _DebugNightBlend : _SkyNightBlend);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float night = GetNightBlend();
                float height = saturate(direction.y);
                float gradient = pow(height, _HorizonFalloff);

                float3 dayColor = lerp(_DayHorizonColor.rgb, _DayZenithColor.rgb, gradient);
                float3 nightColor = lerp(_NightHorizonColor.rgb, _NightZenithColor.rgb, gradient);
                float3 sky = lerp(dayColor, nightColor, night);

                float twilight = 4.0 * night * (1.0 - night);
                sky += _TwilightColor.rgb * twilight * exp(-height / _TwilightWidth);

                float below = saturate(-direction.y * 8.0);
                sky = lerp(sky, _GroundColor.rgb, below);

                float3 sunDirection = normalize(_MainLightPosition.xyz);
                float sunDot = saturate(dot(direction, sunDirection));
                float cosRadius = 1.0 - _SunSize;
                float disc = smoothstep(cosRadius - 0.0006, cosRadius, sunDot);
                float glow = pow(sunDot, 64.0) * _SunGlow;
                sky += _SunColor.rgb * (disc + glow) * (1.0 - night) * (1.0 - below);

                return float4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
