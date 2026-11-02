// The stars: one small quad each, turned to face the camera in the vertex shader and kept at a fixed
// distance around it, so they never come nearer. All four vertices of a quad carry its star's
// direction; uv is the corner (-1..1), uv2 the size in degrees and the brightness, and the colour
// the star's colour with a phase for its twinkle in alpha. While the sky answers (SkyAnswer.cs),
// the stars behind the answer dim, so its figure stands out.
Shader "Game/Stars"
{
    Properties
    {
        _Distance ("Distance", Float) = 5500
        _Intensity ("Intensity", Float) = 1.5
        _Twinkle ("Twinkle", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-200" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Stars"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Distance;
                float _Intensity;
                float _Twinkle;
            CBUFFER_END

            float4 _GlyphDirection;   // xyz: where the answer is; w: cosine of its angular radius
            float _GlyphDim;          // 0..1 while the answer shows
            float _StarGather;        // 0..1 while the entity thinks: the stars shimmer

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
                half4 color : COLOR;
                float bright : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 dir = normalize(v.positionOS.xyz);
                float3 centreVS = TransformWorldToView(_WorldSpaceCameraPos + dir * _Distance);
                float size = radians(v.uv2.x) * _Distance;
                o.positionCS = TransformWViewToHClip(centreVS + float3(v.uv * size, 0.0));
                o.uv = v.uv;
                o.color = v.color;
                float behind = smoothstep(_GlyphDirection.w - 0.01, _GlyphDirection.w + 0.01, dot(dir, _GlyphDirection.xyz));
                o.bright = v.uv2.y * (1.0 - 0.8 * _GlyphDim * behind);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r2 = dot(i.uv, i.uv);
                float shape = (exp(-r2 * 10.0) + 0.2 * exp(-r2 * 3.0)) * saturate(1.0 - r2);
                float twinkle = 1.0 + _Twinkle * sin(_Time.y * (1.1 + 2.7 * i.color.a) + i.color.a * 37.0);
                twinkle *= 1.0 + _StarGather * (0.6 + 0.6 * sin(_Time.y * 6.0 + i.color.a * 60.0));
                return half4(i.color.rgb * shape * i.bright * twinkle * _Intensity, 1.0);
            }
            ENDHLSL
        }
    }
}
