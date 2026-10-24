#ifndef LIGHTHOUSE_SKY_CONSTELLATIONS_INCLUDED
#define LIGHTHOUSE_SKY_CONSTELLATIONS_INCLUDED

#include "SkyConstellationData.hlsl"

float SkyConstellations(float3 celestialDirection, float figureScale, float lineSoftness, float dotRadius, float lineStrength, float dotStrength, float3 setWeights)
{
    float inverseScale = 1.0 / figureScale;
    float result = 0.0;

    [loop]
    for (int c = 0; c < kConstellationCount; c++)
    {
        float weight = setWeights[kConstellationSet[c]];
        float4 bounds = kConstellationBounds[c];
        if (weight <= 0.001 || dot(celestialDirection, bounds.xyz) < bounds.w)
        {
            continue;
        }

        float3 local = normalize(bounds.xyz + (celestialDirection - bounds.xyz) * inverseScale);
        if (dot(local, bounds.xyz) < bounds.w)
        {
            continue;
        }

        float lineMask = 0.0;
        float dotMask = 0.0;

        [loop]
        for (int i = kConstellationStart[c]; i < kConstellationStart[c + 1]; i++)
        {
            float3 a = kSegmentA[i];
            float3 b = kSegmentB[i];
            float3 n = kSegmentN[i];

            float isInside = step(0.0, dot(cross(a, local), n)) * step(0.0, dot(cross(local, b), n));
            float lineAngle = lerp(10.0, abs(dot(local, n)), isInside) * figureScale;
            float endAngle = min(distance(local, a), distance(local, b)) * figureScale;

            float lineDistance = lineAngle / max(lineSoftness, 0.000001);
            float lineProfile = exp(-0.5 * lineDistance * lineDistance);
            float endFade = smoothstep(dotRadius * 1.2, dotRadius * 3.0, endAngle);
            lineMask = max(lineMask, lineProfile * endFade);
            dotMask = max(dotMask, 1.0 - smoothstep(dotRadius * 0.3, dotRadius, endAngle));
        }

        result = max(result, max(lineMask * lineStrength, dotMask * dotMask * dotStrength) * weight);
    }

    return result;
}

#endif
