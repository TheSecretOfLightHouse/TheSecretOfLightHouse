#ifndef LIGHTHOUSE_SKY_NOISE_INCLUDED
#define LIGHTHOUSE_SKY_NOISE_INCLUDED

float3 SkyHash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

float SkyHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float SkyValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 local = frac(p);
    local = local * local * (3.0 - 2.0 * local);
    float a = SkyHash21(cell);
    float b = SkyHash21(cell + float2(1.0, 0.0));
    float c = SkyHash21(cell + float2(0.0, 1.0));
    float d = SkyHash21(cell + float2(1.0, 1.0));
    return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
}

float SkyFbm(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        value += amplitude * SkyValueNoise(p);
        p = p * 2.03 + 17.1;
        amplitude *= 0.5;
    }
    return value;
}

#endif
