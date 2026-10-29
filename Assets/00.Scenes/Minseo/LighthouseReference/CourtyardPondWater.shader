Shader "Lighthouse/Studies/CourtyardPondWater"
{
 Properties {
 _NormalMap ("Ripple Normals", 2D) = "bump" {}
 _Caustics ("Bed Caustics", 2D) = "black" {}
 _Caustic_Strength ("Bed Light Strength", Range(0,2)) = 0.65
 _Caustic_Scale ("Bed Light Net Scale", Range(0.03,0.8)) = 0.14
 _Shallow_Color ("Shallow Color", Color) = (0.025,0.48,0.38,1)
 _Deep_Color ("Deep Color", Color) = (0.009,0.21,0.26,1)
 _Ripple_Strength ("Ripple Slope", Range(0,0.35)) = 0.045
 _Smoothness ("Smoothness", Range(0,1)) = 0.8
 _Refraction ("Refraction Offset", Range(0,0.02)) = 0.004
 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass {
 Tags { "LightMode"="UniversalForward" }
 Blend One Zero
 ZWrite Off
 Cull Off
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
 TEXTURE2D(_Caustics); SAMPLER(sampler_Caustics);
 CBUFFER_START(UnityPerMaterial)
 float4 _Shallow_Color, _Deep_Color;
 float _Ripple_Strength, _Smoothness, _Refraction, _Caustic_Strength, _Caustic_Scale;
 CBUFFER_END
 struct Attributes { float4 positionOS : POSITION; float2 depth : TEXCOORD1; };
 struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float depth : TEXCOORD1; };
 Varyings Vert(Attributes input) {
 Varyings output;
 output.depth = input.depth.x; output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
 output.positionCS = TransformWorldToHClip(output.positionWS);
 return output;
 }
 half4 Frag(Varyings input) : SV_Target {
 float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
 float rawDepth = SampleSceneDepth(uv);
 #if !UNITY_REVERSED_Z
 rawDepth = lerp(UNITY_NEAR_CLIP_VALUE,1,rawDepth);
 #endif
 float3 bedPosition = ComputeWorldSpacePosition(uv,rawDepth,UNITY_MATRIX_I_VP);
 float depth = clamp(input.positionWS.y-bedPosition.y,0,2);
float2 p = input.positionWS.xz;
 float time = _Time.y;
 float2 rippleUv = p * 1.6;
 float3 n0 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,rippleUv + time * float2(0.017,-0.023)));
 float2 rotated = float2(p.x * 0.73 - p.y * 0.68,p.x * 0.68 + p.y * 0.73) * 2.7;
 float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,rotated + time * float2(-0.013,0.019)));
 float2 slope = (n0.xy + n1.xy * 0.65) * _Ripple_Strength;
 float3 normal = normalize(float3(slope.x,1,slope.y));
 float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
 Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
 float3 tint = lerp(_Shallow_Color.rgb, _Deep_Color.rgb, saturate(depth / 0.7));
 float3 illumination = max(0.25, SampleSH(float3(0,1,0)) * 0.65 + sun.color * (0.28 + sun.shadowAttenuation * 0.16));
 float3 color;
 float fresnel = pow(1 - saturate(dot(normal, view)), 5);
 float3 reflected = GlossyEnvironmentReflection(reflect(-view, normal), input.positionWS, 1 - _Smoothness, 1, uv);

 float specular = pow(saturate(dot(normal, SafeNormalize(sun.direction + view))), 160);

 float3 skyHalf = SafeNormalize(view + normalize(float3(-0.14,0.92,0.64)));
 float roughness = 0.022 + min(length(fwidth(normal)),0.008) * 0.7;
 float skySpecular = pow(saturate(dot(normal,skyHalf)),clamp(2.0/(roughness*roughness),350,900));

 float2 offset = normal.xz * _Refraction * saturate(depth * 3);
 float3 background = SampleSceneColor(saturate(uv + offset));
 float2 bedBase = bedPosition.xz - sun.direction.xz * depth * 0.25;
 float2 broadWarp = float2(sin(bedBase.y*0.71+bedBase.x*0.39)+sin(bedBase.x*1.13-bedBase.y*0.43)*0.45,cos(bedBase.x*0.63-bedBase.y*0.51)+sin(bedBase.y*1.07+bedBase.x*0.35)*0.4);
 float2 bedUv = (bedBase + broadWarp * 0.48) * _Caustic_Scale;
 float2 warp = float2(sin(p.y*4.1+p.x*1.3+time*0.35)+sin(p.y*7.3-p.x*2.1-time*0.21)*0.45,sin(p.x*4.7-p.y*1.7-time*0.29)+sin(p.x*8.1+p.y*2.8+time*0.18)*0.45)*0.032;
 float c0=SAMPLE_TEXTURE2D(_Caustics,sampler_Caustics,bedUv+warp+time*float2(0.008,-0.005)).r;
 float c1=SAMPLE_TEXTURE2D(_Caustics,sampler_Caustics,bedUv*1.17-warp+time*float2(-0.005,0.007)).r;
 float caustic=min(c0,c1)*0.45+c0*0.55;
 background *= 1 + caustic * _Caustic_Strength * exp(-depth*0.9) * (0.3+0.7*sun.shadowAttenuation);
 float3 transmission = exp(-depth * float3(5.0,0.9,0.35));
 color = background * transmission + tint * illumination * (1 - transmission) * 0.10;
 color = lerp(color,reflected,0.018 + fresnel * 0.28);
 color += sun.color * (sun.shadowAttenuation * specular * 0.45 + skySpecular * (0.01 + sun.shadowAttenuation * 0.22));
 return half4(color, 1);
 }
 ENDHLSL
 }
 }
}