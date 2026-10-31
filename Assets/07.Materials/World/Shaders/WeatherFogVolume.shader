Shader "Lighthouse/World/WeatherFogVolume"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.55, 0.62, 0.64, 1)
        _Density ("Extinction Per Metre", Float) = 0.04
        _NoiseScale ("Cloud Size Metres", Float) = 35
        _WindTime ("Wind And Time", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest Always
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _Density;
                float _NoiseScale;
                float4 _WindTime;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
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

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 camera = GetCameraPositionWS();
                float3 direction = normalize(input.positionWS - camera);
                float3 originOS = TransformWorldToObject(camera);
                float3 directionOS = mul((float3x3)unity_WorldToObject, direction);
                directionOS = sign(directionOS + 1e-8) * max(abs(directionOS), 1e-8);
                float3 nearPlane = (-0.5 - originOS) / directionOS;
                float3 farPlane = (0.5 - originOS) / directionOS;
                float3 nearAxes = min(nearPlane, farPlane);
                float3 farAxes = max(nearPlane, farPlane);
                float entry = max(0, max(nearAxes.x, max(nearAxes.y, nearAxes.z)));
                float exit = min(farAxes.x, min(farAxes.y, farAxes.z));
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float eyeDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float viewZ = -mul((float3x3)UNITY_MATRIX_V, direction).z;
                exit = min(exit, eyeDepth / max(0.001, viewZ));
                if (exit <= entry || _Density <= 0) { return 0; }

                const int Steps = 32;
                float stepLength = (exit - entry) / Steps;
                float distance = entry + stepLength * 0.5;
                float transmittance = 1;
                float scattering = 0;
                float3 drift = float3(_WindTime.x, 0, _WindTime.y) * _WindTime.z * 0.12;
                [loop] for (int i = 0; i < Steps; i++)
                {
                    float3 world = camera + direction * distance;
                    float3 local = originOS + directionOS * distance;
                    float3 p = (world - drift) / max(1, _NoiseScale);
                    float bank = Noise(float3(p.x * 0.8, 1.7, p.z * 0.8));
                    float cloud = Noise(p * float3(1, 2.4, 1));
                    cloud = cloud * 0.72 + Noise(p * 2.7) * 0.28;
                    float top = lerp(-0.25, 0.46, smoothstep(0.25, 0.75, bank));
                    float height = (1 - smoothstep(top - 0.16, top, local.y)) * smoothstep(-0.5, -0.3, local.y);
                    float edge = 1 - smoothstep(0.36, 0.5, max(abs(local.x), abs(local.z)));
                    float density = smoothstep(0.38, 0.7, cloud) * height * edge;
                    float attenuation = exp(-density * _Density * stepLength);
                    float light = lerp(0.62, 1, saturate((local.y + 0.4) / 0.7));
                    scattering += transmittance * (1 - attenuation) * light;
                    transmittance *= attenuation;
                    distance += stepLength;
                }
                float alpha = 1 - transmittance;
                return half4(_FogColor.rgb * scattering, alpha);
            }
            ENDHLSL
        }
    }
}
