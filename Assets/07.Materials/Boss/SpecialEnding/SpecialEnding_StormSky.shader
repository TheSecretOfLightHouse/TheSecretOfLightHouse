// Procedural stormy night sky: two layers of drifting overcast, a moon glow behind the clouds,
// lightning bolts with cloud flashes, and a horizon mist band that matches the scene fog.
// Works both as RenderSettings.skybox and on a skydome mesh, since the view direction comes from world position.
// Lightning uses cinematic-owned material properties, isolated from world weather globals.
Shader "Lighthouse/Boss/SpecialEndingStormSky"
{
    Properties
    {
        _WeatherProgress ("Clear To Storm", Range(0, 1)) = 1
        _CinematicTime ("Timeline Time", Float) = -1
        _WeatherFrontDirection ("Diagonal Cloud Front", Vector) = (-0.75, 0.65, 0, 0)
        [Header(Gradient)]
        _ZenithColor ("Zenith Color", Color) = (0.07, 0.09, 0.11, 1)
        _HorizonColor ("Horizon Color", Color) = (0.17, 0.22, 0.25, 1)
        _HorizonFogBlend ("Horizon Uses Fog Color", Range(0, 1)) = 1
        _GradientHeight ("Gradient Height", Range(0.05, 1)) = 0.5

        [Header(Overcast Layer)]
        _CloudDarkColor ("Dark Color", Color) = (0.08, 0.1, 0.12, 1)
        _CloudLitColor ("Lit Color", Color) = (0.25, 0.3, 0.34, 1)
        _CloudCoverage ("Coverage", Range(0, 1)) = 0.8
        _CloudSoftness ("Softness", Range(0.01, 0.5)) = 0.22
        _CloudScale ("Scale", Float) = 0.9
        _CloudOpacity ("Opacity", Range(0, 1)) = 0.95
        _CloudWind ("Wind (XY)", Vector) = (0.01, 0.004, 0, 0)
        _CloudShading ("Self Shading", Range(0, 8)) = 3.5

        [Header(Low Scud Layer)]
        _ScudColor ("Color", Color) = (0.055, 0.07, 0.08, 1)
        _ScudCoverage ("Coverage", Range(0, 1)) = 0.45
        _ScudScale ("Scale", Float) = 1.8
        _ScudOpacity ("Opacity", Range(0, 1)) = 0.75
        _ScudSpeed ("Speed Multiplier", Float) = 2.5

        [Header(Moon)]
        _MoonDirection ("Moon Direction (World)", Vector) = (0.4, 0.42, 0.8, 0)
        _MoonColor ("Moon Color", Color) = (0.8, 0.86, 0.9, 1)
        _MoonSize ("Moon Angular Radius", Range(0.005, 0.1)) = 0.03
        _MoonOpacity ("Disc Opacity", Range(0, 1)) = 0.35
        _MoonHaloColor ("Halo Color", Color) = (0.22, 0.28, 0.33, 1)
        _MoonHaloFalloff ("Halo Falloff", Range(2, 256)) = 12
        _MoonGlowFalloff ("Wide Glow Falloff", Range(1, 16)) = 3

        [Header(Stars)]
        _StarDensity ("Density", Range(0, 1)) = 0.2
        _StarBrightness ("Brightness", Range(0, 4)) = 0
        _StarTwinkleSpeed ("Twinkle Speed", Float) = 3

        [Header(Lightning)]
        [HDR] _BoltColor ("Bolt Color", Color) = (3.2, 3.6, 4.2, 1)
        _FlashColor ("Cloud Flash Color", Color) = (0.55, 0.65, 0.8, 1)
        _BoltWidth ("Bolt Width", Range(0.0005, 0.01)) = 0.0018
        _LightningFlash ("Cinematic Flash", Float) = 0
        _LightningBolt ("Cinematic Bolt", Vector) = (0, 1, 0, 0.7)

        [Header(Horizon Mist)]
        _MistHeight ("Height", Range(0.01, 0.5)) = 0.16
        _MistOpacity ("Opacity", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define TAU 6.2831853

            CBUFFER_START(UnityPerMaterial)
                float _WeatherProgress;
                float _CinematicTime;
                float4 _WeatherFrontDirection;
                float4 _ZenithColor;
                float4 _HorizonColor;
                float _HorizonFogBlend;
                float _GradientHeight;
                float4 _CloudDarkColor;
                float4 _CloudLitColor;
                float _CloudCoverage;
                float _CloudSoftness;
                float _CloudScale;
                float _CloudOpacity;
                float4 _CloudWind;
                float _CloudShading;
                float4 _ScudColor;
                float _ScudCoverage;
                float _ScudScale;
                float _ScudOpacity;
                float _ScudSpeed;
                float4 _MoonDirection;
                float4 _MoonColor;
                float _MoonSize;
                float _MoonOpacity;
                float4 _MoonHaloColor;
                float _MoonHaloFalloff;
                float _MoonGlowFalloff;
                float _StarDensity;
                float _StarBrightness;
                float _StarTwinkleSpeed;
                float4 _BoltColor;
                float4 _FlashColor;
                float _BoltWidth;
                float _LightningFlash;
                float4 _LightningBolt;
                float _MistHeight;
                float _MistOpacity;
            CBUFFER_END

            // Globals set by NightStorm. x: azimuth (rad), y: seed, z: bolt intensity, w: bolt top elevation (sin).

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            float Hash11(float x)
            {
                return frac(sin(x * 127.1) * 43758.5453);
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                // Rotating each octave hides the value-noise grid.
                const float2x2 octave = float2x2(1.6, 1.2, -1.2, 1.6);
                float sum = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    sum += amp * ValueNoise(p);
                    p = mul(octave, p);
                    amp *= 0.5;
                }
                return sum;
            }

            // Piecewise-linear noise gives the kinked strokes of a bolt; smooth noise looks like a rope.
            float Jagged(float x)
            {
                float i = floor(x);
                return lerp(Hash11(i), Hash11(i + 1.0), frac(x));
            }

            float BoltPath(float t, float seed)
            {
                return (Jagged(t * 7.0 + seed) - 0.5) * 0.1
                    + (Jagged(t * 23.0 + seed * 1.7) - 0.5) * 0.035
                    + (Jagged(t * 61.0 + seed * 2.3) - 0.5) * 0.01;
            }

            float3 Lightning(float3 dir, float h)
            {
                float intensity = _LightningBolt.z;
                if (intensity <= 0.001)
                {
                    return 0;
                }

                float seed = _LightningBolt.y;
                float top = _LightningBolt.w;
                float t = 1.0 - h / top;
                float inRange = smoothstep(0.0, 0.06, t) * step(t, 1.05);

                float da = atan2(dir.x, dir.z) - _LightningBolt.x;
                da -= TAU * round(da / TAU);
                float x = da * sqrt(saturate(1.0 - h * h));

                float width = _BoltWidth * (1.0 + t * 0.5);
                float d = abs(x - BoltPath(t, seed));
                float core = 1.0 - smoothstep(width * 0.3, width, d);
                float glow = exp(-d * 35.0) * 0.35;

                // One branch splitting off partway down and fading out.
                float tb = 0.25 + Hash11(seed) * 0.35;
                float side = Hash11(seed + 3.1) < 0.5 ? -1.0 : 1.0;
                float bt = t - tb;
                float branchFade = step(0.0, bt) * saturate(1.0 - bt / 0.35);
                float bd = abs(x - (BoltPath(tb, seed) + side * bt * 0.18 + (Jagged(t * 29.0 + seed * 5.1) - 0.5) * 0.02));
                float bCore = (1.0 - smoothstep(width * 0.2, width * 0.7, bd)) * branchFade;
                float bGlow = exp(-bd * 45.0) * 0.2 * branchFade;

                float bolt = (core + bCore * 0.7) * inRange;
                float boltGlow = (glow + bGlow) * inRange;
                return _BoltColor.rgb * (bolt + boltGlow * 0.25) * intensity;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.positionWS - _WorldSpaceCameraPos);
                float h = dir.y;
                float time = _CinematicTime >= 0 ? _CinematicTime : _Time.y;
                // A moving front covers the sky from one side before closing overhead.
                float frontEdge = (Fbm(dir.xz * 4.0 + float2(time * 0.05, 0)) - 0.5) * 0.8;
                float front = smoothstep(-0.65, 0.65, _WeatherProgress * 3.0 - 1.5
                    + dot(dir, normalize(_WeatherFrontDirection.xyz)) + frontEdge);
                front = _WeatherProgress <= 0 ? 0 : (_WeatherProgress >= 1 ? 1 : front);

                // Horizon follows the fog colour so islands fade into the sky without a seam.
                float3 horizon = lerp(_HorizonColor.rgb, unity_FogColor.rgb, _HorizonFogBlend);
                float g = saturate(h / _GradientHeight);
                g = 1.0 - (1.0 - g) * (1.0 - g);
                float3 col = lerp(horizon, _ZenithColor.rgb, g);
                float3 clearSky = lerp(float3(0.55, 0.74, 0.9), float3(0.08, 0.3, 0.62), g);
                col = lerp(clearSky, col, front);

                float3 moonDir = normalize(_MoonDirection.xyz);
                float md = dot(dir, moonDir);
                float halo = pow(saturate(md), _MoonHaloFalloff);
                float glow = pow(saturate(md), _MoonGlowFalloff) * 0.25;

                // Flash is brightest toward the strike but lights the whole cloud deck a little.
                float strikeDa = atan2(dir.x, dir.z) - _LightningBolt.x;
                float toward = pow(saturate(cos(strikeDa) * 0.5 + 0.5), 3.0);
                float flash = _LightningFlash * (0.25 + 0.75 * toward);

                // Stars: one candidate per 3D cell on the view sphere.
                float3 sp = dir * 220.0;
                float3 cell = floor(sp);
                float3 rnd = Hash33(cell);
                float3 rndPos = Hash33(cell + 17.13);
                float starDist = length(sp - (cell + 0.5 + (rndPos - 0.5) * 0.6));
                float isStar = step(1.0 - _StarDensity * 0.05, rnd.x);
                float twinkle = 0.55 + 0.45 * sin(time * _StarTwinkleSpeed * (0.5 + rnd.y) + rnd.z * TAU);
                float star = isStar * smoothstep(0.3, 0.0, starDist) * twinkle * (0.3 + 0.7 * rnd.y)
                    * _StarBrightness * smoothstep(0.02, 0.3, h);

                col += _MoonHaloColor.rgb * (glow + halo) * front;
                col += _FlashColor.rgb * flash * 0.15;

                // Overcast deck projected on a flat ceiling so it compresses toward the horizon.
                float2 uv = dir.xz / (max(h, 0.0) + 0.1) * _CloudScale;
                float2 wind = _CloudWind.xy * time;
                float warp = Fbm(uv * 0.6 - wind * 0.5);
                float n = Fbm(uv + wind + warp * 0.8);
                float threshold = lerp(0.72, 0.28, _CloudCoverage);
                float cloud = smoothstep(threshold - _CloudSoftness, threshold + _CloudSoftness, n);
                cloud *= smoothstep(-0.02, 0.08, h) * front;

                // Cheap self-shadowing: density sampled a step toward the moon. Thinning toward the light = lit rim.
                float2 lightStep = normalize(moonDir.xz + 1e-4) * 0.12;
                float nToLight = Fbm(uv + wind + warp * 0.8 + lightStep);
                float lit = saturate(0.5 + (n - nToLight) * _CloudShading);
                float thickness = smoothstep(threshold, threshold + 0.3, n);
                lit *= 1.0 - thickness * 0.5;
                float3 cloudCol = lerp(_CloudDarkColor.rgb, _CloudLitColor.rgb, lit);
                cloudCol += _MoonHaloColor.rgb * glow * 1.5;
                cloudCol += _FlashColor.rgb * flash * (0.35 + lit * 0.6);

                col += star * (1.0 - cloud);
                col = lerp(col, cloudCol, cloud * _CloudOpacity);
                // Moonlight leaking through thin cloud.
                col += _MoonHaloColor.rgb * halo * 0.6 * cloud * (1.0 - thickness);

                float angle = acos(clamp(md, -1.0, 1.0));
                float disc = smoothstep(_MoonSize, _MoonSize * 0.8, angle);
                float3 rel = (dir - moonDir) / _MoonSize;
                float mottle = Fbm(rel.xy * 1.5 + rel.z);
                float3 moon = _MoonColor.rgb * (0.75 + 0.35 * mottle);
                col = lerp(col, moon, disc * _MoonOpacity * front * (1.0 - cloud * _CloudOpacity * 0.85));

                // Low, fast, dark scud under the deck sells the wind.
                float2 scudUv = dir.xz / (max(h, 0.0) + 0.05) * _ScudScale;
                float scudN = Fbm(scudUv + wind * _ScudSpeed + warp * 0.5);
                float scudThreshold = lerp(0.72, 0.28, _ScudCoverage);
                float scud = smoothstep(scudThreshold - 0.15, scudThreshold + 0.2, scudN) * smoothstep(-0.02, 0.12, h);
                float3 scudCol = _ScudColor.rgb + _FlashColor.rgb * flash * 0.25;
                col = lerp(col, scudCol, scud * _ScudOpacity * front);

                col += Lightning(dir, h);

                // Low drifting mist band; normalized xz avoids the atan2 seam behind the camera.
                float2 around = normalize(dir.xz + 1e-5) * 3.0;
                float mistNoise = Fbm(around + float2(time * 0.01, h * 10.0 - time * 0.02));
                float mist = (1.0 - smoothstep(0.0, _MistHeight, h)) * _MistOpacity * (0.6 + 0.4 * mistNoise);
                float3 mistCol = lerp(float3(0.55, 0.74, 0.9), horizon, front) + _FlashColor.rgb * flash * 0.2;
                col = lerp(col, mistCol, saturate(mist + step(h, 0.0)));

                // Dark gradients band badly in 8-bit; dither them.
                col += (Hash21(input.positionCS.xy) - 0.5) / 255.0;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
