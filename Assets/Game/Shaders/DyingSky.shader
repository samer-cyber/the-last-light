// The sky at the end of time: nearly black overhead, a dull red glow along the horizon where the
// dying sun sits, a faint brown smear where a galaxy was, and only a few stars left, most of them
// red. The sun's disc and the black hole are objects of their own, far away.
Shader "Game/DyingSky"
{
    Properties
    {
        _SunDirection ("Sun Direction", Vector) = (0, 0.1, 1, 0)
        _Zenith ("Zenith", Color) = (0.004, 0.005, 0.011, 1)
        _Horizon ("Horizon", Color) = (0.03, 0.028, 0.04, 1)
        _Glow ("Sun Glow", Color) = (0.55, 0.12, 0.05, 1)
        _GlowWide ("Wide Glow", Color) = (0.14, 0.04, 0.03, 1)
        _Band ("Old Galaxy", Color) = (0.03, 0.02, 0.018, 1)
        _BandNormal ("Old Galaxy Plane", Vector) = (0.3, 0.8, -0.5, 0)
        _Stars ("Stars", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GameNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SunDirection;
                half4 _Zenith;
                half4 _Horizon;
                half4 _Glow;
                half4 _GlowWide;
                half4 _Band;
                float4 _BandNormal;
                float _Stars;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.direction = v.positionOS.xyz;
                return o;
            }

            float4 Hash4(float3 p)
            {
                float4 q = float4(dot(p, float3(127.1, 311.7, 74.7)), dot(p, float3(269.5, 183.3, 246.1)),
                                  dot(p, float3(113.5, 271.9, 124.6)), dot(p, float3(19.9, 47.3, 93.1)));
                return frac(sin(q) * 43758.5453);
            }

            // One star per cell of a grid on the sphere, if the cell has one.
            half3 Stars(float3 d, float cells, float chance)
            {
                float3 g = d * cells;
                float3 cell = floor(g);
                float4 h = Hash4(cell);
                if (h.w > chance) return 0;
                float3 star = cell + 0.2 + 0.6 * h.xyz;
                float r = length(g - star);
                float size = 0.035 + 0.05 * h.x;
                float light = exp(-r * r / (size * size));
                float warmth = h.y;
                half3 colour = warmth < 0.75 ? half3(1.0, 0.45 + 0.3 * h.z, 0.3) : half3(0.85, 0.88, 1.0);
                float twinkle = 0.8 + 0.2 * sin(_Time.y * (1.0 + 3.0 * h.z) + h.x * 40.0);
                return colour * light * (0.25 + 1.2 * h.z * h.z) * twinkle;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float3 sun = normalize(_SunDirection.xyz);
                half3 colour = lerp(_Horizon.rgb, _Zenith.rgb, smoothstep(-0.02, 0.55, d.y));
                float toSun = saturate(dot(d, sun));
                float low = smoothstep(0.55, -0.05, d.y);
                colour += _Glow.rgb * pow(toSun, 14.0) + _GlowWide.rgb * pow(toSun, 2.5) * low;

                float band = exp(-pow(dot(d, normalize(_BandNormal.xyz)) / 0.16, 2.0));
                colour += _Band.rgb * band * saturate(0.4 + GameFbm3(d * 4.0) * 1.4);

                half3 stars = Stars(d, 90.0, 0.06) + Stars(d, 210.0, 0.03) * 0.6;
                colour += stars * _Stars * smoothstep(-0.02, 0.12, d.y) * (1.0 - band * 0.5);
                colour *= lerp(0.35, 1.0, smoothstep(-0.25, 0.0, d.y));
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
