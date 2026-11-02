// The frozen ground: wind-packed snow on the level, dark banded rock where it is steep, and old ice
// where the mesh's vertex colour (red) says a lake froze. No textures: all of it is noise in world
// space, so nothing repeats and nothing stretches on the cliffs.
Shader "Game/Ground"
{
    Properties
    {
        _Snow ("Snow", Color) = (0.66, 0.7, 0.78, 1)
        _SnowShade ("Snow Shade", Color) = (0.5, 0.55, 0.66, 1)
        _Rock ("Rock", Color) = (0.085, 0.085, 0.095, 1)
        _RockBand ("Rock Band", Color) = (0.17, 0.16, 0.16, 1)
        _Ice ("Ice", Color) = (0.2, 0.33, 0.42, 1)
        _WindDirection ("Wind Direction (xz)", Vector) = (0.8, 0, 0.6, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Snow;
            half4 _SnowShade;
            half4 _Rock;
            half4 _RockBand;
            half4 _Ice;
            float4 _WindDirection;
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
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 p = i.positionWS;
                float3 baseNormal = normalize(i.normalWS);
                float large = GameFbm3(p * 0.045);
                float fine = GameFbm3(p * 0.9);

                // Snow lies where it is level enough, a little higher or lower by noise.
                float snow = smoothstep(0.58, 0.8, baseNormal.y + large * 0.18 + fine * 0.06);
                float ice = saturate(i.color.r * 1.2 - 0.1) * snow;

                // Rock: dark, in bands that wander.
                float bands = sin(p.y * 1.7 + GameFbm3(p * 0.12) * 6.0) * 0.5 + 0.5;
                half3 rock = lerp(_Rock.rgb, _RockBand.rgb, smoothstep(0.55, 0.9, bands) * (0.6 + 0.4 * fine));

                // Snow: drifts lined up across the wind.
                float2 wind = normalize(_WindDirection.xz);
                float ripple = sin(dot(p.xz, wind) * 2.4 + GameNoise(p * 0.3) * 4.0) * 0.5 + 0.5;
                half3 snowColour = lerp(_SnowShade.rgb, _Snow.rgb, saturate(0.55 + large * 0.6 + ripple * 0.15));

                // Ice: deep and clear, with pale cracks.
                float crack = 1.0 - smoothstep(0.0, 0.035, abs(GameNoise(p * 0.55)));
                half3 iceColour = lerp(_Ice.rgb * (0.8 + 0.4 * fine), half3(0.55, 0.68, 0.78), crack * 0.6);

                half3 albedo = lerp(rock, snowColour, snow);
                albedo = lerp(albedo, iceColour, ice);
                half smoothness = lerp(lerp(0.18, 0.32, snow), 0.88, ice);

                float3 normalWS = GameBumped(baseNormal, p, lerp(0.5, 1.4, snow), lerp(0.6, 0.08, snow));
                normalWS = lerp(normalWS, baseNormal, ice * 0.8);
                return GameShade(p, normalWS, i.positionCS, albedo, 0.0, smoothness, half3(0, 0, 0), 1.0, i.fog);
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

            float4 Vert(Attributes v) : SV_POSITION { return GameShadowPosition(v.positionOS.xyz, v.normalOS); }
            half4 Frag() : SV_Target { return 0; }
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

            struct Attributes { float4 positionOS : POSITION; };

            float4 Vert(Attributes v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half4 Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }
    }
}
