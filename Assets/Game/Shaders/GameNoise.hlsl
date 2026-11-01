#ifndef GAME_NOISE_INCLUDED
#define GAME_NOISE_INCLUDED

// Gradient noise in 3D and a fractal sum of it, written for this project.

float3 GameHash3(float3 p)
{
    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
               dot(p, float3(269.5, 183.3, 246.1)),
               dot(p, float3(113.5, 271.9, 124.6)));
    return -1.0 + 2.0 * frac(sin(p) * 43758.5453);
}

// Roughly -1..1, smooth, no visible grid.
float GameNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

    float n000 = dot(GameHash3(i + float3(0, 0, 0)), f - float3(0, 0, 0));
    float n100 = dot(GameHash3(i + float3(1, 0, 0)), f - float3(1, 0, 0));
    float n010 = dot(GameHash3(i + float3(0, 1, 0)), f - float3(0, 1, 0));
    float n110 = dot(GameHash3(i + float3(1, 1, 0)), f - float3(1, 1, 0));
    float n001 = dot(GameHash3(i + float3(0, 0, 1)), f - float3(0, 0, 1));
    float n101 = dot(GameHash3(i + float3(1, 0, 1)), f - float3(1, 0, 1));
    float n011 = dot(GameHash3(i + float3(0, 1, 1)), f - float3(0, 1, 1));
    float n111 = dot(GameHash3(i + float3(1, 1, 1)), f - float3(1, 1, 1));

    float x00 = lerp(n000, n100, u.x);
    float x10 = lerp(n010, n110, u.x);
    float x01 = lerp(n001, n101, u.x);
    float x11 = lerp(n011, n111, u.x);
    return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z) * 1.6;
}

float GameFbm3(float3 p)
{
    float sum = 0.5 * GameNoise(p);
    p = p * 2.03 + float3(1.7, 9.2, 3.1);
    sum += 0.25 * GameNoise(p);
    p = p * 2.01 + float3(8.3, 2.8, 5.4);
    sum += 0.125 * GameNoise(p);
    return sum;
}

float GameFbm4(float3 p)
{
    float sum = GameFbm3(p);
    p = p * 8.18 + float3(4.1, 6.6, 1.2);
    return sum + 0.0625 * GameNoise(p);
}

#endif
