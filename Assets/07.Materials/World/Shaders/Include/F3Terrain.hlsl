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
bool F3MapPoint(inout float2 samplePosition)
{
    if (_F3RegionExtent <= 0) return true;
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
    return max(0.25,groundDepth+0.05)*1.25;
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
float3 F3Displacement(float2 rest)
{
    float4 geometry=F3GeometryAt(rest);
    float4 field=F3FieldRaw(rest,geometry);
    float breakingLimit=F3BreakingLimit(geometry.x);
    float3 displacement=0;
    float swell=OpenSeaSwellAt(rest,_OceanTime);
    float depth=max(0.25,geometry.x+0.05);
    float shoal=(1-smoothstep(0.6,2.5,depth))*smoothstep(0.08,0.35,depth);
    for(int i=0;i<min((int)_OceanWaveCount,OCEAN_MAX_WAVES);i++)
    {
        float4 a=_OceanWaveA[i], b=_OceanWaveB[i];
        if(a.z<=0) continue;
        float k=OCEAN_TWO_PI/a.z;
        float phase=k*(dot(a.xy,rest+field.yz)-a.w*(_OceanTime-field.w));
        float transmission=min(F3WavelengthTransmission(field.x,a.z)*swell,breakingLimit);
        displacement.xz+=a.xy*(transmission*b.y/k*cos(phase));
        displacement.y+=transmission*b.x*(1+0.65*shoal)*(sin(phase)-0.25*shoal*cos(phase*2));
    }
    return displacement;
}
float F3Height(float2 world)
{
    float4 geometry=F3GeometryAt(world);
    if(geometry.w<=0 || geometry.y<=0) return 0;
    float2 rest=world;
    [unroll] for(int i=0;i<6;i++) rest=world-F3Displacement(rest).xz;
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
    float2 drive=_F3Flow.xy*_F3Flow.z;
    float2 uv=(world-_F3CurrentLayout.xy)/max(_F3CurrentLayout.z,0.001);
    if (_F3RegionExtent > 0)
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
