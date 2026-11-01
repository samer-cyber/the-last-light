// A glowing skin just outside the orb: a soft rim, drifting strands and, near the end, cracks.
Shader "Game/OrbShell"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.55, 1.0, 1)
        _Accent ("Accent", Color) = (1.0, 0.3, 0.85, 1)
        _Intensity ("Intensity", Float) = 1.4
        _Pulse ("Pulse", Range(0, 1)) = 0
        _Instability ("Instability", Range(0, 1)) = 0
        _Scale ("Scale", Float) = 2.6
        _Flow ("Flow", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OrbShell"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GameNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Accent;
                float _Intensity;
                float _Pulse;
                float _Instability;
                float _Scale;
                float _Flow;
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
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float facing = saturate(dot(normalize(i.normalWS), normalize(i.viewDirWS)));
                float rim = pow(1.0 - facing, 2.4);
                float t = _Time.y * _Flow;
                float3 p = i.positionOS * 2.0 * _Scale;

                float f = GameFbm4(p + float3(t, t * 1.3, -t));
                float strands = pow(saturate(1.0 - abs(f) * 3.0), 6.0);
                float crack = pow(saturate(1.0 - abs(GameNoise(p * 2.2 + t * 8.0))), 22.0) * _Instability;

                float glow = rim * (0.55 + strands * 1.6) + strands * 0.18 * facing + crack * 3.0;
                half3 col = lerp(_Color.rgb, _Accent.rgb, saturate(strands * 0.8 + crack));
                float energy = _Intensity * (1.0 + 2.0 * _Pulse + 1.5 * _Instability);
                return half4(col * glow * energy, 1.0);
            }
            ENDHLSL
        }
    }
}
