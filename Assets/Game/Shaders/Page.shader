// The notebook's page: the notes, drawn by UI Toolkit into a render texture, shown on the page
// quad. Unlit, so it stays readable in the dark, but brightened by the orb's light (_Light) and a
// little darker toward the edges and the spiral, as paper held in one hand would be.
Shader "Game/Page"
{
    Properties
    {
        _MainTex ("Notes", 2D) = "white" {}
        _Light ("Light", Range(0, 2)) = 0.8
        _Tint ("Light Colour", Color) = (0.92, 0.9, 1.0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Page"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Light;
                half4 _Tint;
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

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 notes = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                float2 fromCentre = abs(i.uv - 0.5) * 2.0;
                float edge = 1.0 - 0.18 * pow(saturate(max(fromCentre.x, fromCentre.y)), 3.0);
                float spiral = 1.0 - 0.25 * smoothstep(0.93, 1.0, i.uv.y);
                return half4(notes * _Tint.rgb * (_Light * edge * spiral), 1.0);
            }
            ENDHLSL
        }
    }
}
