// The orb's body: swirling energy with depth, a darker band inside the edge and a hot rim.
// _Pulse follows the message's light; _Instability opens cracks as it nears breaking.
Shader "Game/OrbCore"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.02, 0.0, 0.07, 1)
        _Mid ("Mid", Color) = (0.5, 0.06, 0.7, 1)
        _Hot ("Hot", Color) = (0.2, 0.75, 1.0, 1)
        _Core ("Core", Color) = (1.0, 0.93, 0.82, 1)
        _Intensity ("Intensity", Float) = 2.2
        _Pulse ("Pulse", Range(0, 1)) = 0
        _Instability ("Instability", Range(0, 1)) = 0
        _Scale ("Noise Scale", Float) = 2.0
        _Flow ("Flow Speed", Float) = 0.07
        _Swirl ("Swirl", Float) = 2.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OrbCore"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GameNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep;
                half4 _Mid;
                half4 _Hot;
                half4 _Core;
                float _Intensity;
                float _Pulse;
                float _Instability;
                float _Scale;
                float _Flow;
                float _Swirl;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 viewDirOS : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(p.positionWS);
                o.viewDirOS = TransformWorldToObjectDir(o.viewDirWS);
                return o;
            }

            // Turns space around the vertical axis, faster towards the middle.
            float3 Swirl(float3 q, float t)
            {
                float r = length(q.xz);
                float a = _Swirl * (1.0 - saturate(r)) + t;
                float s = sin(a);
                float c = cos(a);
                return float3(c * q.x - s * q.z, q.y, s * q.x + c * q.z);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Flow;
                float3 n = normalize(i.normalWS);
                float3 v = normalize(i.viewDirWS);
                float facing = saturate(dot(n, v));
                float3 vOS = normalize(i.viewDirOS);
                float3 p0 = i.positionOS * 2.0;   // the unit sphere

                // Three layers stepping into the sphere, so the swirls seem to have depth.
                float density = 0.0;
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float3 q = Swirl(p0 - vOS * (0.25 * k), t * (1.0 + 0.5 * k));
                    float3 w = q * _Scale;
                    float warp = GameFbm3(w + float3(0.0, t * 3.0, 0.0));
                    float d = GameFbm4(w + warp * 1.5 + float3(t, -t, t * 0.5));
                    density += saturate(0.5 + 0.9 * d) * (1.0 - 0.25 * k);
                }
                density /= 2.25;

                // Thin bright strands where the noise crosses zero.
                float3 fq = Swirl(p0 * _Scale * 1.6, t * 2.0);
                float strand = pow(saturate(1.0 - abs(GameFbm3(fq + float3(0.0, t * 4.0, 0.0)))), 9.0);

                // Cracks that open as it destabilises.
                float crack = pow(saturate(1.0 - abs(GameNoise(p0 * 6.0 + t * 12.0))), 28.0) * _Instability;

                float heat = saturate((density - 0.3) * 1.6 + strand * (0.7 + 0.5 * _Pulse) + _Pulse * 0.12 + crack);
                half3 col = lerp(_Deep.rgb, _Mid.rgb, smoothstep(0.1, 0.45, heat));
                col = lerp(col, _Hot.rgb, smoothstep(0.45, 0.78, heat));
                col = lerp(col, _Core.rgb, saturate(smoothstep(0.82, 1.0, heat) + crack));

                // Dimmer toward the edge, then a thin hot rim right at it.
                float rim = pow(1.0 - facing, 4.0);
                col *= lerp(0.45, 1.0, facing);
                col += _Hot.rgb * rim * (0.8 + 2.5 * _Pulse);

                float energy = _Intensity * (1.0 + 0.6 * _Pulse + 1.2 * _Instability);
                return half4(col * energy, 1.0);
            }
            ENDHLSL
        }
    }
}
