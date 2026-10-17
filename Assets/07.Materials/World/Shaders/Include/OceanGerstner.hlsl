#ifndef LIGHTHOUSE_OCEAN_GERSTNER_INCLUDED
#define LIGHTHOUSE_OCEAN_GERSTNER_INCLUDED

#define OCEAN_MAX_WAVES 8
#define OCEAN_TWO_PI 6.28318530718
#include "OceanModifiers.hlsl"

float4 _OceanWaveA[OCEAN_MAX_WAVES];
float4 _OceanWaveB[OCEAN_MAX_WAVES];
float _OceanWaveCount;
float _OceanTime;

void OceanGerstner_float(float3 restPositionWS, out float3 positionWS, out float3 normalWS)
{
    float3 displacement = float3(0.0, 0.0, 0.0);
    float slopeX = 0.0;
    float slopeZ = 0.0;
    float stretchXX = 0.0;
    float stretchZZ = 0.0;
    float stretchXZ = 0.0;

    int count = min((int)_OceanWaveCount, OCEAN_MAX_WAVES);

    for (int i = 0; i < count; i++)
    {
        float2 direction = _OceanWaveA[i].xy;
        float wavelength = _OceanWaveA[i].z;
        float speed = _OceanWaveA[i].w;
        float amplitude = _OceanWaveB[i].x;
        float steepness = _OceanWaveB[i].y;

        if (wavelength <= 0.0)
        {
            continue;
        }

        float waveNumber = OCEAN_TWO_PI / wavelength;
        float phase = waveNumber * (dot(direction, restPositionWS.xz) - speed * _OceanTime);
        float sine = sin(phase);
        float cosine = cos(phase);

        float horizontal = steepness / waveNumber * cosine;
        displacement.xz += direction * horizontal;
        displacement.y += amplitude * sine;

        float slope = waveNumber * amplitude * cosine;
        float stretch = steepness * sine;
        slopeX += direction.x * slope;
        slopeZ += direction.y * slope;
        stretchXX += direction.x * direction.x * stretch;
        stretchZZ += direction.y * direction.y * stretch;
        stretchXZ += direction.x * direction.y * stretch;
    }

    positionWS = restPositionWS + displacement;

    float modifierHeight;
    float2 modifierSlope;
    OceanModifiersAt(positionWS.xz, _OceanTime, modifierHeight, modifierSlope);
    positionWS.y += modifierHeight;

    float normalY = (1.0 - stretchXX) * (1.0 - stretchZZ) - stretchXZ * stretchXZ;
    float normalX = -(slopeX * (1.0 - stretchZZ) + stretchXZ * slopeZ);
    float normalZ = -(slopeZ * (1.0 - stretchXX) + stretchXZ * slopeX);

    normalX -= modifierSlope.x * normalY;
    normalZ -= modifierSlope.y * normalY;

    normalWS = normalize(float3(normalX, normalY, normalZ));
}

#endif
