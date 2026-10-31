Shader "Lighthouse/World/F3Surf"
{
    Properties
    {
        _ParticleMode ("Particle Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-5" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Include/OceanGerstner.hlsl"
            #include "Include/F3Terrain.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _ParticleMode;
            CBUFFER_END
            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 color : COLOR;
                float2 uv : TEXCOORD2;
                float fog : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world=TransformObjectToWorld(input.positionOS);
                output.positionCS=TransformWorldToHClip(output.world);
                output.normal=TransformObjectToWorldNormal(input.normalOS);
                output.color=input.color;
                output.uv=input.uv;
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                clip(_F3Enabled-0.5);
                float4 geometry=F3GeometryAt(input.world.xz);
                clip(geometry.w-0.002);
                if(F3GroundAt(input.world.xz)>0.5) clip(input.world.y+geometry.x-0.002);
                clip(geometry.y-0.5);
                clip(input.world.y-F3Height(input.world.xz)+0.04);
                float alpha=input.color.a;
                if(_ParticleMode>0.5 && _ParticleMode<1.5)
                {
                    float2 uv=input.uv*2-1;
                    alpha*=1-smoothstep(0.15,1,dot(uv,uv));
                }
                Light sun=GetMainLight();
                float diffuse=0.65+0.35*abs(dot(normalize(input.normal),sun.direction));
                float3 color=input.color.rgb*(sun.color*diffuse+SampleSH(float3(0,1,0))*0.25);
                return half4(MixFog(color,input.fog),alpha);
            }
            ENDHLSL
        }
    }
}
