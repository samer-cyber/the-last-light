#ifndef GAME_LIT_INCLUDED
#define GAME_LIT_INCLUDED

// What the lit shaders of the frozen world share: URP's physically based lighting (the red sun,
// the mind's light, shadows, fog) for a surface a shader has worked out, and the shadow caster's
// position. Written for this project.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "GameNoise.hlsl"

half4 GameShade(float3 positionWS, half3 normalWS, float4 positionCS, half3 albedo, half metallic, half smoothness,
                half3 emission, half occlusion, half fogFactor)
{
    InputData input = (InputData)0;
    input.positionWS = positionWS;
    input.positionCS = positionCS;
    input.normalWS = normalize(normalWS);
    input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    input.shadowCoord = TransformWorldToShadowCoord(positionWS);
    input.fogCoord = fogFactor;
    input.bakedGI = SampleSH(input.normalWS);
    input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    input.shadowMask = half4(1, 1, 1, 1);

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo;
    surface.metallic = metallic;
    surface.smoothness = smoothness;
    surface.occlusion = occlusion;
    surface.emission = emission;
    surface.alpha = 1;
    surface.normalTS = half3(0, 0, 1);

    half4 colour = UniversalFragmentPBR(input, surface);
    colour.rgb = MixFog(colour.rgb, fogFactor);
    return colour;
}

// A normal bent by noise, from the noise's slope found by small steps.
float3 GameBumped(float3 normalWS, float3 positionWS, float scale, float amount)
{
    const float e = 0.02;
    float3 p = positionWS * scale;
    float n = GameFbm3(p);
    float3 slope = float3(GameFbm3(p + float3(e, 0, 0)) - n, GameFbm3(p + float3(0, e, 0)) - n, GameFbm3(p + float3(0, 0, e)) - n) / e;
    slope -= normalWS * dot(slope, normalWS);
    return normalize(normalWS - slope * amount);
}

#ifdef GAME_SHADOW_CASTER
float3 _LightDirection;
float3 _LightPosition;

float4 GameShadowPosition(float3 positionOS, float3 normalOS)
{
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 normalWS = TransformObjectToWorldNormal(normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    return positionCS;
}
#endif

#endif
