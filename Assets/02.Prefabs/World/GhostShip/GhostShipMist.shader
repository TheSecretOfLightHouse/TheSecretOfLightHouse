Shader "Lighthouse/World/GhostShipMist"
{
    // Low sea mist around the ghost ship, ray-marched inside a unit cube (scale the object to size the bank).
    // Thick at the waterline and thinning upward, round at the edges, drifting with world-space noise so it does not
    // stretch with the box. Nothing below the sea level or behind the hull: the march stops at the scene depth.
    Properties
    {
        _Color ("Mist Color", Color) = (0.62, 0.74, 0.72, 1)
        [HDR] _GlowColor ("Lantern Glow", Color) = (0.25, 0.9, 0.55, 1)
        _GlowRadius ("Glow Radius (box 0-1)", Range(0.05, 1)) = 0.35
        _Density ("Density", Range(0, 2)) = 0.55
        _HeightFalloff ("Height Falloff", Range(0, 12)) = 4.5
        _EdgeSoftness ("Edge Start (box 0-1)", Range(0, 1)) = 0.45
        _NoiseScale ("Noise Scale (per metre)", Range(0.01, 1)) = 0.11
        _NoiseContrast ("Noise Contrast", Range(0.5, 4)) = 1.8
        _Wind ("Drift (m/s)", Vector) = (0.6, 0.15, 0.35, 0)
        _SeaLevel ("Sea Level (world y)", Float) = 0
        _NearFade ("Camera Near Fade (m)", Range(0.1, 10)) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Fewer steps cost less; the noise is soft enough that banding stays hidden.
            #define STEPS 28

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _GlowColor;
                float _GlowRadius;
                float _Density;
                float _HeightFalloff;
                float _EdgeSoftness;
                float _NoiseScale;
                float _NoiseContrast;
                float4 _Wind;
                float _SeaLevel;
                float _NearFade;
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

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                         lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                         lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float sum = ValueNoise(p) * 0.55;
                sum += ValueNoise(p * 2.03 + 7.1) * 0.3;
                sum += ValueNoise(p * 4.11 + 13.7) * 0.15;
                return sum;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 rayWS = normalize(input.positionWS - _WorldSpaceCameraPos);
                float3 origin = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 direction = mul((float3x3)unity_WorldToObject, rayWS);
                // t stays in world metres although the cube is scaled.
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

                float stride = (end - start) / STEPS;
                float3 drift = _Wind.xyz * _Time.y;
                float sum = 0;
                float glow = 0;
                [loop] for (int i = 0; i < STEPS; i++)
                {
                    float t = start + (i + 0.5) * stride;
                    float3 p = origin + direction * t;
                    float3 world = _WorldSpaceCameraPos + rayWS * t;

                    float height = p.y + 0.5;
                    float radial = length(p.xz * 2.0);
                    float edge = 1 - smoothstep(_EdgeSoftness, 1, radial);
                    // Slow, larger noise lifts the bank unevenly so its top reads as billows, not a flat lid.
                    float lift = ValueNoise((world - drift * 0.4) * _NoiseScale * 0.45);
                    float layer = exp(-max(0, height - lift * 0.3) * _HeightFalloff) * (1 - smoothstep(0.6, 1, height));
                    float aboveSea = smoothstep(-0.3, 0.6, world.y - _SeaLevel);
                    float near = saturate(t / _NearFade);
                    float noise = saturate((Fbm((world - drift) * _NoiseScale) - 0.5) * _NoiseContrast + 0.5);

                    float density = noise * edge * layer * aboveSea * near;
                    sum += density;
                    glow += density * (1 - smoothstep(0, _GlowRadius, radial));
                }

                float alpha = 1 - exp(-sum * stride * _Density);
                float glowShare = sum > 0.0001 ? glow / sum : 0;
                half3 color = _Color.rgb + _GlowColor.rgb * glowShare;
                return half4(color, alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
