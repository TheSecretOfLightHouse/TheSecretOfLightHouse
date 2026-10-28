#ifndef LIGHTHOUSE_OCEAN_SEABED_INCLUDED
#define LIGHTHOUSE_OCEAN_SEABED_INCLUDED

#include "OceanColor.hlsl"

// Light net on terrain below the water surface. Same ridge pattern as the surface-side caustic,
// projected along the sun so deeper ground shifts against shallower ground.
// Inputs are world position and time only, so every client computes the same value.
float OceanSeabedCaustic(float3 positionWS, float3 normalWS, float3 sunDir, float time,
    float waterLevel, float fadePerMetre)
{
    float depth = max(0.0, waterLevel - positionWS.y);
    float under = smoothstep(0.0, 0.12, waterLevel - positionWS.y);
    float2 p = positionWS.xz + sunDir.xz / max(sunDir.y, 0.25) * depth;
    float pattern = OceanCaustic(p, float2(0.0, 0.0), time);
    float facing = saturate(normalWS.y);
    facing *= facing;
    float sunUp = saturate(sunDir.y * 4.0);
    return pattern * under * facing * sunUp * exp(-depth * fadePerMetre);
}

// Caustic light shifts from the tint toward white-ish near the surface and keeps the tint deeper down.
float3 OceanSeabedCausticTint(float3 tint, float depth)
{
    return lerp(float3(1.0, 1.0, 1.0), tint, saturate(depth * 0.4));
}

#endif
