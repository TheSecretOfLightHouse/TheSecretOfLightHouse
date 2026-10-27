Shader "Lighthouse/World/WaterSpray"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            struct SprayParticle { float4 positionAge; float4 velocityLife; float4 sizeSeed; };
            StructuredBuffer<SprayParticle> _SprayParticles;
            TEXTURE2D(_OceanFoamDetail); SAMPLER(sampler_OceanFoamDetail);
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float2 uv:TEXCOORD0;
                float4 data:TEXCOORD1;
                float fog:TEXCOORD2;
                float eye:TEXCOORD3;
            };
            Varyings Vert(uint vertex:SV_VertexID,uint instance:SV_InstanceID)
            {
                Varyings o;
                SprayParticle p=_SprayParticles[instance];
                const float2 corners[6]={float2(-1,-1),float2(1,-1),float2(1,1),float2(-1,-1),float2(1,1),float2(-1,1)};
                float2 corner=corners[vertex];
                float age=p.positionAge.w/max(0.001,p.velocityLife.w);
                float alive=step(0.001,p.velocityLife.w)*(1-step(1,age));
                float size=p.sizeSeed.x*(1+age*0.7)*alive;
                float3 position=p.positionAge.xyz+UNITY_MATRIX_I_V._m00_m10_m20*corner.x*size
                    +UNITY_MATRIX_I_V._m01_m11_m21*corner.y*size*lerp(1,1.7,p.sizeSeed.z);
                o.positionCS=TransformWorldToHClip(position);
                o.uv=corner*0.5+0.5;
                o.data=float4(age,p.sizeSeed.y,p.sizeSeed.z,alive);
                o.fog=ComputeFogFactor(o.positionCS.z);
                o.eye=-TransformWorldToView(position).z;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float radial=1-smoothstep(0.25,1,length(i.uv*2-1));
                float grain=sqrt(SAMPLE_TEXTURE2D(_OceanFoamDetail,sampler_OceanFoamDetail,i.uv*0.45+frac(i.data.y*.137)).r);
                float sceneEye=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)),_ZBufferParams);
                float contact=saturate((sceneEye-i.eye)/0.12);
                float alpha=radial*grain*smoothstep(0,0.08,i.data.x)*(1-smoothstep(0.45,1,i.data.x));
                alpha*=lerp(0.16,0.30,i.data.z)*i.data.w*contact;
                Light sun=GetMainLight();
                float3 color=max(0.025,sun.color*0.95+SampleSH(float3(0,1,0))*0.45);
                return half4(MixFog(color,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
