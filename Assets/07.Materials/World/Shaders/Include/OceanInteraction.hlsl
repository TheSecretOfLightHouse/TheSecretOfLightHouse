#ifndef LIGHTHOUSE_OCEAN_INTERACTION_INCLUDED
#define LIGHTHOUSE_OCEAN_INTERACTION_INCLUDED
TEXTURE2D(_OceanInteractionState);
SAMPLER(sampler_OceanInteractionState);
TEXTURE2D(_OceanFoamDetail);
SAMPLER(sampler_OceanFoamDetail);
float4 _OceanInteractionArea;
float _OceanInteractionEnabled;

float OceanInteractionWeight(float2 p)
{
    if(_OceanInteractionEnabled<0.5) return 0;
    float2 uv=(p-_OceanInteractionArea.xy)/max(0.01,_OceanInteractionArea.z);
    return smoothstep(0.015,0.1,min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
}

float4 OceanInteraction(float2 p)
{
    if(_OceanInteractionEnabled<0.5) return 0;
    float2 uv = (p-_OceanInteractionArea.xy)/max(0.01,_OceanInteractionArea.z);
    float edge = min(min(uv.x,uv.y),min(1-uv.x,1-uv.y));
    float fade = smoothstep(0.015,0.1,edge);
    return SAMPLE_TEXTURE2D_LOD(_OceanInteractionState,sampler_OceanInteractionState,saturate(uv),0)*fade;
}

void OceanInteractionDisplace(inout float3 position,inout float3 normal)
{
    float stepSize=max(0.01,_OceanInteractionArea.w);
    float height = OceanInteraction(position.xz).x;
    float dx = OceanInteraction(position.xz+float2(stepSize,0)).x-OceanInteraction(position.xz-float2(stepSize,0)).x;
    float dz = OceanInteraction(position.xz+float2(0,stepSize)).x-OceanInteraction(position.xz-float2(0,stepSize)).x;
    position.y += height;
    normal = normalize(normal+float3(-dx,0,-dz)/(2*stepSize));
}

float FoamHash(float2 p)
{
    return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);
}
float FoamNoise(float2 p)
{
    float2 i=floor(p),f=frac(p);
    f=f*f*(3-2*f);
    return lerp(lerp(FoamHash(i),FoamHash(i+float2(1,0)),f.x),
        lerp(FoamHash(i+float2(0,1)),FoamHash(i+1),f.x),f.y);
}
float OceanFoamCoverage(float2 world,float density,float time)
{
    // All foam shares one world-space pattern; density changes strand thickness, not a second overlay.
    float2 uv=world*0.085;
    uv+=float2(FoamNoise(world*0.23+time*0.015),FoamNoise(world*0.23+17.3-time*0.012))*0.04;
    float detail=sqrt(SAMPLE_TEXTURE2D(_OceanFoamDetail,sampler_OceanFoamDetail,uv).r);
    float micro=sqrt(SAMPLE_TEXTURE2D(_OceanFoamDetail,sampler_OceanFoamDetail,world*0.47+17.3).r);
    float threshold=lerp(0.68,0.25,saturate(density));
    float aa=max(0.025,fwidth(detail));
    float lace=smoothstep(threshold-aa,threshold+aa+0.08,detail);
    return lace*lerp(0.7,1,micro)*saturate(density*2);

}
#endif
