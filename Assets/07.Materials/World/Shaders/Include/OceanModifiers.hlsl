#ifndef LIGHTHOUSE_OCEAN_MODIFIERS_INCLUDED
#define LIGHTHOUSE_OCEAN_MODIFIERS_INCLUDED

#define OCEAN_MAX_MODIFIERS 8
#define OCEAN_MODIFIER_TWO_PI 6.28318530718

float4 _OceanModifierA[OCEAN_MAX_MODIFIERS];
float4 _OceanModifierB[OCEAN_MAX_MODIFIERS];
float4 _OceanModifierC[OCEAN_MAX_MODIFIERS];
float _OceanModifierCount;

void OceanModifierAt(
    int index, float2 positionXZ, float time,
    out float height, out float2 slope)
{
    height = 0.0;
    slope = float2(0.0, 0.0);

    float4 a = _OceanModifierA[index];
    float4 b = _OceanModifierB[index];
    float speed = _OceanModifierC[index].x;
    int kind = (int)b.x;

    float2 offset = positionXZ - a.xy;
    float distanceSquared = dot(offset, offset);
    float radius = a.z;
    float strength = a.w;

    if (kind == 0 || radius <= 0.0)
    {
        return;
    }

    if (kind == 3)
    {
        float halfWidth = b.z * 0.5;
        float wavelength = b.w;
        float distance = sqrt(distanceSquared);

        if (halfWidth <= 0.0 || wavelength <= 0.0 || distance <= 0.0)
        {
            return;
        }

        float radialOffset = distance - radius;
        float u = radialOffset / halfWidth;

        if (abs(u) >= 1.0)
        {
            return;
        }

        float inside = 1.0 - u * u;
        float envelope = inside * inside;
        float envelopeSlope = -4.0 * u * inside / halfWidth;
        float waveNumber = OCEAN_MODIFIER_TWO_PI / wavelength;
        float phase = waveNumber * (radialOffset - speed * (time - b.y));
        float sine = sin(phase);
        float cosine = cos(phase);

        height = strength * envelope * sine;
        float radialSlope = strength
            * (envelopeSlope * sine + envelope * waveNumber * cosine);
        slope = radialSlope * offset / distance;
        return;
    }

    float sign = 0.0;
    if (kind == 1)
    {
        sign = -1.0;
    }
    else if (kind == 2)
    {
        sign = 1.0;
    }
    else
    {
        return;
    }

    float radiusSquared = radius * radius;
    if (distanceSquared >= radiusSquared)
    {
        return;
    }

    float inside = 1.0 - distanceSquared / radiusSquared;
    float signedStrength = sign * strength;

    height = signedStrength * inside * inside;
    slope = -4.0 * signedStrength * inside / radiusSquared * offset;
}

void OceanModifiersAt(
    float2 positionXZ, float time,
    out float height, out float2 slope)
{
    height = 0.0;
    slope = float2(0.0, 0.0);

    int count = min((int)_OceanModifierCount, OCEAN_MAX_MODIFIERS);
    for (int i = 0; i < count; i++)
    {
        float value;
        float2 gradient;
        OceanModifierAt(i, positionXZ, time, value, gradient);
        height += value;
        slope += gradient;
    }
}

#endif
