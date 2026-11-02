// The sky's answer (SkyAnswer.cs): the stars of a 1 or a 0 light one after another, as if drawn, and
// faint lines join them like a constellation. uv: a star's corner (-1..1), or for a line its length
// (x, 0..1) and width (y, -1..1). uv2: when it appears in the drawing (0..1), and 0 for a star or 1
// for a line. Colour alpha: brightness. _Reveal runs the drawing; _Fade lets it go.
Shader "Game/Glyph"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.9, 0.7, 1)
        _Reveal ("Reveal", Float) = 0
        _Fade ("Fade", Range(0, 1)) = 1
        _Intensity ("Intensity", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-150" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Glyph"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Reveal;
                float _Fade;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float order : TEXCOORD1;
                float kind : TEXCOORD2;
                float bright : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.order = v.uv2.x;
                o.kind = v.uv2.y;
                o.bright = v.color.a;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float since = _Reveal - i.order;
                if (since <= 0.0) return half4(0.0, 0.0, 0.0, 0.0);
                float shape;
                if (i.kind < 0.5)
                {
                    float r2 = dot(i.uv, i.uv);
                    shape = (exp(-r2 * 9.0) + 0.35 * exp(-r2 * 2.2)) * saturate(1.0 - r2);
                    shape *= 1.0 + 2.5 * exp(-since * 14.0);
                }
                else
                {
                    float drawn = saturate(since * 12.0);
                    shape = exp(-i.uv.y * i.uv.y * 5.0) * step(i.uv.x, drawn) * 0.22;
                }
                float twinkle = 0.9 + 0.1 * sin(_Time.y * 3.0 + i.order * 40.0);
                return half4(_Color.rgb * shape * i.bright * twinkle * _Intensity * _Fade, 1.0);
            }
            ENDHLSL
        }
    }
}
