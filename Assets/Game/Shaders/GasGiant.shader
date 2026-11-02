// A gas giant: bands of cloud stirred by turbulence, lit by the Sun (the main light).
Shader "Game/GasGiant"
{
    Properties
    {
        _Seed ("Seed", Vector) = (0, 0, 0, 0)
        _Light ("Light Bands", Color) = (0.86, 0.78, 0.64, 1)
        _Dark ("Dark Bands", Color) = (0.52, 0.36, 0.24, 1)
        _Bands ("Bands", Float) = 16
        _Ambient ("Ambient", Float) = 0.01
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GasGiant"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GameNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Seed;
                half4 _Light;
                half4 _Dark;
                float _Bands;
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
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.local = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.local);
                float stir = GameFbm3(n * float3(3.0, 9.0, 3.0) + _Seed.xyz + float3(_Time.y * 0.01, 0.0, 0.0));
                float bands = sin(n.y * _Bands + stir * 2.2) * 0.5 + 0.5;
                half3 albedo = lerp(_Dark.rgb, _Light.rgb, bands);
                Light sun = GetMainLight();
                float lit = saturate(dot(normalize(i.normalWS), sun.direction));
                return half4(albedo * (sun.color * lit + _Ambient), 1.0);
            }
            ENDHLSL
        }
    }
}
