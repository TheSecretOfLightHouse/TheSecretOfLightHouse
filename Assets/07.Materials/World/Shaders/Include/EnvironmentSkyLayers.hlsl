#ifndef LIGHTHOUSE_ENVIRONMENT_SKY_LAYERS_INCLUDED
#define LIGHTHOUSE_ENVIRONMENT_SKY_LAYERS_INCLUDED

#include "SkyNoise.hlsl"

float3 EnvironmentNebula(
    float3 direction,
    float time,
    float scale,
    float filamentStrength)
{
    float2 uv = direction.xz / max(0.35, direction.y + 0.5);
    uv *= scale;

    float2 drift = float2(time * 0.008, -time * 0.003);
    float warp = SkyFbm(uv * 0.65 + drift);
    float density = SkyFbm(uv + float2(warp, -warp) * 1.8);

    float ribbon = 0.5 + 0.5 *
        sin(uv.y * 5.0 + warp * 9.0 + density * 3.0);

    float threads = pow(saturate(ribbon), 12.0);
    float veil = smoothstep(0.35, 0.8, density) * 0.3;
    float flow = veil + threads * density * filamentStrength;
    float horizonFade = smoothstep(0.0, 0.18, direction.y);

    return float3(flow, flow, flow) * horizonFade;
}

float EnvironmentCloudLayer(
    float3 direction,
    float time,
    float coverage,
    float scale,
    float3 sunDirection,
    out float light)
{
    float2 uv = direction.xz / (saturate(direction.y) + 0.15);
    float2 drift = float2(time * 0.008, time * 0.003);
    float2 baseUv = uv * scale * 0.55 + drift;

    float baseDensity = SkyFbm(baseUv);
    float detail = SkyFbm(uv * scale * 2.1 - drift * 1.6);
    float density = (baseDensity * 0.78 + detail * 0.22 - 0.5) * 1.6 + 0.5;

    float2 towardSun = normalize(sunDirection.xz + float2(0.0001, 0.0001)) * 0.12;
    float sunSide = (SkyFbm((uv + towardSun) * scale * 0.55 + drift) * 0.78 + detail * 0.22 - 0.5) * 1.6 + 0.5;

    float threshold = lerp(1.0, 0.1, saturate(coverage)) -
        smoothstep(0.8, 1.0, coverage) * 0.35;
    float mask = smoothstep(threshold, threshold + 0.08, density);
    float core = smoothstep(threshold, threshold + 0.3, density);

    light = saturate(0.55 + (density - sunSide) * 5.0) * lerp(1.0, 0.78, core);

    mask *= smoothstep(0.0, 0.12, direction.y);
    return mask * saturate(coverage * 8.0);
}

float EnvironmentCloudMask(
    float3 direction,
    float time,
    float coverage,
    float scale)
{
    float light;
    return EnvironmentCloudLayer(direction, time, coverage, scale, float3(0.0, 1.0, 0.0), light);
}

#endif
