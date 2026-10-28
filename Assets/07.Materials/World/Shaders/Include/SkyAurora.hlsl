#ifndef LIGHTHOUSE_SKY_AURORA_INCLUDED
#define LIGHTHOUSE_SKY_AURORA_INCLUDED

#include "SkyNoise.hlsl"

float3 SkyAurora(float3 direction, float time, float azimuthRadians, float baseElevation, float height, float3 lowColor, float3 highColor)
{
    float2 horizontal = direction.xz;
    float horizontalLength = length(horizontal);
    if (horizontalLength < 0.001)
    {
        return float3(0.0, 0.0, 0.0);
    }

    horizontal /= horizontalLength;
    float facing = dot(horizontal, float2(sin(azimuthRadians), cos(azimuthRadians)));
    float sector = smoothstep(0.1, 0.75, facing);
    if (sector <= 0.0)
    {
        return float3(0.0, 0.0, 0.0);
    }

    float azimuth = atan2(horizontal.x, horizontal.y);
    float fold = SkyFbm(float2(azimuth * 2.0 + time * 0.03, time * 0.02));
    float relative = direction.y - (baseElevation + (fold - 0.5) * 0.22);

    float rays = SkyValueNoise(float2(azimuth * 38.0 + fold * 6.0, time * 0.15));
    float edgeNoise = SkyValueNoise(float2(azimuth * 9.0 - time * 0.04, direction.y * 6.0 + time * 0.03));
    float softness = lerp(0.05, 0.2, edgeNoise);
    float shifted = relative + (rays - 0.5) * 0.05;
    float vertical = smoothstep(-softness, softness, shifted) * exp(-max(shifted, 0.0) / height);
    float drift = 0.65 + 0.35 * SkyValueNoise(float2(azimuth * 6.0, direction.y * 14.0 - time * 0.6));
    float horizonFade = smoothstep(-0.04, 0.06, direction.y);
    float intensity = vertical * (0.35 + 0.65 * rays * rays) * drift * sector * horizonFade;

    float3 color = lerp(lowColor, highColor, saturate(relative / (height * 2.5)));
    return color * intensity;
}

#endif
