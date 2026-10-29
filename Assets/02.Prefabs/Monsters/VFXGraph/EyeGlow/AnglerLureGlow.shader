Shader "Lighthouse/Angler Lure Glow"
{
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
            Varyings vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; o.color=i.color; return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float r=length(i.uv*2.0-1.0);
                float glow=pow(saturate(1.0-r),2.0);
                float pulse=0.9+0.1*sin(_Time.y*2.0);
                return half4(i.color.rgb*2.0, i.color.a*glow*pulse);
            }
            ENDHLSL
        }
    }
}
