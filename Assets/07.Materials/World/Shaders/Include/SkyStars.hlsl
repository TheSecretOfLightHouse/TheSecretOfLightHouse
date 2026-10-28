#ifndef LIGHTHOUSE_SKY_STARS_INCLUDED
#define LIGHTHOUSE_SKY_STARS_INCLUDED

#include "SkyNoise.hlsl"

float3 SkyStarLayer(float3 starDirection, float scale, float radius, float seed, float twinkle, float time)
{
    float3 p = starDirection * scale;
    float3 cell = floor(p);
    float3 hash = SkyHash33(cell + seed);
    float3 position = cell + 0.2 + 0.6 * hash;

    float spot = 1.0 - smoothstep(0.0, radius, distance(p, position));
    spot *= spot;

    float3 hash2 = SkyHash33(cell + seed + 17.7);
    float magnitude = lerp(0.25, 1.0, hash2.x * hash2.x);
    float flicker = 1.0 - twinkle * 0.5 * (1.0 + sin(time * (2.0 + hash2.y * 4.0) + hash2.z * 6.2831853));
    float3 tint = lerp(float3(0.70, 0.82, 1.0), float3(1.0, 0.86, 0.70), hash2.y);

    return tint * spot * magnitude * flicker;
}

float3 SkyStars(float3 starDirection, float height, float density, float brightness, float twinkle, float time)
{
    float3 fine = SkyStarLayer(starDirection, density, 0.12, 0.0, twinkle, time);
    float3 bright = SkyStarLayer(starDirection, density * 0.45, 0.15, 41.0, twinkle, time) * 1.6;
    float horizonFade = smoothstep(0.0, 0.2, height);
    return (fine + bright) * brightness * horizonFade;
}

#endif
