// A shooting star, drawn by a LineRenderer from its tail (u = 0) to its head (u = 1): a bright head
// and a tail that fades behind it. The line's colour alpha fades the whole streak.
Shader "Game/Streak"
{
    Properties
    {
        _Color ("Colour", Color) = (0.85, 0.9, 1.0, 1)
        _Intensity ("Intensity", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-140" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Streak"
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
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float across = (i.uv.y - 0.5) * 2.0;
                float tail = pow(saturate(i.uv.x), 2.5) * exp(-across * across * 4.0);
                float head = exp(-pow((1.0 - i.uv.x) * 14.0, 2.0)) * exp(-across * across * 2.0) * 2.0;
                return half4(_Color.rgb * (tail + head) * _Intensity * i.color.a, 1.0);
            }
            ENDHLSL
        }
    }
}
