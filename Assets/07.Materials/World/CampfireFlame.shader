Shader "Lighthouse/World/CampfireFlame"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 position = TransformObjectToWorld(input.positionOS.xyz);
                float height = input.uv.y;
                position.y += height * (0.035 * sin(_Time.y * 4.1) + 0.02 * sin(_Time.y * 7.3));
                float phase = _Time.y * 6.0 + position.x * 3.7 + position.z * 4.3;
                position.x += sin(phase - height * 5.0) * height * height * 0.13;
                position.z += cos(phase * 0.83 - height * 4.0) * height * height * 0.1;
                output.positionCS = TransformWorldToHClip(position);
                output.positionWS = position;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = input.uv.y;
                float facing = abs(dot(normalize(input.normalWS), normalize(GetWorldSpaceViewDir(input.positionWS))));
                float ripple = sin(input.uv.x * 18.85 + height * 10.0 - _Time.y * 8.0) * 0.06;
                float heat = saturate(height + (1.0 - facing) * 0.45 + ripple);
                float3 hot = float3(1.6, 1.0, 0.22);
                float3 orange = float3(1.25, 0.28, 0.015);
                float3 red = float3(0.9, 0.07, 0.005);
                float3 color = lerp(hot, orange, smoothstep(0.05, 0.65, heat));
                color = lerp(color, red, smoothstep(0.65, 1.0, heat));
                float alpha = smoothstep(0.0, 0.18, facing) * (1.0 - smoothstep(0.72, 1.0, height));
                float breakup = sin(height * 25.0 - _Time.y * 11.0 + sin(input.uv.x * 25.13) * 1.8);
                alpha *= lerp(0.88, 1.0, smoothstep(-0.5, 0.6, breakup));
                alpha *= 0.85;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
