Shader "Lighthouse/World/SurfCrest"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" "RenderType"="Transparent" }
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
            float3 _SurfFocus;
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                float3 normalWS:TEXCOORD1;
                float2 data:TEXCOORD2;
                float fog:TEXCOORD3;
            };
            Varyings Vert(uint vertex:SV_VertexID,uint instance:SV_InstanceID)
            {
                Varyings o=(Varyings)0;
                uint island=instance/3;
                uint band=instance%3;
                uint quad=vertex/6;
                uint segment=quad/20;
                uint strip=quad%20;
                const float2 corners[6]={float2(0,0),float2(1,0),float2(1,1),float2(0,0),float2(1,1),float2(0,1)};
                float2 corner=corners[vertex%6];
                float4 edge=_OceanCoastEdges[island*OCEAN_COAST_SEGMENTS+segment];
                float4 area=_OceanCoastBounds[island];
                float2 shore=lerp(edge.xy,edge.zw,corner.x);
                if(area.w<=0 || distance(shore,_SurfFocus.xz)>60)
                {
                    o.positionCS=float4(0,0,0,1);
                    return o;
                }
                float4 previous=_OceanCoastEdges[island*OCEAN_COAST_SEGMENTS+(segment+95)%96];
                float4 next=_OceanCoastEdges[island*OCEAN_COAST_SEGMENTS+(segment+1)%96];
                float2 tangent=normalize(edge.zw-edge.xy);
                float2 before=normalize(previous.zw-previous.xy);
                float2 after=normalize(next.zw-next.xy);
                float2 startNormal=normalize(float2(tangent.y+before.y,-tangent.x-before.x));
                float2 endNormal=normalize(float2(tangent.y+after.y,-tangent.x-after.x));
                float2 outward=normalize(lerp(startNormal,endNormal,corner.x));
                float width=CoastWidth(area.w,_OceanCoastProfiles[island],shore);
                float beach=smoothstep(1,5,width);
                float crest=(1.57079632679+OCEAN_TWO_PI*(floor(_OceanTime*0.9/OCEAN_TWO_PI)+band)-_OceanTime*0.9)/0.55;
                crest+=sin(shore.x*.3+shore.y*.2)*.4;
                if(crest<=0 || crest>=15)
                {
                    o.positionCS=float4(0,0,0,1);
                    return o;
                }
                float envelope=smoothstep(lerp(0.5,2.2,beach),lerp(2,4.5,beach),crest)
                    *(1-smoothstep(10,15,crest))*step(0.01,area.w);
                envelope*=1-smoothstep(42,60,distance(shore,_SurfFocus.xz));
                float energy=0;
                for(int i=0;i<min((int)_OceanWaveCount,OCEAN_MAX_WAVES);i++) energy+=_OceanWaveB[i].x;
                float amplitude=min(0.55,energy*0.55)*envelope*(0.65+0.35*FoamNoise(shore*.4+_OceanTime*.07));
                float s=(strip+corner.y)/20;
                float2 p;
                float y;
                if(_BreakerTimes.w>0)
                {
                    float age;
                    if(crest>=6) age=_BreakerTimes.x*saturate(1-(crest-6)/9);
                    else if(crest>=3) age=lerp(_BreakerTimes.y,_BreakerTimes.x,(crest-3)/3);
                    else age=lerp(_BreakerTimes.z,_BreakerTimes.y,saturate((crest-.5)/2.5));
                    float steepness=min(.7,.3+(1-beach)*.5*saturate(energy*2));
                    BreakerState st=BreakerMakeState(age,saturate(energy*.8),0,steepness);
                    st.crestBack=crest;
                    st.height=amplitude*1.7;
                    float back;
                    float h;
                    BreakerSection(st,s,back,h);
                    float back0;
                    float h0;
                    BreakerSection(st,max(0,s-.05),back0,h0);
                    float back1;
                    float h1;
                    BreakerSection(st,min(1,s+.05),back1,h1);
                    float2 tangent2=float2(back1-back0,h1-h0);
                    float2 n2=normalize(float2(-tangent2.y,tangent2.x)+float2(0,.0001));
                    p=shore+outward*back;
                    y=CoastHeight(crest,width,_OceanTime,0,energy)+h;
                    o.normalWS=normalize(float3(outward.x*n2.x,abs(n2.y)+.001,outward.y*n2.x));
                }
                else
                {
                    float theta=-1.57079632679+s*4.1;
                    float d=crest+cos(theta)*amplitude*0.8;
                    y=CoastHeight(crest,width,_OceanTime,0,energy)+(sin(theta)+1)*amplitude*0.45;
                    p=shore+outward*d;
                    float2 crossSection=float2(-sin(theta)*0.8,cos(theta)*0.45);
                    o.normalWS=normalize(float3(outward.x*crossSection.y,-crossSection.x,outward.y*crossSection.y)+float3(0,.001,0));
                }
                o.positionWS=float3(p.x,y+OceanInteraction(p).x,p.y);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.data=float2(s,envelope*saturate(energy*2));
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                clip(i.data.y-0.005);
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float3 normal=normalize(i.normalWS);
                Light sun=GetMainLight();
                float3 light=max(0.025,sun.color*0.9+SampleSH(float3(0,1,0))*.4);
                float3 tint=float3(0.025,0.35,0.30)*light;
                float3 color=lerp(SampleSceneColor(saturate(uv+normal.xz*.002)),tint,.62);
                float density=smoothstep(.3,.8,i.data.x)*.95;
                float foam=OceanFoamCoverage(i.positionWS.xz,density,_OceanTime);
                color=lerp(color,light,foam*density);
                float alpha=i.data.y*smoothstep(0,.15,i.data.x)*(1-smoothstep(.8,1,i.data.x))*lerp(.25,.85,i.data.x);
                return half4(MixFog(color,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
