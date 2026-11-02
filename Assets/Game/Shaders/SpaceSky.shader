// The sky of the young solar system: near black, a faint band of the galaxy broken by dark lanes of
// dust, and the glow of the dust in the planets' plane toward the Sun. The stars are drawn apart
// (Stars.shader), so they can light up as the entity's answers.
Shader "Game/SpaceSky"
{
    Properties
    {
        _BandNormal ("Galaxy Plane Normal", Vector) = (0.35, 0.86, 0.36, 0)
        _BandColor ("Band Colour", Color) = (0.035, 0.03, 0.045, 1)
        _SunDirection ("Sun Direction", Vector) = (0, 0, 1, 0)
        _Zodiacal ("Zodiacal Light", Float) = 0.03
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GameNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BandNormal;
                half4 _BandColor;
                float4 _SunDirection;
                float _Zodiacal;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.direction = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float lat = dot(d, normalize(_BandNormal.xyz));
                float band = exp(-lat * lat / 0.02);
                float wide = exp(-lat * lat / 0.15);
                float clouds = 0.5 + 0.5 * GameFbm4(d * 4.0);
                float lanes = smoothstep(0.0, 0.3, GameFbm3(d * 7.0 + 3.1)) * exp(-lat * lat / 0.006);

                half3 colour = half3(0.0011, 0.0011, 0.002);
                colour += _BandColor.rgb * (band * clouds * (1.0 - 0.8 * lanes) + 0.25 * wide);

                // Dust in the plane of the planets (the XZ plane), lit by the Sun.
                float sun = saturate(dot(d, normalize(_SunDirection.xyz)));
                float plane = exp(-d.y * d.y / 0.012);
                colour += half3(1.0, 0.82, 0.58) * _Zodiacal * pow(sun, 5.0) * plane;
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
}
