#ifndef GAME_VOID_SKY_INCLUDED
#define GAME_VOID_SKY_INCLUDED

#include "GameNoise.hlsl"

// The void before anything existed: almost black, with faint slow wisps, there so that the
// bending of space around the orb can be seen. Needs Core.hlsl included first, for _Time.
half3 VoidSky(float3 dir)
{
    float t = _Time.y * 0.005;
    float3 p = dir * 1.7;
    float2 q = float2(GameFbm3(p + float3(0.0, t, 0.0)),
                      GameFbm3(p + float3(5.2, -t, 1.3)));
    float n = GameFbm4(p * 1.5 + float3(q * 1.6, t));
    float wisps = smoothstep(-0.05, 0.5, n);
    float strands = pow(saturate(1.0 - abs(GameFbm3(p * 3.4 + float3(q.yx, -t) * 1.8))), 10.0);

    half3 colour = half3(0.0015, 0.0012, 0.004);
    colour += lerp(half3(0.004, 0.016, 0.026), half3(0.026, 0.008, 0.05), saturate(0.5 + q.x * 1.5)) * wisps;
    colour += half3(0.018, 0.02, 0.045) * strands * wisps;
    return colour;
}

#endif
