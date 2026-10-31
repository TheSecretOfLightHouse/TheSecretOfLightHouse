#ifndef LIGHTHOUSE_OPEN_SEA_SWELL_INCLUDED
#define LIGHTHOUSE_OPEN_SEA_SWELL_INCLUDED
#define OPEN_SEA_SWELL_TERMS 5
float4 _OpenSeaSwell[OPEN_SEA_SWELL_TERMS];
float4 _OpenSeaFlow;
float OpenSeaSwellAt(float2 world,float time)
{
    float depth=_OpenSeaFlow.z;
    if(depth<=0) return 1;
    float2 p=world-_OpenSeaFlow.xy*time;
    float sum=0;
    [unroll] for(int i=0;i<OPEN_SEA_SWELL_TERMS;i++)
    {
        float4 term=_OpenSeaSwell[i];
        sum+=term.w*sin(term.x*p.x+term.y*p.y+term.z);
    }
    float n=sum*_OpenSeaFlow.w;
    return 1+depth*n*rsqrt(1+n*n);
}
#endif
