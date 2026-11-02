// A rocky planet, young or grown: crust and liquid rock, seas filling the low ground, polar ice, a
// white veil of steam and swirls of cloud, lit by the Sun (the main light). Liquid rock shines by
// itself, so the night side of a molten world glows. Planet.cs eases the amounts over time.
Shader "Game/Planet"
{
    Properties
    {
        _Seed ("Seed", Vector) = (0, 0, 0, 0)
        _Scale ("Feature Scale", Float) = 2.2
        _Rock ("Rock", Color) = (0.11, 0.1, 0.1, 1)
        _RockHigh ("High Rock", Color) = (0.3, 0.27, 0.24, 1)
        _Lava ("Lava", Color) = (1.0, 0.3, 0.05, 1)
        _LavaHot ("Hot Lava", Color) = (1.0, 0.75, 0.3, 1)
        _Deep ("Deep Sea", Color) = (0.01, 0.04, 0.12, 1)
        _Shallow ("Shallow Sea", Color) = (0.03, 0.16, 0.26, 1)
        _CloudColor ("Cloud", Color) = (0.95, 0.96, 1.0, 1)
        _Molten ("Molten", Range(0, 1)) = 0.5
        _Glow ("Glow", Float) = 4
        _Sea ("Sea", Range(0, 1)) = 0
        _Steam ("Steam", Range(0, 1)) = 0
        _Clouds ("Clouds", Range(0, 1)) = 0
        _Ice ("Ice", Range(0, 1)) = 0
        _Craters ("Craters", Range(0, 1)) = 0
        _Ambient ("Ambient", Float) = 0.012
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Planet"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GameNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Seed;
                float _Scale;
                half4 _Rock;
                half4 _RockHigh;
                half4 _Lava;
                half4 _LavaHot;
                half4 _Deep;
                half4 _Shallow;
                half4 _CloudColor;
                float _Molten;
                float _Glow;
                float _Sea;
                float _Steam;
                float _Clouds;
                float _Ice;
                float _Craters;
                float _Ambient;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 local : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.local = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            // Bowls with raised rims, scattered by a cell noise: 1 on a rim, -1 at a floor.
            float Craters(float3 n)
            {
                float3 p = n * 9.0 + _Seed.zxy;
                float3 cell = floor(p);
                float shape = 0.0;
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    float3 c = cell + float3(x, y, z);
                    float3 jitter = GameHash3(c) * 0.5 + 0.5;
                    float size = 0.25 + 0.3 * jitter.x;
                    float d = length(p - (c + jitter)) / size;
                    shape += (d < 1.0 ? -(1.0 - d * d) : exp(-(d - 1.0) * (d - 1.0) * 30.0) * 0.6) * step(jitter.y, 0.55);
                }
                return shape;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.local);
                float t = _Time.y;
                float3 p = n * _Scale + _Seed.xyz;

                // Height: low basins and high ground.
                float h = saturate(GameFbm4(p) * 0.9 + 0.5);
                float crater = _Craters > 0.0 ? Craters(n) * _Craters : 0.0;
                h = saturate(h + crater * 0.08);

                // Liquid rock fills the low ground as the world melts, and glows along the seams between plates.
                float drift = t * 0.004;
                float seam = pow(saturate(1.0 - abs(GameNoise(n * 6.0 + _Seed.yzx + float3(drift, 0.0, -drift)))), 14.0);
                float lavaLevel = lerp(-0.15, 1.15, _Molten);
                float pool = 1.0 - smoothstep(lavaLevel - 0.06, lavaLevel + 0.06, h);
                float seams = seam * saturate(_Molten * 4.0 + 0.04);
                float lava = saturate(max(pool, seams));
                float churn = GameFbm3(n * 11.0 + _Seed.zxy + float3(0.0, t * 0.03, 0.0)) * 0.5 + 0.5;
                half3 lavaColour = lerp(_Lava.rgb, _LavaHot.rgb, saturate(churn * 1.3 - 0.2 + pool * 0.25));

                // Rock.
                float grain = GameNoise(n * 40.0 + _Seed.xyz) * 0.5 + 0.5;
                half3 rock = lerp(_Rock.rgb, _RockHigh.rgb, saturate(h * 1.2 - 0.1)) * (0.85 + 0.3 * grain) * (1.0 + crater * 0.25);

                // Seas fill the low ground and put out the liquid rock.
                float seaLevel = lerp(-0.05, 0.62, _Sea);
                float water = (1.0 - smoothstep(seaLevel - 0.015, seaLevel + 0.015, h)) * step(0.001, _Sea);
                half3 sea = lerp(_Shallow.rgb, _Deep.rgb, saturate((seaLevel - h) * 7.0));
                lava *= 1.0 - water;

                // Ice grows from the poles.
                float iceEdge = 1.0 - _Ice * 0.55;
                float ice = smoothstep(iceEdge, iceEdge + 0.04, abs(n.y) + 0.04 * GameNoise(n * 8.0)) * step(0.001, _Ice);

                half3 albedo = lerp(rock, sea, water);
                albedo = lerp(albedo, half3(0.85, 0.88, 0.92), ice);
                albedo = lerp(albedo, lavaColour * 0.15, lava);

                // Clouds swirl in bands; steam is a white veil over everything.
                float3 q = n * 3.2 + _Seed.yxz;
                float swirl = GameFbm3(q + float3(t * 0.01, 0.0, 0.0));
                float cloudField = GameFbm4(float3(q.x + swirl * 1.5 + t * 0.006, q.y * 1.8, q.z + swirl)) * 0.5 + 0.5;
                float cloud = smoothstep(0.62 - 0.32 * _Clouds, 0.8 - 0.25 * _Clouds, cloudField) * step(0.001, _Clouds);
                float veil = _Steam * (0.75 + 0.25 * cloudField);
                float cover = saturate(max(cloud, veil));

                // Light from the Sun.
                Light sun = GetMainLight();
                float3 N = normalize(i.normalWS);
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                float ndl = dot(N, sun.direction);
                float lit = saturate(ndl);
                float soft = saturate(ndl * 0.8 + 0.2);
                half3 colour = albedo * (sun.color * lit + _Ambient);
                float3 H = normalize(sun.direction + V);
                colour += sun.color * water * (1.0 - ice) * pow(saturate(dot(N, H)), 120.0) * 1.5 * lit;
                colour = lerp(colour, _CloudColor.rgb * (sun.color * soft * soft + _Ambient), cover);

                // Liquid rock shines by itself, dimmed where steam and cloud cover it.
                colour += lavaColour * lava * _Glow * (0.6 + 0.6 * churn) * (1.0 - 0.85 * cover);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
}
