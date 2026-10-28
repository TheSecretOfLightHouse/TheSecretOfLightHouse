#ifndef LIGHTHOUSE_SKY_CELESTIAL_FRAME_INCLUDED
#define LIGHTHOUSE_SKY_CELESTIAL_FRAME_INCLUDED

float3 SkyWorldToCelestial(float3 worldDirection, float poleElevationDegrees, float siderealAngle)
{
    float sinLatitude;
    float cosLatitude;
    sincos(radians(poleElevationDegrees), sinLatitude, cosLatitude);

    float3 pole = float3(0.0, sinLatitude, cosLatitude);
    float3 eastAxis = float3(1.0, 0.0, 0.0);
    float3 southAxis = float3(0.0, -cosLatitude, sinLatitude);

    float sinAngle;
    float cosAngle;
    sincos(siderealAngle, sinAngle, cosAngle);

    float3 axisX = cosAngle * eastAxis - sinAngle * southAxis;
    float3 axisY = sinAngle * eastAxis + cosAngle * southAxis;

    return float3(dot(worldDirection, axisX), dot(worldDirection, axisY), dot(worldDirection, pole));
}

#endif
