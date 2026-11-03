// A shaft of light: an additive cylinder, brightest where you look through most of it (its middle)
// and fading to nothing at its silhouette, so it reads as light in the air rather than a tube.
Shader "Game/Beam"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1, 0.75, 0.45, 1)
        _Intensity ("Intensity", Float) = 1
        _Softness ("Softness", Float) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float height : TEXCOORD2;
                float cap : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                output.height = input.positionOS.y;
                output.cap = abs(input.normalOS.y);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float facing = abs(dot(normalize(input.normalWS), normalize(input.viewWS)));
                float core = pow(saturate(facing), _Softness);
                // The cylinder's caps (top and bottom faces) add nothing.
                float cap = step(0.98, input.cap);
                float ends = smoothstep(1.0, 0.85, abs(input.height));
                return half4(_Color.rgb * _Intensity * core * ends * (1.0 - cap), 0);
            }
            ENDHLSL
        }
    }
}
