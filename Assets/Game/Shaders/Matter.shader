// What the mind makes things from, and the loose pieces of it lying about: stone, ice, metal and
// light. Lit like everything else in the frozen world, with colour that wanders by noise, worn
// lighter edges on blocks, a rim that glows (ice) and light of its own (light). While a piece is
// being made it appears from the bottom up through noise, with a glowing seam where it is forming
// (_Build 0 to 1; _BuildRange: the piece's lowest point and its height, in world metres).
Shader "Game/Matter"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.42, 0.42, 0.44, 1)
        _Variation ("Colour Variation", Color) = (0.3, 0.3, 0.33, 1)
        _NoiseScale ("Noise Scale", Float) = 1.6
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Bump ("Bump", Range(0, 1)) = 0.25
        _EdgeWear ("Edge Wear (blocks)", Range(0, 1)) = 0.6
        _EdgeColor ("Edge Colour", Color) = (0.62, 0.62, 0.64, 1)
        [HDR] _Glow ("Rim Glow", Color) = (0, 0, 0, 1)
        [HDR] _Emission ("Emission", Color) = (0, 0, 0, 1)
        _Build ("Build", Range(0, 1)) = 1
        _BuildRange ("Build Range (bottom, height)", Vector) = (0, 1, 0, 0)
        [HDR] _BuildEdge ("Build Seam", Color) = (3, 2.2, 1.4, 1)
        _Seam ("Glowing Joints (made things)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _Variation;
            float _NoiseScale;
            half _Smoothness;
            half _Metallic;
            half _Bump;
            half _EdgeWear;
            half4 _EdgeColor;
            half4 _Glow;
            half4 _Emission;
            float _Build;
            float4 _BuildRange;
            half4 _BuildEdge;
            half _Seam;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

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
                float3 positionOS : TEXCOORD2;
                float3 scale : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // Forming: from the bottom up, through noise, with a hot seam.
                float h = saturate((i.positionWS.y - _BuildRange.x) / max(_BuildRange.y, 0.01));
                float t = h * 0.75 + (GameNoise(i.positionWS * 2.3) * 0.5 + 0.5) * 0.25;
                float cut = _Build * 1.08 - t;
                clip(cut);
                float seam = saturate(1.0 - cut / 0.06) * step(_Build, 0.999);

                float n = GameFbm3(i.positionWS * _NoiseScale) * 0.5 + 0.5;
                half3 albedo = lerp(_BaseColor.rgb, _Variation.rgb, saturate(n * 1.4 - 0.2));

                // Worn edges on blocks: near where two faces meet (the second-nearest face is close).
                float3 toFace = (0.5 - abs(i.positionOS)) * i.scale;
                float lo = min(toFace.x, min(toFace.y, toFace.z));
                float hi = max(toFace.x, max(toFace.y, toFace.z));
                float mid = toFace.x + toFace.y + toFace.z - lo - hi;
                float edge = (1.0 - smoothstep(0.0, 0.07, mid)) * _EdgeWear;
                albedo = lerp(albedo, _EdgeColor.rgb, edge * (0.6 + 0.4 * n));

                float3 normalWS = GameBumped(normalize(i.normalWS), i.positionWS, _NoiseScale * 2.0, _Bump * 0.6);
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float rim = pow(1.0 - saturate(dot(normalWS, view)), 3.0);
                // Things the mind made keep a faint warm light in their joints.
                float joint = (1.0 - smoothstep(0.0, 0.025, mid)) * _Seam;
                half3 emission = _Emission.rgb * (0.75 + 0.25 * n) + _Glow.rgb * rim + _BuildEdge.rgb * (seam + joint * 0.08);
                return GameShade(i.positionWS, normalWS, i.positionCS, albedo, _Metallic, _Smoothness * (0.85 + 0.3 * n), emission, 1.0 - edge * 0.2, i.fog);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define GAME_SHADOW_CASTER
            #include "GameLit.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = GameShadowPosition(v.positionOS.xyz, v.normalOS);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float h = saturate((i.positionWS.y - _BuildRange.x) / max(_BuildRange.y, 0.01));
                clip(_Build * 1.08 - (h * 0.75 + (GameNoise(i.positionWS * 2.3) * 0.5 + 0.5) * 0.25));
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "GameNoise.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float h = saturate((i.positionWS.y - _BuildRange.x) / max(_BuildRange.y, 0.01));
                clip(_Build * 1.08 - (h * 0.75 + (GameNoise(i.positionWS * 2.3) * 0.5 + 0.5) * 0.25));
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }
}
