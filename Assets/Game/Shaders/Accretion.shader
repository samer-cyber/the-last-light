// The disc of gas falling into the black hole: hot near the hole, red and torn at its edge, turning,
// brighter on the side coming toward the viewer. On a flat ring whose uv.x runs around it and uv.y
// from the inner edge (0) to the outer (1).
Shader "Game/Accretion"
{
    Properties
    {
        [HDR] _Hot ("Hot", Color) = (3.0, 2.2, 1.5, 1)
        [HDR] _Cool ("Cool", Color) = (0.9, 0.18, 0.05, 1)
        _Intensity ("Intensity", Float) = 1
        _Speed ("Turning Speed", Float) = 0.03
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-30" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GameNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Hot;
                half4 _Cool;
                float _Intensity;
                float _Speed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 positionOS : TEXCOORD2; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float r = i.uv.y;
                float angle = i.uv.x * 6.2831853;
                // Inner parts turn faster.
                float turn = angle + _Time.y * _Speed * (1.0 + 2.5 * (1.0 - r));
                float2 swirl = float2(cos(turn), sin(turn)) * (1.0 + r * 3.0);
                float gas = GameFbm4(float3(swirl * 2.2, r * 6.0 - _Time.y * 0.02)) * 0.5 + 0.5;
                float streaks = GameNoise(float3(cos(turn) * 9.0, sin(turn) * 9.0, r * 30.0)) * 0.5 + 0.5;
                float density = smoothstep(0.0, 0.08, r) * smoothstep(1.0, 0.55, r) * (0.35 + 0.65 * gas) * (0.7 + 0.3 * streaks);
                half3 colour = lerp(_Hot.rgb, _Cool.rgb, smoothstep(0.0, 0.7, r));
                // The side turning toward the viewer is brighter.
                float3 tangent = normalize(TransformObjectToWorldDir(cross(float3(0, 1, 0), i.positionOS)));
                float toward = dot(tangent, normalize(GetCameraPositionWS() - i.positionWS));
                float doppler = 0.55 + 0.45 * toward;
                return half4(colour * density * doppler * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
