Shader "Lighthouse/World/ClearOcean"
{
    Properties
    {
        _Shallow_Color ("Shallow Color", Color) = (0.02, 0.95, 0.9, 1)
        _Deep_Color ("Deep Color", Color) = (0.015, 0.65, 0.95, 1)
        _Very_Deep_Color ("Very Deep Color", Color) = (0.005, 0.28, 0.85, 1)
        _Base_Opacity ("Deep Opacity", Range(0, 1)) = 0.96
        _Shallows_Opacity ("Shallow Opacity", Range(0, 1)) = 0.14
        _Smoothness ("Smoothness", Range(0, 1)) = 0.75
        _Deep_Height ("Shallow Transition Metres", Float) = 1.25
        _Very_Deep_Height ("Deep Transition Metres", Float) = 22
        _Absorption ("Absorption Per Metre", Range(0, 1)) = 0.12
        _Path_Tint ("Optical Path Tint", Range(0, 1)) = 0.35
        _Deep_Falloff ("Deep Color Falloff", Range(1, 12)) = 8
        _Ripple_Strength ("Ripple Strength", Range(0, 0.3)) = 0.055
        _Reflection_Strength ("Reflection Strength", Range(0, 1)) = 0.25
        _Refraction_Strength ("Refraction Strength", Range(0, 0.02)) = 0.002
        _Sparkle_Strength ("Sun Sparkle", Range(0, 3)) = 1
        _Far_Smooth ("Far Normal Smoothing", Range(0, 1)) = 0.65
        _Depth_Fade ("Seabed Fade Per Metre", Range(0, 2)) = 0.35
        _Clarity ("Open Sea Clarity", Range(0, 1)) = 0
        _Lagoon_Reach ("Island Lagoon Reach Metres", Range(2, 14)) = 11
        _Sky_Light ("Sky Light", Range(0, 2)) = 0
        _Zone_Radii ("Zone Outer Radii + Blend Half Width", Vector) = (200, 360, 490, 60)
        _Zone_Tint0 ("Inner Tint + Clarity Scale", Vector) = (1, 1, 1, 1)
        _Zone_Tint1 ("Middle Tint + Clarity Scale", Vector) = (1, 1, 1, 1)
        _Zone_Tint2 ("Outer Tint + Clarity Scale", Vector) = (1, 1, 1, 1)
        _Zone_Sky ("Zone Sky Scale", Vector) = (1, 1, 1, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-10" }
        Pass
        {
            Name "ClearOcean"
            Tags { "LightMode" = "UniversalForward" }
            Blend One Zero
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Include/OceanGerstner.hlsl"
            #include "Include/OceanInteraction.hlsl"
            #include "Include/OceanColor.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Shallow_Color;
                float4 _Deep_Color;
                float4 _Very_Deep_Color;
                float _Base_Opacity;
                float _Shallows_Opacity;
                float _Smoothness;
                float _Deep_Height;
                float _Very_Deep_Height;
                float _Absorption;
                float _Path_Tint;
                float _Deep_Falloff;
                float _Ripple_Strength;
                float _Reflection_Strength;
                float _Refraction_Strength;
                float _Sparkle_Strength;
                float _Far_Smooth;
                float _Depth_Fade;
                float _Clarity;
                float _Lagoon_Reach;
                float _Sky_Light;
                float4 _Zone_Radii;
                float4 _Zone_Tint0;
                float4 _Zone_Tint1;
                float4 _Zone_Tint2;
                float4 _Zone_Sky;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float4 coast : TEXCOORD3;
                float3 restWS : TEXCOORD4;
                float3 normalDelta : TEXCOORD5;
                float2 lagoon : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 restWS = TransformObjectToWorld(input.positionOS);
                float lagoonReach;
                OceanGerstnerFieldLagoon_float(restWS, output.positionWS, output.normalWS, output.coast, lagoonReach);
                output.lagoon = float2(lagoonReach, step(0.01, lagoonReach));
                output.restWS = restWS;
                output.normalDelta = output.normalWS - OceanGerstnerBaseNormal(restWS);
                output.positionWS.y += OceanInteraction(output.positionWS.xz).x;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float3 RippleNormal(float3 positionWS, float3 normalWS)
            {
                float2 slope = OceanRippleSlope(positionWS.xz, _OceanTime);
                float footprint = max(length(ddx(positionWS.xz)), length(ddy(positionWS.xz)));
                float fade = 1.0 - smoothstep(0.08, 0.8, footprint);
                return normalize(normalWS + float3(-slope.x, 0, -slope.y) * _Ripple_Strength * 2.5 * fade);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(uv);
                float ndcDepth = rawDepth;
                #if !UNITY_REVERSED_Z
                    ndcDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif
                float3 bottomWS = ComputeWorldSpacePosition(uv, ndcDepth, UNITY_MATRIX_I_VP);
                float bottomEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float waterEye = -TransformWorldToView(input.positionWS).z;
                float thickness = max(0, bottomEye - waterEye);
                float depth = max(0, input.positionWS.y - bottomWS.y);
                #if UNITY_REVERSED_Z
                    bool hasBottom = rawDepth > 0.00001 && thickness > 0;
                #else
                    bool hasBottom = rawDepth < 0.99999 && thickness > 0;
                #endif
                // Screen-space depth returns whatever the straight view ray hits, which can be a shelf
                // far ahead of the pixel. Underwater rays refract steeply, so a bottom whose horizontal
                // run is long compared with its depth is not the bottom below this pixel.
                // The coast field interpolates to zero at the edge of its coverage; its unit gradient shrinks there,
                // so the gradient length tells valid pixels from blended ones.
                float coastValid = smoothstep(0.8, 0.97, length(input.coast.xy)) * step(0.001, input.coast.w);
                // Per-island reach is interpolated with a validity weight so zero-coverage vertices do not bias it.
                float lagoonReach = lerp(_Lagoon_Reach, input.lagoon.x / max(input.lagoon.y, 0.001), step(0.5, input.lagoon.y));
                float lagoonT = lerp(1.0, saturate(max(input.coast.z, 0.0) / max(lagoonReach, 0.1)), coastValid);
                float bottomRun = length(bottomWS.xz - input.positionWS.xz);
                float maxRun = (depth + 0.3) * 1.4;
                float bottomValid = hasBottom ? (1.0 - smoothstep(maxRun, maxRun * 1.8, bottomRun)) * (1.0 - smoothstep(0.5, 0.92, lagoonT)) : 0.0;
                hasBottom = hasBottom && bottomValid > 0.0;
                depth = lerp(max(_Very_Deep_Height, 0.2), depth, bottomValid);
                float shallowEnd = max(0.1, _Deep_Height);
                float deepEnd = max(shallowEnd + 0.1, _Very_Deep_Height);
                if (!hasBottom)
                {
                    depth = deepEnd;
                    thickness = deepEnd * 4;
                }
                float3 zoneW = OceanZoneWeights(input.positionWS.xz, _Zone_Radii);
                float3 zoneTint = _Zone_Tint0.rgb * zoneW.x + _Zone_Tint1.rgb * zoneW.y + _Zone_Tint2.rgb * zoneW.z;
                float clarityZ = _Clarity * dot(zoneW, float3(_Zone_Tint0.w, _Zone_Tint1.w, _Zone_Tint2.w));
                float skyLightZ = _Sky_Light * dot(zoneW, _Zone_Sky.xyz);
                float3 zoneDeep = _Deep_Color.rgb * zoneTint;
                float3 waterColor = OceanBodyColor(_Shallow_Color.rgb * zoneTint, zoneDeep, _Very_Deep_Color.rgb * zoneTint, depth, shallowEnd, deepEnd, _Deep_Falloff);
                float clarityPatch = OceanClarityPatch(input.positionWS.xz, _OceanTime);
                float clarityLift = (clarityPatch - 0.5) * clarityZ;
                waterColor *= 1.0 + clarityLift * 0.3;
                waterColor = lerp(waterColor, lerp(waterColor, zoneDeep, 0.25), saturate(clarityLift * 2.0));

                // Island lagoon: turquoise at the shore fading to the deep blue over the coast reach.
                float lagoonMix = (1.0 - smoothstep(0.0, 1.0, lagoonT)) * saturate(_Clarity * 1.5);
                waterColor = lerp(waterColor, lerp(_Shallow_Color.rgb, _Deep_Color.rgb, smoothstep(0.0, 1.0, lagoonT)), lagoonMix);

                float3 surfacePosition = input.positionWS;
                // Per-pixel Gerstner normal: vertex spacing is coarser than the shortest wavelengths.
                float3 interactionNormal = normalize(OceanGerstnerBaseNormal(input.restWS) + input.normalDelta);
                OceanInteractionDisplace(surfacePosition, interactionNormal);
                float3 normalWS = RippleNormal(input.positionWS, interactionNormal);
                float cameraDistance = length(input.positionWS - _WorldSpaceCameraPos);
                normalWS = normalize(lerp(normalWS, float3(0, 1, 0), smoothstep(250, 900, cameraDistance) * _Far_Smooth));
                float footprint = max(length(ddx(input.positionWS.xz)), length(ddy(input.positionWS.xz)));
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 offset = normalWS.xz * _Refraction_Strength * saturate(thickness);
                float2 refractedUV = saturate(uv + offset);
                float refractedEye = LinearEyeDepth(SampleSceneDepth(refractedUV), _ZBufferParams);
                float edgeFade = saturate(min(min(uv.x, uv.y), min(1 - uv.x, 1 - uv.y)) * 40);
                refractedUV = lerp(uv, refractedUV, step(waterEye + 0.02, refractedEye) * edgeFade);
                float3 sceneColor = SampleSceneColor(refractedUV);
                // Sand keeps its own lighting and texture. Red light is absorbed first.
                float path = hasBottom ? length(bottomWS - input.positionWS) + depth : 0;
                float3 absorption = max(0.005, _Absorption) * float3(12.0, 1.8, 0.5);
                float3 transmission = hasBottom ? exp(-path * absorption) : float3(0, 0, 0);
                float turbidity = saturate((_Shallows_Opacity - 0.14) * 0.65);
                transmission *= 1 - turbidity;
                transmission *= bottomValid;
                // Vertical depth fade: steep drop-offs and island skirts must sink into the water colour.
                transmission *= exp(-depth * _Depth_Fade);

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float diffuse = 0.45 + 0.55 * saturate(dot(normalWS, sun.direction));
                float3 illumination = sun.color * diffuse * lerp(0.65, 1, sun.shadowAttenuation);
                illumination += SampleSH(normalWS) * 0.35;
                float luminance = dot(waterColor, float3(0.2126, 0.7152, 0.0722));
                float3 color = sceneColor * transmission + waterColor * illumination * (1 - transmission);
                float fresnel = 0.02 + 0.98 * pow(1 - saturate(dot(normalWS, viewWS)), 5);
                float3 reflected = GlossyEnvironmentReflection(reflect(-viewWS, normalWS), input.positionWS, 1 - _Smoothness, 1, uv);
                float reflectionWeight = fresnel * _Reflection_Strength * saturate(luminance * 6);
                color = lerp(color, reflected, reflectionWeight);
                color += OceanAmbientGlow(waterColor, _Shallow_Color.rgb * zoneTint, fresnel, skyLightZ);
                float3 halfway = SafeNormalize(sun.direction + viewWS);
                float specularPower = exp2(4 + _Smoothness * 7);
                float specular = pow(saturate(dot(normalWS, halfway)), specularPower);
                color += sun.color * sun.shadowAttenuation * specular * 0.05 * clarityZ;
                float causticFade = 1.0 - smoothstep(0.3, 1.2, footprint);
                float causticMask = saturate(clarityPatch * 1.6 - 0.3) * clarityZ * clarityZ * causticFade;
                color += sun.color * sun.shadowAttenuation * causticMask * 0.06
                    * OceanCaustic(input.positionWS.xz, normalWS.xz, _OceanTime) * (1 - transmission.g);
                float shallowCaustic = OceanCaustic(input.positionWS.xz, normalWS.xz, _OceanTime)
                    * transmission.g * clarityZ * clarityZ * causticFade;
                color += sun.color * sun.shadowAttenuation * shallowCaustic * 0.22;
                color += OceanCrestGlow(_Shallow_Color.rgb, sun.color, viewWS, sun.direction,
                    input.positionWS.y - input.restWS.y, _Path_Tint);
                color += sun.color * sun.shadowAttenuation * _Sparkle_Strength
                    * OceanSparkle(input.positionWS, normalWS, halfway, _OceanTime, footprint) * clarityZ * clarityZ;
                float phase = CoastPhase(input.coast.z,_OceanTime)/OCEAN_TWO_PI;
                float cycle = frac(phase);
                float crest = 1-smoothstep(0.025,0.095,abs(cycle-0.25));
                float residue = smoothstep(0.22,0.32,cycle)*(1-smoothstep(0.48,0.78,cycle));
                float shoreWindow = smoothstep(0,0.4,input.coast.z)
                    *(1-smoothstep(input.coast.w*0.6,input.coast.w,input.coast.z))*step(0.01,input.coast.w);
                float energy = 0;
                for(int i=0;i<min((int)_OceanWaveCount,OCEAN_MAX_WAVES);i++) energy+=_OceanWaveB[i].x;
                float shoreDensity = max(crest*0.95,residue*0.6)*shoreWindow*saturate(energy*5);
                float4 interaction = OceanInteraction(input.positionWS.xz);
                float density = max(shoreDensity,interaction.w);
                float coverage = OceanFoamCoverage(input.positionWS.xz-interaction.yz*0.12,density,_OceanTime);
                float3 foamLight = max(0.025,sun.color*0.85+SampleSH(float3(0,1,0))*0.4);
                color = lerp(color,foamLight,coverage*saturate(density)*0.82);
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
}
