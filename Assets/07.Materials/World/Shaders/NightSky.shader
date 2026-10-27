Shader "Lighthouse/World/NightSky"
{
    Properties
    {
        [Header(Day)]
        _DayZenithColor ("Day Zenith", Color) = (0.22, 0.45, 0.78, 1)
        _DayHorizonColor ("Day Horizon", Color) = (0.72, 0.84, 0.93, 1)

        [Header(Clouds)]
        _CloudColor ("Cloud Color", Color) = (0.93, 0.96, 1, 1)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.6
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.75
        _CloudScale ("Cloud Scale", Range(0.5, 10)) = 3
        _CloudSpeed ("Cloud Drift Speed", Range(0, 0.1)) = 0.008

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

        [Header(Constellations)]
        _ConstellationScale ("Constellation Scale", Range(0.05, 1)) = 0.33
        _ConstellationLineSoftness ("Line Softness", Range(0.0005, 0.01)) = 0.001
        _ConstellationLineStrength ("Line Brightness", Range(0, 1)) = 0.045

        [Header(Aurora Regions)]
        _AuroraMapCenter ("Map Center (World XZ)", Vector) = (0, 0, 0, 0)
        _AuroraBoundaryRadii ("Boundary Radii (X Middle Outer, Y Outer Edge)", Vector) = (360, 490, 0, 0)
        _AuroraBoundaryWeights ("Boundary Enables (X Middle Outer, Y Outer Edge)", Vector) = (1, 1, 0, 0)
        _AuroraBandHalfWidth ("Boundary Band Half Width (Meters)", Range(0, 100)) = 15
        _AuroraFadeWidth ("Boundary Fade Width (Meters)", Range(1, 100)) = 25

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
            #include "Include/SkyCelestialFrame.hlsl"
            #include "Include/SkyStars.hlsl"
            #include "Include/SkyConstellations.hlsl"
            #include "Include/SkyAurora.hlsl"
            #include "Include/SkyMoon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DayZenithColor;
                float4 _DayHorizonColor;
                float4 _CloudColor;
                float _CloudCoverage;
                float _CloudOpacity;
                float _CloudScale;
                float _CloudSpeed;
                float4 _NightZenithColor;
                float4 _NightHorizonColor;
                float4 _TwilightColor;
                float _TwilightWidth;
                float _HorizonFalloff;
                float4 _GroundColor;
                float4 _SunColor;
                float _SunSize;
                float _SunGlow;
                float _ConstellationScale;
                float _ConstellationLineSoftness;
                float _ConstellationLineStrength;
                float4 _AuroraMapCenter;
                float4 _AuroraBoundaryRadii;
                float4 _AuroraBoundaryWeights;
                float _AuroraBandHalfWidth;
                float _AuroraFadeWidth;
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
                float sunVisibility = smoothstep(-0.04, 0.04, sunDirection.y);
                float3 sunColor = lerp(_SunColor.rgb, _TwilightColor.rgb, twilight);
                sky += sunColor * (disc + glow) * sunVisibility
                    * (1.0 - smoothstep(0.65, 1.0, night)) * (1.0 - below);

                float skyTime = _Time.y;

                // Stars and constellations must share the same celestial frame.
                float3 celestialDirection = SkyWorldToCelestial(
                    direction,
                    37.0,
                    0.0);

                float3 stars = SkyStars(
                    celestialDirection,
                    direction.y,
                    90.0,
                    1.2,
                    0.3,
                    skyTime);

                float constellationMask = SkyConstellations(
                    celestialDirection,
                    _ConstellationScale,
                    _ConstellationLineSoftness * _ConstellationScale,
                    0.003 * _ConstellationScale,
                    _ConstellationLineStrength,
                    1.0,
                    float3(1.0, 1.0, 0.0));

                float horizonFade = smoothstep(0.0, 0.1, direction.y);
                float3 constellations =
                    float3(0.55, 0.72, 1.0) * constellationMask * horizonFade;

                // Use each rendering camera so remote players see their own region.
                float mapDistance = distance(_WorldSpaceCameraPos.xz, _AuroraMapCenter.xy);
                float2 boundaryDistance = abs(mapDistance - _AuroraBoundaryRadii.xy);
                float2 boundaryBands = 1.0 - smoothstep(
                    _AuroraBandHalfWidth,
                    _AuroraBandHalfWidth + max(_AuroraFadeWidth, 0.001),
                    boundaryDistance);
                boundaryBands *= saturate(_AuroraBoundaryWeights.xy);
                float auroraVisibility = max(boundaryBands.x, boundaryBands.y);
                float3 aurora = float3(0.0, 0.0, 0.0);
                if (auroraVisibility > 0.001 && night > 0.001)
                {
                    aurora = SkyAurora(
                    direction,
                    skyTime,
                    0.0,
                    0.18,
                    0.15,
                    float3(0.08, 0.65, 0.3),
                    float3(0.4, 0.12, 0.65)) * auroraVisibility;
                }

                float3 moonDirection = normalize(float3(0.4, 0.55, 0.7));
                float4 moon = SkyMoon(
                    direction,
                    moonDirection,
                    0.5,
                    0.035,
                    float3(0.85, 0.9, 1.0),
                    0.15);

                // The moon disc hides stars and aurora behind it.
                float3 nightDetails =
                    (stars + constellations + aurora) * (1.0 - moon.a)
                    + moon.rgb;

                float celestialVisibility = smoothstep(0.65, 1.0, night);
                sky += nightDetails * celestialVisibility * horizonFade;

                // Project onto a high cloud layer; fade the horizon to avoid stretching.
                if (direction.y > 0.01 && _CloudCoverage > 0.001)
                {
                    float2 cloudUv = direction.xz / max(direction.y, 0.15) * _CloudScale;
                    float2 drift = float2(1.0, 0.35) * skyTime * _CloudSpeed;
                    float cloudNoise = SkyFbm(cloudUv + drift);
                    float detail = SkyFbm(cloudUv * 2.7 - drift * 0.4);
                    float density = cloudNoise * 0.8 + detail * 0.2;
                    float threshold = lerp(0.85, 0.2, _CloudCoverage);
                    float cloudMask = smoothstep(threshold, threshold + 0.16, density);
                    cloudMask *= smoothstep(0.02, 0.22, direction.y);
                    cloudMask *= _CloudOpacity * (1.0 - night);
                    float3 cloudColor = _CloudColor.rgb * lerp(0.72, 1.0, detail);
                    cloudColor = lerp(cloudColor, _TwilightColor.rgb * 0.8, twilight * 0.65);
                    sky = lerp(sky, cloudColor, cloudMask);
                }

                return float4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
