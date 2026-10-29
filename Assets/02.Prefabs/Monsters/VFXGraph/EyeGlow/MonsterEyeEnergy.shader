Shader "Lighthouse/Monster Eye Energy"
{
    Properties { _Trail ("Trail Mode", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial)
            float _Trail;
            CBUFFER_END
            Varyings vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; o.color=i.color; return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p=i.uv*2-1;
                if (_Trail > 0.5) { float a=pow(saturate(1-abs(p.y)), 1.6); return half4(i.color.rgb, i.color.a*a); }
                float r=length(p);
                float core=1.0-smoothstep(0.18,0.42,r);
                float halo=pow(saturate(1.0-r),2.0);
                float pulse=0.94+0.06*sin(_Time.y*2.5);
                float intensity=saturate(core+halo*0.75)*pulse;
                float3 color=lerp(i.color.rgb*2.2, float3(2.2,3.4,4.0), core*0.7);
                return half4(color, i.color.a*intensity);
            }
            ENDHLSL
        }
    }
}
