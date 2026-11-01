Shader "Lighthouse/Boss/WaterCurtain"
{
    Properties
    {
        _FlowTime ("Time", Float) = 0
        _Strength ("Strength", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+4" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _FlowTime;
                float _Strength;
            CBUFFER_END
            struct A { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; float fog : TEXCOORD2; };
            V Vert(A input)
            {
                V o;
                o.world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = input.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(V i) : SV_Target
            {
                float seed = dot(floor(i.world.xz*0.3),float2(0.17,0.31));
                float2 flow = float2(i.uv.x*3.5, i.uv.y*3-_FlowTime*2.4+seed);
                float n=Noise(flow);
                float edge=smoothstep(0,0.14,i.uv.x)*smoothstep(1,0.84,i.uv.x);
                float torn=smoothstep(lerp(0.08,0.62,i.uv.y),0.8,n);
                float bottom=1-smoothstep(0.78,1,i.uv.y);
                float top=smoothstep(0,0.06+n*0.08,i.uv.y);
                float alpha=edge*torn*bottom*top*_Strength*0.44;
                clip(alpha-0.012);
                float streak=Noise(float2(i.uv.x*32,i.uv.y*1.8-_FlowTime*3));
                half3 color=lerp(half3(0.12,0.23,0.3),half3(0.5,0.68,0.76),streak*0.75+n*0.25);
                return half4(MixFog(color,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
