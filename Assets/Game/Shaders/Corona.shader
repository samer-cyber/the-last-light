// The orb's corona: streamers of light flowing out from the core, on a quad that always faces the
// camera. Noise is taken on a circle (cos, sin of the angle) so there is no seam, and along the log
// of the radius, so the streamers keep their shape as they stretch outward.
Shader "Game/Corona"
{
    Properties
    {
        _Inner ("Inner Colour", Color) = (0.85, 0.95, 1.0, 1)
        _Outer ("Outer Colour", Color) = (0.45, 0.3, 1.0, 1)
        _Intensity ("Intensity", Float) = 2
        _Edge ("Core Edge (quad radius)", Float) = 0.1
        _Pulse ("Pulse", Range(0, 1)) = 0
        _Instability ("Instability", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Corona"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GameNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Inner;
                half4 _Outer;
                float _Intensity;
                float _Edge;
                float _Pulse;
                float _Instability;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centreVS = TransformWorldToView(TransformObjectToWorld(float3(0.0, 0.0, 0.0)));
                float scale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                o.positionCS = TransformWViewToHClip(centreVS + float3(v.positionOS.xy * scale, 0.0));
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float radii = r / _Edge;                  // distance from the centre in core radii
                if (radii < 0.95) return half4(0, 0, 0, 1);
                float angle = atan2(p.y, p.x);
                float2 around = float2(cos(angle), sin(angle));
                float t = _Time.y;
                float outward = log(radii);

                float flow = 0.35 + 0.9 * _Instability;
                float streamers = GameFbm3(float3(around * 3.0, outward * 1.6 - t * flow));
                float fine = GameNoise(float3(around * 11.0, outward * 4.0 - t * flow * 2.3));
                float rays = pow(saturate(0.55 + 0.65 * streamers + 0.12 * fine), 2.2);

                float fall = exp(-(radii - 1.0) * (1.6 - 0.6 * _Pulse));
                float hug = exp(-(radii - 1.0) * 6.0);
                float glow = fall * (0.25 + 1.6 * rays) + hug * 1.4;
                glow *= smoothstep(1.0, 0.7, r);

                half3 colour = lerp(_Inner.rgb, _Outer.rgb, saturate((radii - 1.0) / 5.0));
                float energy = _Intensity * (1.0 + 2.2 * _Pulse + 1.5 * _Instability);
                return half4(colour * glow * energy, 1.0);
            }
            ENDHLSL
        }
    }
}
