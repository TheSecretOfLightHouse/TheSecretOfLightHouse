Shader "Lighthouse/Player/SearchlightVolume"
{
    Properties
    {
        [HDR] _Tint ("Scattering Color", Color) = (0.65, 0.78, 1, 1)
        _Density ("Density", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Density;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 rayWS = normalize(input.positionWS - _WorldSpaceCameraPos);
                float3 origin = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 direction = mul((float3x3)unity_WorldToObject, rayWS);
                // Keep t in world metres even though the bounding cube is scaled.
                float3 safeDirection = direction + step(abs(direction), 0.000001) * 0.000001;
                float3 a = (-0.5 - origin) / safeDirection;
                float3 b = (0.5 - origin) / safeDirection;
                float3 nearPlane = min(a, b);
                float3 farPlane = max(a, b);
                float start = max(0, max(nearPlane.x, max(nearPlane.y, nearPlane.z)));
                float end = min(farPlane.x, min(farPlane.y, farPlane.z));

                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 surfaceWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                end = min(end, dot(surfaceWS - _WorldSpaceCameraPos, rayWS));
                if (end <= start) { return 0; }

                float stride = (end - start) / 48.0;
                float sum = 0;
                [loop] for (int i = 0; i < 48; i++)
                {
                    float t = start + (i + 0.5) * stride;
                    float3 p = origin + direction * t;
                    float along = p.z + 0.5;
                    float radius = max(0.001, along * 0.5);
                    float radial = length(p.xy) / radius;
                    float edge = 1 - smoothstep(0.35, 1, radial);
                    float tip = smoothstep(0, 0.025, along);
                    float tail = 1 - smoothstep(0.55, 1, along);
                    float surfaceFade = saturate((end - t) / 0.4);
                    sum += edge * edge * tip * tail * surfaceFade / (1 + along * along * 10);
                }
                float scattering = 1 - exp(-sum * stride * _Density);
                return half4(_Tint.rgb * scattering, 0);
            }
            ENDHLSL
        }
    }
}
