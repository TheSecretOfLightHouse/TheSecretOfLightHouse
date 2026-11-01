#ifndef LIGHTHOUSE_F3_TERRAIN_INCLUDED
#define LIGHTHOUSE_F3_TERRAIN_INCLUDED
#include "OpenSeaSwell.hlsl"
TEXTURE2D(_F3Transmission); SAMPLER(sampler_F3Transmission);
TEXTURE2D(_F3Geometry); SAMPLER(sampler_F3Geometry);
TEXTURE2D(_F3GroundCoverage); SAMPLER(sampler_F3GroundCoverage);
float4 _F3Layout;
float4 _F3Regions[32];
int _F3RegionCount;
float _F3RegionExtent;
float4 _F3WorldBounds;
TEXTURE2D_ARRAY(_F3Directions); SAMPLER(sampler_F3Directions);
float4 _F3DirectionInfo[32];
float _F3DirectionsEnabled;
bool F3MapPoint(inout float2 samplePosition)
{
    if (_F3RegionExtent <= 0) return true;
    if (any(samplePosition < _F3WorldBounds.xy) || any(samplePosition >= _F3WorldBounds.zw)) return false;
    int selected = -1;
    float nearest = 1e20;
    for (int i = 0; i < _F3RegionCount; i++)
    {
        float2 local = samplePosition - _F3Regions[i].xy;
        float distance = dot(local - _F3RegionExtent * 0.5, local - _F3RegionExtent * 0.5);
        if (all(local >= 0) && all(local < _F3RegionExtent) && distance < nearest)
        {
            nearest = distance;
            selected = i;
        }
    }
    if (selected < 0) return false;
    samplePosition += _F3Regions[selected].zw - _F3Regions[selected].xy;
    return true;
}

float4 _F3Flow;
float4 _F3CurrentLayout;
float _F3CurrentWorld;
TEXTURE2D(_F3CurrentBasis);
float _F3Enabled;
float _F3GeometrySize;

float4 F3GeometryTexel(int2 cell, int size)
{
    // Texel-centre samplePosition queries keep uniform wake sources and varying surface queries consistent.
    return SAMPLE_TEXTURE2D_LOD(_F3Geometry, sampler_PointClamp, (cell + 0.5) / size, 0);
}
float4 F3TransmissionTexel(int2 cell, int size)
{
    return SAMPLE_TEXTURE2D_LOD(_F3Transmission, sampler_PointClamp, (cell + 0.5) / size, 0);
}

float4 F3BilinearGeometry(float2 uv, int size)
{
    float2 coordinate = uv * size - 0.5;
    int2 cell = clamp((int2)floor(coordinate), 0, size - 2);
    float2 fraction = coordinate - cell;
    return lerp(lerp(F3GeometryTexel(cell,size), F3GeometryTexel(cell+int2(1,0),size), fraction.x),
        lerp(F3GeometryTexel(cell+int2(0,1),size), F3GeometryTexel(cell+1,size), fraction.x), fraction.y);
}
float4 F3BilinearTransmission(float2 uv, int size)
{
    float2 coordinate = uv * size - 0.5;
    int2 cell = clamp((int2)floor(coordinate), 0, size - 2);
    float2 fraction = coordinate - cell;
    return lerp(lerp(F3TransmissionTexel(cell,size), F3TransmissionTexel(cell+int2(1,0),size), fraction.x),
        lerp(F3TransmissionTexel(cell+int2(0,1),size), F3TransmissionTexel(cell+1,size), fraction.x), fraction.y);
}

bool F3Inside(float2 world)
{
    if (!F3MapPoint(world)) return false;
    float2 q=(world-_F3Layout.xy)/max(_F3Layout.z,0.001)-0.5;
    float size=_F3Layout.w/max(_F3Layout.z,0.001);
    return all(q>=0) && all(q<=size-1);
}
float F3GroundAt(float2 world)
{
    if(_F3Enabled<0.5 || !F3Inside(world)) return 0;
    F3MapPoint(world);
    return SAMPLE_TEXTURE2D_LOD(_F3GroundCoverage,sampler_F3GroundCoverage,(world-_F3Layout.xy)/_F3Layout.w,0).r;
}
float4 F3GeometryAt(float2 world);
#define F3_SHORT_WAVELENGTH 8.0
#define F3_LONG_WAVELENGTH 60.0
#define F3_SHORT_EXPONENT 1.5
#define F3_LONG_EXPONENT 0.5
float F3BreakingLimit(float groundDepth)
{
    float depth=max(0.25,groundDepth+0.05);
    return depth*lerp(0.5,1.25,smoothstep(2,8,depth));
}
float F3WavelengthTransmission(float baked,float wavelength)
{
    float t=smoothstep(0,1,saturate((wavelength-F3_SHORT_WAVELENGTH)/(F3_LONG_WAVELENGTH-F3_SHORT_WAVELENGTH)));
    return pow(max(baked,0),lerp(F3_SHORT_EXPONENT,F3_LONG_EXPONENT,t));
}
float4 F3FieldRaw(float2 world,float4 geometry)
{
    if(_F3Enabled<0.5 || !F3Inside(world)) return float4(1,0,0,0);
    if(geometry.w<=0 || geometry.y<=0) return 0;
    F3MapPoint(world);
    return F3BilinearTransmission((world-_F3Layout.xy)/_F3Layout.w,(int)round(_F3Layout.w/_F3Layout.z));
}
float4 F3Field(float2 world)
{
    if(_F3Enabled<0.5 || !F3Inside(world)) return float4(1,0,0,0);
    float4 geometry=F3GeometryAt(world);
    float4 field=F3FieldRaw(world,geometry);
    if(geometry.w<=0 || geometry.y<=0) return 0;
    field.x=min(field.x,F3BreakingLimit(geometry.x));
    return field;
}
float4 F3GeometryAt(float2 world)
{
    if(_F3Enabled<0.5 || !F3Inside(world)) return float4(4,1,1,4);
    F3MapPoint(world);
    float2 uv=(world-_F3Layout.xy)/_F3Layout.w;
    int2 cell=clamp((int2)floor(uv*_F3GeometrySize),0,(int)_F3GeometrySize-1);
    float4 data=F3GeometryTexel(cell,(int)_F3GeometrySize);
    float4 filtered=F3BilinearGeometry(uv,(int)_F3GeometrySize);
    data.x=filtered.x;
    data.w=filtered.w;
    return data;
}
float4 F3DirectionAt(float2 world, float2 direction, float4 fallback)
{
    if (_F3DirectionsEnabled < 0.5 || _F3RegionExtent <= 0) return fallback;
    if (any(world < _F3WorldBounds.xy) || any(world >= _F3WorldBounds.zw)) return float4(1,0,0,0);
    float4 result = float4(1,0,0,0);
    float angle = atan2(direction.y, direction.x) * (180.0 / 3.14159265359);
    for (int i = 0; i < _F3RegionCount; i++)
    {
        float2 local = world - _F3Regions[i].xy;
        float edge = min(min(local.x,local.y),min(_F3RegionExtent-local.x,_F3RegionExtent-local.y));
        if (edge <= 0) continue;
        float4 info = _F3DirectionInfo[i];
        float relative = frac((angle - info.z) / 360.0) * info.y;
        float first = floor(relative);
        float second = fmod(first + 1, info.y);
        float2 uv = clamp(local / _F3RegionExtent, 0.5 / info.w, 1 - 0.5 / info.w);
        float4 field = lerp(SAMPLE_TEXTURE2D_ARRAY_LOD(_F3Directions, sampler_F3Directions, uv, info.x + first, 0),
            SAMPLE_TEXTURE2D_ARRAY_LOD(_F3Directions, sampler_F3Directions, uv, info.x + second, 0), frac(relative));
        float weight = smoothstep(0,1,saturate(edge/(2*_F3Layout.z)));
        result.x *= lerp(1,field.x,weight);
        result.yzw += field.yzw*weight;
    }
    return result;
}
float F3PhaseOffset(float wavelength) { return wavelength*2.39996323; }
void F3OpenSeaSurface(float2 rest,out float3 displacement,out float3 normal)
{
    displacement=0;
    float3 derivativeX=0, derivativeZ=0;
    float envelope=0;
    float2 envelopeSlope=0;
    for(int i=0;i<min((int)_OceanWaveCount,OCEAN_MAX_WAVES);i++)
    {
        float4 a=_OceanWaveA[i], b=_OceanWaveB[i];
        if(a.z<=0) continue;
        float swell;
        float2 swellSlope;
        OpenSeaSwellAndGradient(rest+float2(a.y,-a.x)*a.z,_OceanTime,swell,swellSlope);
        float k=OCEAN_TWO_PI/a.z;
        float phase=k*(dot(a.xy,rest)-a.w*_OceanTime)+F3PhaseOffset(a.z);
        float sine=sin(phase), cosine=cos(phase);
        float horizontal=swell*b.y/k*cosine;
        float2 horizontalSlope=b.y/k*(swellSlope*cosine-swell*k*a.xy*sine);
        float2 heightSlope=b.x*(swellSlope*sine+swell*k*a.xy*cosine);
        displacement+=float3(a.x*horizontal,swell*b.x*sine,a.y*horizontal);
        derivativeX+=float3(a.x*horizontalSlope.x,heightSlope.x,a.y*horizontalSlope.x);
        derivativeZ+=float3(a.x*horizontalSlope.y,heightSlope.y,a.y*horizontalSlope.y);
        envelope+=swell*b.x;
        envelopeSlope+=swellSlope*b.x;
    }
    float limit=F3BreakingLimit(4);
    if(envelope>limit)
    {
        float scale=limit/envelope;
        float2 scaleSlope=-scale/envelope*envelopeSlope;
        derivativeX=derivativeX*scale+displacement*scaleSlope.x;
        derivativeZ=derivativeZ*scale+displacement*scaleSlope.y;
        displacement*=scale;
    }
    normal=normalize(cross(float3(0,0,1)+derivativeZ,float3(1,0,0)+derivativeX));
}
float3 F3Displacement(float2 rest)
{
    float4 geometry=F3GeometryAt(rest);
    float4 fallback=0;
    [branch] if(_F3DirectionsEnabled<0.5 || _F3RegionExtent<=0) fallback=F3FieldRaw(rest,geometry);
    float breakingLimit=F3BreakingLimit(geometry.x);
    float3 displacement=0;
    float envelope=0;
    float depth=max(0.25,geometry.x+0.05);
    float shoal=(1-smoothstep(0.6,2.5,depth))*smoothstep(0.08,0.35,depth);
    for(int i=0;i<min((int)_OceanWaveCount,OCEAN_MAX_WAVES);i++)
    {
        float4 a=_OceanWaveA[i], b=_OceanWaveB[i];
        if(a.z<=0) continue;
        float4 field=F3DirectionAt(rest,a.xy,fallback);
        if(geometry.w<=0 || geometry.y<=0) field=0;
        float swell=OpenSeaSwellAt(rest+float2(a.y,-a.x)*a.z,_OceanTime);
        float k=OCEAN_TWO_PI/a.z;
        float phase=k*(dot(a.xy,rest+field.yz)-a.w*(_OceanTime-field.w))+F3PhaseOffset(a.z);
        float transmission=min(F3WavelengthTransmission(field.x,a.z)*swell,breakingLimit);
        displacement.xz+=a.xy*(transmission*b.y/k*cos(phase));
        displacement.y+=transmission*b.x*(1+0.2*shoal)*(sin(phase)-0.25*shoal*cos(phase*2));
        envelope+=transmission*b.x*(1+0.2*shoal)*(1+0.25*shoal);
    }
    return displacement*min(1,breakingLimit/max(envelope,0.0001));
}
float3 F3RestNormal(float2 rest)
{
    const float stepSize=0.1;
    float3 tangentX=float3(2*stepSize,0,0)+F3Displacement(rest+float2(stepSize,0))-F3Displacement(rest-float2(stepSize,0));
    float3 tangentZ=float3(0,0,2*stepSize)+F3Displacement(rest+float2(0,stepSize))-F3Displacement(rest-float2(0,stepSize));
    return normalize(cross(tangentZ,tangentX));
}
float F3Height(float2 world)
{
    float4 geometry=F3GeometryAt(world);
    if(geometry.w<=0 || geometry.y<=0) return 0;
    float2 rest=world;
    [loop] for(int i=0;i<12;i++) rest=world-F3Displacement(rest).xz;
    float height=F3Displacement(rest).y;
    float modifierHeight; float2 modifierSlope;
    OceanModifiersAt(world,_OceanTime,modifierHeight,modifierSlope);
    return height+modifierHeight;
}
float3 F3Normal(float2 world)
{
    const float stepSize=0.1;
    float dx=F3Height(world+float2(stepSize,0))-F3Height(world-float2(stepSize,0));
    float dz=F3Height(world+float2(0,stepSize))-F3Height(world-float2(0,stepSize));
    return normalize(float3(-dx,2*stepSize,-dz));
}
float2 F3Current(float2 world)
{
    if(_F3Enabled<0.5) return float2(-0.08,0.06);
    float4 geometry=F3GeometryAt(world);
    if(geometry.x<=0 || geometry.y<=0 || geometry.w<=0) return 0;
    float2 drive=_F3Flow.xy*_F3Flow.z;
    float2 uv=(world-_F3CurrentLayout.xy)/max(_F3CurrentLayout.z,0.001);
    if (_F3RegionExtent > 0 && _F3CurrentWorld < 0.5)
    {
        float2 mapped = world;
        uv = F3MapPoint(mapped) ? (mapped - _F3Layout.xy) / _F3Layout.w : float2(-1,-1);
    }
    float2 flow=drive;
    if(all(uv>=0) && all(uv<=1))
    {
        float4 basis=SAMPLE_TEXTURE2D_LOD(_F3CurrentBasis,sampler_LinearClamp,uv,0);
        flow=float2(drive.x*basis.x+drive.y*basis.z,drive.x*basis.y+drive.y*basis.w);
    }
    float speed=length(flow);
    return speed>_F3Flow.w && speed>1e-5 ? flow*(_F3Flow.w/speed) : flow;
}
float2 F3Advect(float2 source, float duration)
{
    float2 samplePosition=source;
    float stepTime=duration*0.25;
    [unroll] for(int i=0;i<4;i++)
    {
        float2 midpoint=samplePosition+F3Current(samplePosition)*(stepTime*0.5);
        float2 next=samplePosition+F3Current(midpoint)*stepTime;
        float4 geometry=F3GeometryAt(next);
        if(_F3Enabled>0.5 && (geometry.y<=0 || geometry.w<=0 || geometry.x<=0)) break;
        samplePosition=next;
    }
    return samplePosition;
}
float F3WakeGate(float2 source,float2 target)
{
    if(_F3Enabled<0.5) return 1;
    float4 a=F3GeometryAt(source), b=F3GeometryAt(target);
    if(a.x<=0 || b.x<=0 || abs(a.z-b.z)>0.1) return 0;
    int steps=min(128,(int)ceil(length(target-source)/max(_F3Layout.w/_F3GeometrySize*0.5,0.001)));
    [loop] for(int i=1;i<steps;i++)
    {
        if(F3GeometryAt(lerp(source,target,(float)i/steps)).x<=0) return 0;
    }
    return saturate(b.x/0.35);
}
#endif
