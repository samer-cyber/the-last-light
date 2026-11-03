// Spark's hologram (Hologram.cs): a picture from its archive, projected as light. Additive, so black
// is empty air; tinted toward cyan, with scan lines that drift up, a slow shimmer, a band of glitch
// that passes now and then, and edges that fade. _Reveal (0..1) builds it from the bottom up as it
// appears and takes it away as it goes; _Flicker shakes its brightness.
Shader "Game/Hologram"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.45, 0.9, 1, 1)
        _Colour ("Own colour kept", Range(0, 1)) = 0.35
        _Intensity ("Intensity", Float) = 1.6
        _Reveal ("Reveal", Range(0, 1)) = 1
        _Flicker ("Flicker", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                float _Colour;
                float _Intensity;
                float _Reveal;
                float _Flicker;
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

            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                // A band of glitch now and then: that slice slides sideways.
                float band = floor(t * 1.7);
                float bandY = Hash(band * 7.13);
                float inBand = step(abs(uv.y - bandY), 0.025) * step(0.6, Hash(band * 3.7));
                uv.x += inBand * (Hash(band + floor(t * 30.0)) - 0.5) * 0.06;
                // Shimmer.
                uv.x += sin(uv.y * 90.0 + t * 6.0) * 0.0012;
                half3 picture = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                half lum = dot(picture, half3(0.3, 0.55, 0.15));
                half3 colour = lerp(_Tint.rgb * lum * 1.4, picture, _Colour);
                // Scan lines drifting up, and a brighter sweep.
                float lines = 0.72 + 0.28 * sin((uv.y * 260.0 - t * 4.0) * 3.14159);
                float sweep = 1.0 + 0.6 * exp(-pow(frac(uv.y - t * 0.25) * 8.0 - 4.0, 2.0));
                // Edges fade; it builds from the bottom up as it appears.
                float2 edge = min(i.uv, 1.0 - i.uv);
                float fade = saturate(min(edge.x, edge.y) * 14.0);
                float reveal = smoothstep(_Reveal * 1.08 - 0.08, _Reveal * 1.08, 1.0 - i.uv.y);
                reveal = 1.0 - reveal;
                float front = exp(-pow((1.0 - i.uv.y - _Reveal) * 30.0, 2.0)) * step(_Reveal, 0.999);
                float flicker = 1.0 - _Flicker * (0.5 + 0.5 * sin(t * 61.0)) * Hash(floor(t * 24.0));
                half3 c = colour * lines * sweep * fade * reveal * flicker * _Intensity;
                c += _Tint.rgb * front * 2.5 * fade;
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
