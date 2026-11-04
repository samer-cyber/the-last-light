// Frost grown over curved glass from the inside: it follows the glass all the way round (no flat panel), thicker at the
// bottom and where the glass turns away from the eye, crystalline, a little thinner where a face would be, so a sleeper is
// only a dim shape behind it. Lit like everything else (the pod's own light, the hall's cold air), seen from both sides.
Shader "Game/Frost"
{
    Properties
    {
        _Color ("Frost colour", Color) = (0.78, 0.86, 0.94, 1)
        _Density ("How thick the frost is", Range(0, 1)) = 0.8
        _Floor ("Height of the floor (thickest just above it)", Float) = 0
        _Face ("Height of a face (a little clearer there)", Float) = 1.6
        _Glow ("Light of its own", Float) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Frost"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "GameLit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Density;
                float _Floor;
                float _Face;
                float _Glow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 p = i.positionWS;
                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                float3 v = GetWorldSpaceNormalizeViewDir(p);
                // Ferns of rime: large soft patches, fine grain, and bright crystal edges.
                float patches = GameFbm3(p * 3.1) * 0.5 + 0.5;
                float grain = GameNoise(p * 22.0) * 0.5 + 0.5;
                float crystals = smoothstep(0.62, 0.9, GameNoise(p * 58.0 + float3(3.1, 0.0, 7.7)) * 0.5 + 0.5);
                float edge = pow(1.0 - saturate(abs(dot(n, v))), 1.6);
                float bottom = saturate(1.0 - (p.y - _Floor) / 1.25);
                float face = saturate(1.0 - abs(p.y - _Face) / 0.32);
                float a = _Density * (0.62 + 0.32 * patches + 0.1 * grain) + 0.4 * edge + 0.3 * bottom - 0.2 * face * (1.0 - edge);
                a = saturate(a);
                half3 albedo = _Color.rgb * (0.85 + 0.25 * grain) + crystals * 0.18;
                half4 lit = GameShade(p, n, i.positionCS, albedo, 0.0, 0.42, _Color.rgb * _Glow * (0.6 + 0.6 * patches), 1.0, i.fog);
                return half4(lit.rgb, a);
            }
            ENDHLSL
        }
    }
}
