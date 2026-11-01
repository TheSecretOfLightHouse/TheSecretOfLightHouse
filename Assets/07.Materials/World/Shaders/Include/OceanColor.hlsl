#ifndef LIGHTHOUSE_OCEAN_COLOR_INCLUDED
#define LIGHTHOUSE_OCEAN_COLOR_INCLUDED

// Water body colour from optical depth. Shallow -> Deep over a few shallow-transition
// lengths, Deep -> VeryDeep with an exponential approach so open sea (no bottom) settles
// on VeryDeep without a hard band.
float3 OceanBodyColor(float3 shallow, float3 deep, float3 veryDeep,
    float depth, float shallowEnd, float deepEnd, float falloff)
{
    float nearEnd = shallowEnd * 4.0;
    float nearMix = 1.0 - exp(-depth / (shallowEnd * 1.6));
    nearMix = nearMix * nearMix * (3.0 - 2.0 * nearMix);
    float farT = saturate(max(0.0, depth - nearEnd) * falloff / max(deepEnd, 0.1) * 0.5);
    float farMix = farT * farT * (3.0 - 2.0 * farT);
    float3 body = lerp(shallow, deep, nearMix);
    // Thin water over sand scatters light back: a pale aqua lift that fades within the first metres.
    float lift = exp(-depth / (shallowEnd * 0.9));
    body = lerp(body, body * 0.55 + float3(0.45, 0.62, 0.58) * 0.9, lift * 0.28);
    return lerp(body, veryDeep, farMix);
}

// Smooth zone weights from the distance to the map centre: x inner, y middle, z outer.
// radii.xyz are the zone outer radii, radii.w is the half width of each blend band.
float3 OceanZoneWeights(float2 positionXZ, float4 radii)
{
    float r = length(positionXZ);
    float toMiddle = smoothstep(radii.x - radii.w, radii.x + radii.w, r);
    float toOuter = smoothstep(radii.y - radii.w, radii.y + radii.w, r);
    return float3(1.0 - toMiddle, toMiddle * (1.0 - toOuter), toOuter);
}

// Unlit glow owned by the ocean palette: when direct light is gone the water keeps a share of
// its own colour, and the shallow tone brightens toward grazing angles. The sky follows this
// palette, not the other way round.
float3 OceanAmbientGlow(float3 waterColor, float3 sheenColor, float fresnel, float glow)
{
    float3 body = waterColor * 0.42;
    float3 sheen = sheenColor * (0.2 + 0.8 * fresnel) * 0.85;
    return (body + sheen) * glow;
}

// Light scattered through thin crests toward the camera, tinted by the shallow colour.
float3 OceanCrestGlow(float3 shallow, float3 sunColor, float3 viewWS, float3 sunDir,
    float crestHeight, float tint)
{
    float backLit = pow(saturate(dot(viewWS, -sunDir) * 0.5 + 0.5), 3.0);
    float thin = saturate(crestHeight * 0.6 + 0.35);
    return shallow * sunColor * backLit * thin * tint;
}

float2 OceanHash22(float2 p)
{
    float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.xx + q.yz) * q.zy);
}

float OceanHash21(float2 p)
{
    float3 q = frac(float3(p.xyx) * 0.1031);
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

// Value noise with analytic derivative: x = value, yz = d/dp.
float3 OceanNoiseD(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float2 du = 6.0 * f * (1.0 - f);
    float a = OceanHash21(i);
    float b = OceanHash21(i + float2(1, 0));
    float c = OceanHash21(i + float2(0, 1));
    float d = OceanHash21(i + float2(1, 1));
    float k = a - b - c + d;
    float value = a + (b - a) * u.x + (c - a) * u.y + k * u.x * u.y;
    float2 grad = du * (float2(b - a, c - a) + k * u.yx);
    return float3(value, grad);
}

// Non-repeating ripple slope: rotated, drifting noise octaves instead of straight
// sine planes, so no stripe or lattice pattern shows on the surface.
float2 OceanRippleSlope(float2 p, float time)
{
    float2 slope = 0;
    float amp = 1.0;
    float freq = 0.32;
    float angle = 0.0;
    for (int i = 0; i < 3; i++)
    {
        float s = sin(angle);
        float c = cos(angle);
        float2 q = float2(c * p.x - s * p.y, s * p.x + c * p.y) * freq
            + float2(time * 0.07 * (i + 1), -time * 0.05 * (i + 2));
        float3 n = OceanNoiseD(q);
        float2 g = n.yz * freq * amp;
        slope += float2(c * g.x + s * g.y, -s * g.x + c * g.y);
        amp *= 0.4;
        freq *= 1.9;
        angle += 1.1;
    }
    return slope;
}

// Sun glitter as fine grain. Close to the camera each facet of a high-frequency noise
// normal gives a one-pixel glint, spread evenly (no low-frequency gating, so no clusters).
// With distance the grain is filtered into a smooth sheen lobe instead of being dropped,
// so the glitter reads as one continuous sparkling sheet toward the sun.
float OceanSparkle(float3 positionWS, float3 normalWS, float3 halfway, float time,
    float footprint)
{
    float2 p = positionWS.xz;
    // n1 is a slow patch noise: it gathers the fine glints into small clusters and also tilts the facets.
    float3 n1 = OceanNoiseD(p * 2.5 + float2(time * 0.25, -time * 0.18));
    float3 n2 = OceanNoiseD(p * 15.0 + float2(-time * 0.9, time * 0.5) + 3.7);
    float2 g = n1.yz * 0.03 + n2.yz * 0.045;
    float3 facet = normalize(normalWS + float3(g.x, 0.0, g.y));
    float cluster = lerp(0.3, 1.7, smoothstep(0.3, 0.7, n1.x));
    float fine = smoothstep(0.9985, 0.9999, dot(facet, halfway)) * cluster;
    float sheen = pow(saturate(dot(normalWS, halfway)), 140.0) * 0.3;
    float grain = smoothstep(0.04, 0.25, footprint);
    return lerp(fine * 1.2, sheen, grain);
}

// Fake open-sea depth variation: slow large patches, 0..1.
float OceanClarityPatch(float2 p, float time)
{
    float2 q = p * 0.011 + float2(time * 0.004, time * 0.0025);
    float n = OceanNoiseD(q).x * 0.65 + OceanNoiseD(q * 2.7 + 5.0).x * 0.35;
    return saturate((n - 0.5) * 1.8 + 0.5);
}

// Net-like light lines on the faked seabed; ridges of two warped noise layers.
float OceanCaustic(float2 p, float2 warp, float time)
{
    float2 a = p * 0.55 + warp * 1.0 + float2(time * 0.09, time * 0.06);
    float2 b = p * 0.8 - warp * 0.8 + float2(-time * 0.07, time * 0.1) + 9.0;
    float ra = 1.0 - abs(OceanNoiseD(a).x * 2.0 - 1.0);
    float rb = 1.0 - abs(OceanNoiseD(b).x * 2.0 - 1.0);
    return pow(saturate(ra * rb), 4.0);
}

#endif
