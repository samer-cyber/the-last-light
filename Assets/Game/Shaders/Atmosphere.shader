// The air around a planet, drawn on a shell a little larger than it. Each pixel takes how close its
// line of sight passes to the planet: beyond the limb the glow thins out with height, over the disc
// it is a faint haze. Brightest where sunlight grazes it, and toward the Sun (the ring of light
// around a planet seen against the Sun). Auroras ring the poles when the planet has a magnetic field.
Shader "Game/Atmosphere"
{
    Properties
    {
        _Haze ("Haze", Color) = (0.35, 0.55, 1.0, 1)
        _Thickness ("Thickness", Range(0, 4)) = 1
        _Planet ("Planet Radius (share of the shell's)", Float) = 0.92
        _Aurora ("Aurora", Range(0, 1)) = 0
        _AuroraColor ("Aurora Colour", Color) = (0.25, 1.0, 0.55, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Atmosphere"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GameNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Haze;
                float _Thickness;
                float _Planet;
                float _Aurora;
                half4 _AuroraColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 centre = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float shell = 0.5 * length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float radius = shell * _Planet;
                float height = (shell - radius) * 0.3;

                float3 eye = GetCameraPositionWS();
                float3 ray = normalize(i.positionWS - eye);
                float3 toCentre = centre - eye;
                float along = dot(toCentre, ray);
                float miss = sqrt(max(dot(toCentre, toCentre) - along * along, 0.0));
                float3 closest = eye + ray * along;
                float3 up = normalize(closest - centre);

                bool overDisc = miss < radius;
                float density = overDisc ? 0.12 + 0.5 * pow(miss / radius, 8.0) : exp(-(miss - radius) / height);

                Light sun = GetMainLight();
                // Over the disc the air is lit where the ground below it is; light from behind the
                // planet only rims its edge, since the planet itself blocks it everywhere else.
                float3 below = overDisc ? normalize(i.positionWS - centre) : up;
                float day = saturate(dot(below, sun.direction) * 0.9 + 0.3);
                float towardSun = overDisc ? 0.0 : pow(saturate(dot(ray, sun.direction)), 10.0) * 2.5;
                half3 colour = _Haze.rgb * density * (day + towardSun) * _Thickness * sun.color;

                // Auroras: ovals of light around each pole, in curtains that drift, seen best on the night side.
                float3 axis = normalize(TransformObjectToWorldDir(float3(0.0, 1.0, 0.0)));
                float lat = abs(dot(below, axis));
                float ring = smoothstep(0.78, 0.85, lat) * smoothstep(0.96, 0.9, lat);
                float curtains = saturate(GameNoise(below * 7.0 + float3(0.0, _Time.y * 0.25, 0.0)) * 1.2 + 0.45);
                float aurora = _Aurora * ring * curtains * (1.0 - 0.7 * day) * (overDisc ? 0.8 : exp(-(miss - radius) / (height * 1.5)));
                colour += _AuroraColor.rgb * aurora;
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
}
