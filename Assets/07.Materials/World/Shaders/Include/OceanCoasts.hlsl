#ifndef LIGHTHOUSE_OCEAN_COASTS_INCLUDED
#define LIGHTHOUSE_OCEAN_COASTS_INCLUDED
#define OCEAN_COAST_CAPACITY 32
#define OCEAN_COAST_SEGMENTS 96
float4 _OceanCoastBounds[OCEAN_COAST_CAPACITY];
float4 _OceanCoastEdges[OCEAN_COAST_CAPACITY * OCEAN_COAST_SEGMENTS];
float _OceanCoastEnabled;
float4 _OceanCoastProfiles[OCEAN_COAST_CAPACITY];
float4 _OceanCoastLagoon[OCEAN_COAST_CAPACITY];

float CoastWidth(float width, float4 profile, float2 p)
{
    if(dot(profile.zw,profile.zw)<0.1) return width;
    float2 delta=p-profile.xy;
    float facing=dot(delta,profile.zw)/max(0.001,length(delta));
    return width*lerp(0.14,1,smoothstep(0.05,0.8,facing));
}

float CoastPhase(float d,float time) { return max(0,d)*0.55+time*0.9; }

float CoastHeight(float d, float width, float time, float height, float energy)
{
    d = max(0.0, d);
    float fetch=max(7,width);
    float blend = smoothstep(fetch * 2.2, fetch * 5, d);
    float beach=smoothstep(1,5,width);
    float amplitude = min(min(0.55, energy * 0.55),lerp(0.28,0.035+d*0.06,beach))
        *smoothstep(lerp(-0.1,0.1,beach),lerp(0.3,2,beach),d);
    // Increasing time moves a constant phase towards smaller shore distance.
    float phase=CoastPhase(d,time);
    float shore = amplitude * (0.7*sin(phase)-0.4*cos(phase*2)-0.22*sin(phase*3));
    float result = lerp(shore, height, blend);
    float safeDepth = 0.025 + 0.012 * d + 0.015 * max(0.0, d - width);
    float release = smoothstep(width * 1.3, width * 2.2, d);
    return lerp(max(result, -safeDepth), result, release);
}

void OceanCoastFieldLagoonAt(float2 p, float time, float energy, inout float height, inout float3 normal, out float4 field, out float lagoonReach)
{
    field = 0;
    lagoonReach = 0;
    if (_OceanCoastEnabled < 0.5)
    {
        return;
    }
    float best = 1e20;
    float distance = 0.0;
    float width = 0.0;
    float2 gradient = 0.0;
    float nearestLagoon = 0.0;
    [loop] for (int island = 0; island < OCEAN_COAST_CAPACITY; island++)
    {
        float4 area = _OceanCoastBounds[island];
        float2 delta = p - area.xy;
        if (area.w <= 0.0 || dot(delta, delta) > area.z * area.z)
        {
            continue;
        }
        [loop] for (int i = 0; i < OCEAN_COAST_SEGMENTS; i++)
        {
            float4 edge = _OceanCoastEdges[island * OCEAN_COAST_SEGMENTS + i];
            float2 tangent = edge.zw - edge.xy;
            float t = saturate(dot(p - edge.xy, tangent) / max(0.0001, dot(tangent, tangent)));
            float2 offset = p - edge.xy - t * tangent;
            float square = dot(offset, offset);
            if (square < best)
            {
                best = square;
                float sign = dot(offset, float2(tangent.y, -tangent.x)) >= 0.0 ? 1.0 : -1.0;
                distance = sign * sqrt(square);
                gradient = square > 0.000001 ? sign * offset * rsqrt(square) : normalize(float2(tangent.y, -tangent.x));
                width = CoastWidth(area.w,_OceanCoastProfiles[island],p);
                nearestLagoon = _OceanCoastLagoon[island].x;
            }
        }
    }
    if (width <= 0.0)
    {
        return;
    }
    field = float4(gradient, distance, width);
    lagoonReach = nearestLagoon;
    float stepSize = 0.01;
    float alongDistance = (CoastHeight(distance + stepSize, width, time, height, energy)
        - CoastHeight(distance - stepSize, width, time, height, energy)) / (2.0 * stepSize);
    float alongHeight = (CoastHeight(distance, width, time, height + stepSize, energy)
        - CoastHeight(distance, width, time, height - stepSize, energy)) / (2.0 * stepSize);
    float2 slope = -normal.xz / max(0.001, normal.y);
    slope = slope * alongHeight + gradient * alongDistance;
    normal = normalize(float3(-slope.x, 1.0, -slope.y));
    height = CoastHeight(distance, width, time, height, energy);
}
void OceanCoastFieldAt(float2 p, float time, float energy, inout float height, inout float3 normal, out float4 field)
{
    float lagoonReach;
    OceanCoastFieldLagoonAt(p, time, energy, height, normal, field, lagoonReach);
}
void OceanCoastAt(float2 p, float time, float energy, inout float height, inout float3 normal)
{
    float4 unused;
    OceanCoastFieldAt(p, time, energy, height, normal, unused);
}
#endif
