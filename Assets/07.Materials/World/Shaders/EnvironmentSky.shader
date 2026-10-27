Shader "Lighthouse/World/EnvironmentSky"
{
    Properties
    {
        [HDR] _Zenith ("Zenith", Color) = (0.008, 0.014, 0.04, 1)
        [HDR] _Horizon ("Horizon", Color) = (0.025, 0.08, 0.15, 1)
        [HDR] _NebulaColor ("Nebula Color", Color) = (0.15, 0.38, 0.7, 1)
        _EnvironmentValues ("Stars Nebula Clouds Light", Vector) = (1, 0.65, 0.1, 1)
        _Daylight ("Daylight", Range(0, 1)) = 0
        _PatternScale ("Pattern Scale", Range(0.5, 8)) = 3
        _FilamentStrength ("Filament Strength", Range(0, 2)) = 0.7
        [HideInInspector] _EnvironmentTime ("Environment Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Include/SkyStars.hlsl"
            #include "Include/EnvironmentSkyLayers.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Zenith;
                float4 _Horizon;
                float4 _NebulaColor;
                float4 _EnvironmentValues;
                float _Daylight;
                float _PatternScale;
                float _FilamentStrength;
                float _EnvironmentTime;
            CBUFFER_END

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

            float4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float height = saturate(direction.y);
                float gradient = pow(height, 0.55);
                float time = _EnvironmentTime;

                float3 sky = lerp(_Horizon.rgb, _Zenith.rgb, gradient);

                float3 stars = SkyStars(
                    direction, direction.y, 90.0,
                    _EnvironmentValues.x, 0.3, time);

                float3 nebula = EnvironmentNebula(
                    direction, time, _PatternScale, _FilamentStrength);

                sky += stars;
                sky += nebula * _NebulaColor.rgb * _EnvironmentValues.y;

                float3 lightDirection = _MainLightPosition.xyz;
                float lengthSquared = dot(lightDirection, lightDirection);
                lightDirection *= rsqrt(max(lengthSquared, 0.0001));

                float sunDot = saturate(dot(direction, lightDirection));
                float disc = smoothstep(0.9994, 0.9998, sunDot);
                float glow = pow(sunDot, 96.0) * 0.25;
                float sunVisibility = smoothstep(-0.02, 0.05, lightDirection.y);
                float3 sunColor = float3(1.0, 0.93, 0.8);

                sky += sunColor * (disc + glow) *
                    sunVisibility * _Daylight * _EnvironmentValues.w;

                float cloudMask = EnvironmentCloudMask(
                    direction, time, _EnvironmentValues.z, _PatternScale);

                float3 nightCloud = _Horizon.rgb * 0.55;
                float3 dayCloud = lerp(float3(0.92, 0.9, 0.96), _Horizon.rgb * 1.25, 0.3) *
                    _EnvironmentValues.w;

                float3 cloudColor = lerp(nightCloud, dayCloud, _Daylight);
                sky = lerp(sky, cloudColor, cloudMask * 0.9);

                float below = saturate(-direction.y * 5.0);
                sky = lerp(sky, _Horizon.rgb * 0.35, below);

                return float4(max(sky, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
}
