// A soft glow around the orb, on a quad that always faces the camera.
Shader "Game/Halo"
{
    Properties
    {
        _Color ("Color", Color) = (0.45, 0.4, 1.0, 1)
        _Intensity ("Intensity", Float) = 0.9
        _Edge ("Orb Edge (quad radius)", Float) = 0.26
        _Pulse ("Pulse", Range(0, 1)) = 0
        _Instability ("Instability", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Halo"
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
                float _Edge;
                float _Pulse;
                float _Instability;
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
                float3 centreVS = TransformWorldToView(TransformObjectToWorld(float3(0.0, 0.0, 0.0)));
                float scale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                o.positionCS = TransformWViewToHClip(centreVS + float3(v.positionOS.xy * scale, 0.0));
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv * 2.0 - 1.0);   // 0 at the centre, 1 at the quad's edge
                float outside = max(r - _Edge, 0.0);
                float glow = exp(-outside * 9.0) * 0.8 + exp(-outside * 3.0) * 0.25;
                glow *= smoothstep(1.0, 0.75, r);
                float energy = _Intensity * (1.0 + 1.6 * _Pulse + 1.0 * _Instability);
                return half4(_Color.rgb * glow * energy, 1.0);
            }
            ENDHLSL
        }
    }
}
