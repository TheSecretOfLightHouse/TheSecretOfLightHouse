Shader "Lighthouse/World/BreakerWave"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Include/OceanGerstner.hlsl"
            #include "Include/OceanInteraction.hlsl"
            #include "Include/BreakerWaveProfile.hlsl"
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                float3 normalWS:TEXCOORD1;
                float4 data:TEXCOORD2;
                float4 extra:TEXCOORD3;
                float fog:TEXCOORD4;
            };
            float BreakerFbm(float2 p)
            {
                return .5*FoamNoise(p)+.3*FoamNoise(p*2.07+7.1)+.2*FoamNoise(p*4.13+3.3);
            }
            float3 BreakerWorld(int part,BreakerState st,float2 impact,float2 f,float2 r,float width,float s,float c)
            {
                float3 local=BreakerPoint(part,st,impact,f,r,width,s,c);
                if(part==0 && st.phase==3)
                {
                    float rag=FoamNoise(local.xz*2.2+_OceanTime*1.3)-.5;
                    local.y+=rag*.45*sin(st.progress*3.14159265)*local.y;
                }
                float sea=_BreakerSea+OceanInteraction(local.xz).x;
                return float3(local.x,sea+local.y,local.z);
            }
            Varyings Vert(uint vertex:SV_VertexID,uint instance:SV_InstanceID)
            {
                Varyings o=(Varyings)0;
                o.positionCS=float4(0,0,0,1);
                uint ev=instance/3;
                int part=(int)(instance%3);
                if((float)ev>=_BreakerCount) return o;
                float4 a=_BreakerA[ev];
                float4 b=_BreakerB[ev];
                BreakerState st=BreakerMakeState(b.z,b.w,b.y,lerp(0.6,1,b.w));
                if(st.phase==0) return o;
                float alpha=part==0?st.ribbonAlpha:(part==1?st.floorAlpha:st.sheetAlpha);
                if(alpha<=0.001) return o;
                float2 impact=a.xy;
                float2 f=a.zw;
                float2 r=float2(f.y,-f.x);
                const float2 corners[6]={float2(0,0),float2(1,0),float2(1,1),float2(0,0),float2(1,1),float2(0,1)};
                float2 corner=corners[vertex%6];
                uint quad=vertex/6;
                float c=((quad%BREAKER_COLUMNS)+corner.x)/BREAKER_COLUMNS;
                float s=((quad/BREAKER_COLUMNS)+corner.y)/BREAKER_ROWS;
                float3 p=BreakerWorld(part,st,impact,f,r,b.x,s,c);
                float3 normal=float3(0,1,0);
                if(part==0)
                {
                    float3 ds=BreakerWorld(part,st,impact,f,r,b.x,min(1,s+0.03),c)-BreakerWorld(part,st,impact,f,r,b.x,max(0,s-0.03),c);
                    float3 dc=BreakerWorld(part,st,impact,f,r,b.x,s,min(1,c+0.03))-BreakerWorld(part,st,impact,f,r,b.x,s,max(0,c-0.03));
                    normal=normalize(cross(dc,ds)+float3(0,0.0001,0));
                    if(normal.y<0) normal=-normal;
                }
                else if(part==2)
                {
                    normal=normalize(float3(-f.x,0.2,-f.y));
                }
                o.positionWS=p;
                o.normalWS=normal;
                o.positionCS=TransformWorldToHClip(p);
                o.data=float4(c,s,alpha,part);
                o.extra=float4(st.shape.w,st.phase,st.progress,b.w);
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                clip(i.data.z-0.004);
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float3 normal=normalize(i.normalWS);
                Light sun=GetMainLight();
                float3 light=max(0.025,sun.color*0.9+SampleSH(float3(0,1,0))*.4);
                float3 tint=float3(0.025,0.35,0.30)*light;
                float3 body=lerp(SampleSceneColor(saturate(uv+normal.xz*.004)),tint,.62);
                float edge=smoothstep(0,.12,i.data.x)*smoothstep(0,.12,1-i.data.x);
                int part=(int)(i.data.w+0.5);
                float t=_OceanTime;
                float u=i.data.x;
                float v=i.data.y;
                float phase=i.extra.y;
                float prog=i.extra.z;
                float burst=phase<2.5?0:(phase<3.5?prog:1);
                float rundown=phase>3.5?prog:0;
                float3 color;
                float alpha;
                if(part==0)
                {
                    float lip=smoothstep(.4,.85,v);
                    float n=BreakerFbm(i.positionWS.xz*2.4+float2(t*.9,-t*.7)+v*2);
                    float foam=OceanFoamCoverage(i.positionWS.xz+i.positionWS.y*3,lip*i.extra.x,t);
                    color=lerp(body,light,saturate(foam+lip*.5+burst*(.55+.45*n)));
                    alpha=i.data.z*edge*lerp(.35,.9,smoothstep(.55,.9,v));
                    if(phase>2.5&&phase<3.5)
                    {
                        alpha*=smoothstep(prog,prog+.3,n*.85+.15);
                    }
                }
                else if(part==1)
                {
                    float2 w=float2(u*5,v*4);
                    float warp=BreakerFbm(w*1.3+float2(t*.4,-t*.3));
                    float2 p=w+(warp-.5)*1.6-float2(0,rundown*2.5);
                    float lace=BreakerFbm(p*1.7+3.1);
                    float lacemask=smoothstep(lerp(.38,.54,rundown),lerp(.38,.54,rundown)+.16,lace);
                    float worldFoam=OceanFoamCoverage(i.positionWS.xz+float2(0,rundown*2),.9*i.extra.x,t);
                    float foam=saturate(max(worldFoam*.8,lacemask));
                    float soft=smoothstep(0,.35,u)*smoothstep(0,.35,1-u)*smoothstep(0,.12,v)*(1-smoothstep(.35,1,v));
                    color=lerp(body,light,saturate(foam*1.4+.25*soft));
                    alpha=i.data.z*soft*lerp(.12,.9,foam)*lerp(1,1.25,rundown);
                }
                else
                {
                    float sp=lerp(.7,1.5,FoamNoise(float2(u*6,5.3)));
                    float flow=phase<3.5?prog*.4:.4+rundown*1.3;
                    float streak=FoamNoise(float2(u*16,v*1.8+flow*2.2*sp));
                    float finger=lerp(.5,1.0,FoamNoise(float2(u*9,2.7+t*.5)));
                    float topFade=1-smoothstep(finger-.15,finger,v);
                    float core=smoothstep(.3,0,v)*.5;
                    color=lerp(light*.92,light,streak);
                    alpha=i.data.z*edge*topFade*(.3+.7*smoothstep(.2,.65,streak)+core)*.95;
                }
                return half4(MixFog(color,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
