Shader "Lighthouse/World/StormWallFog"
{
    Properties
    {
        _Color ("Fog Color", Color) = (1, 1, 1, 1)
        _Opacity ("Max Opacity", Range(0, 1)) = 0.6
        _CloudSize ("Cloud Size Metres", Float) = 28
        _Drift ("Drift Metres Per Second", Vector) = (1.2, 0.15, 0.6, 0)
        _Coverage ("Coverage", Range(0, 1)) = 0.42
        _Softness ("Softness", Range(0.05, 0.6)) = 0.3
        _TopFade ("Top Fade Start", Range(0, 1)) = 0.35
        _NearFade ("Near Fade Start And End", Vector) = (6, 30, 0, 0)
        _WaterLevel ("Water Level In Wall Height", Range(0, 1)) = 0.2
        _BottomFade ("Rise Min Scale And Rise Length", Vector) = (0.3, 0.2, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _WaterLevel;
                float4 _BottomFade;
                float4 _Color;
                float _Opacity;
                float _CloudSize;
                float4 _Drift;
                float _Coverage;
                float _Softness;
                float _TopFade;
                float4 _NearFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float height : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.height = input.positionOS.y;
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(
                    lerp(lerp(Hash(cell), Hash(cell + float3(1,0,0)), f.x),
                         lerp(Hash(cell + float3(0,1,0)), Hash(cell + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash(cell + float3(0,0,1)), Hash(cell + float3(1,0,1)), f.x),
                         lerp(Hash(cell + float3(0,1,1)), Hash(cell + float3(1,1,1)), f.x), f.y), f.z);
            }

            float Cloud(float3 p)
            {
                return Noise(p) * 0.72 + Noise(p * 1.9 + 7.3) * 0.22 + Noise(p * 3.7 + 19.1) * 0.06;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p = (input.positionWS - _Drift.xyz * _Time.y) / max(1, _CloudSize);
                p.y *= 1.1;

                float cloud = Cloud(p);
                float density = smoothstep(_Coverage, _Coverage + _Softness, cloud);

                float topNoise = Noise(float3(p.x * 0.7, 3.1, p.z * 0.7));
                float top = lerp(_TopFade, 1, topNoise);
                float heightFade = 1 - smoothstep(top - 0.3, top + 0.05, input.height);

                float bottomNoise = Noise(float3(p.x * 0.9, 5.7, p.z * 0.9)) * 0.6 + cloud * 0.4;
                float riseLength = _BottomFade.y * (_BottomFade.x + bottomNoise * 1.6);
                heightFade *= smoothstep(_WaterLevel, _WaterLevel + riseLength, input.height);

                float dist = distance(GetCameraPositionWS(), input.positionWS);
                float nearFade = smoothstep(_NearFade.x, _NearFade.y, dist);

                float alpha = _Opacity * density * heightFade * nearFade;
                float3 lightTint = lerp(0.45, 1, saturate(SampleSH(float3(0, 1, 0)) + GetMainLight().color * 0.6));
                return half4(_Color.rgb * lightTint, alpha);
            }
            ENDHLSL
        }
    }
}
