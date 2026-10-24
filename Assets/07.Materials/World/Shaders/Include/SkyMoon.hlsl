#ifndef LIGHTHOUSE_SKY_MOON_INCLUDED
#define LIGHTHOUSE_SKY_MOON_INCLUDED

#include "SkyNoise.hlsl"

float4 SkyMoon(float3 direction, float3 moonDirection, float phase, float angularRadius, float3 color, float glowStrength)
{
    float moonLength = length(moonDirection);
    if (moonLength < 0.001)
    {
        return float4(0.0, 0.0, 0.0, 0.0);
    }

    float3 moon = moonDirection / moonLength;
    float cosAngle = dot(direction, moon);
    if (cosAngle <= 0.0)
    {
        return float4(0.0, 0.0, 0.0, 0.0);
    }

    float3 right = abs(moon.y) > 0.999 ? float3(1.0, 0.0, 0.0) : normalize(cross(float3(0.0, 1.0, 0.0), moon));
    float3 up = cross(moon, right);
    float2 uv = float2(dot(direction, right), dot(direction, up)) / angularRadius;
    float radiusSq = dot(uv, uv);

    float disc = 1.0 - smoothstep(0.92, 1.0, sqrt(radiusSq));
    float3 normal = float3(uv, sqrt(saturate(1.0 - radiusSq)));

    float angle = phase * 6.2831853;
    float3 lightDirection = float3(-sin(angle), 0.0, -cos(angle));
    float lit = smoothstep(-0.02, 0.12, dot(normal, lightDirection));

    float mare = smoothstep(0.45, 0.65, SkyFbm(uv * 2.2 + 3.1));
    float surface = lerp(1.0, 0.55, mare) * (0.9 + 0.1 * SkyFbm(uv * 9.0));
    float3 body = color * surface * (lit + 0.04) * disc;

    float illumination = 0.5 * (1.0 - cos(angle));
    float glow = exp(-(1.0 - cosAngle) / (6.0 * angularRadius * angularRadius)) * glowStrength * illumination;

    return float4(body + color * glow, disc);
}

#endif
