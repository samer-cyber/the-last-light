// The mind's plan drawn in light before it builds: each piece a faint glass shape with bright
// edges and a glowing rim, lines of light drifting up through it. It draws itself in from the
// bottom (_Reveal 0 to 1; _RevealRange: the piece's lowest point and its height, in metres).
// _Wanting dims it to a slow flicker while the mind waits for the material it lacks.
Shader "Game/Ghost"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1.2, 0.95, 0.7, 1)
        _Intensity ("Intensity", Float) = 1
        _Fill ("Fill", Float) = 0.05
        _Rim ("Rim", Float) = 0.6
        _Edge ("Edges", Float) = 1.3
        _Lines ("Drifting Lines", Float) = 0.35
        _Reveal ("Reveal", Range(0, 1)) = 1
        _RevealRange ("Reveal Range (bottom, height)", Vector) = (0, 1, 0, 0)
        _Wanting ("Wanting", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Ghost"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GameNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Fill;
                float _Rim;
                float _Edge;
                float _Lines;
                float _Reveal;
                float4 _RevealRange;
                float _Wanting;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 scale : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float h = saturate((i.positionWS.y - _RevealRange.x) / max(_RevealRange.y, 0.01));
                float cut = _Reveal * 1.1 - (h * 0.7 + (GameNoise(i.positionWS * 3.1) * 0.5 + 0.5) * 0.3);
                clip(cut);
                float drawing = saturate(1.0 - cut / 0.05) * step(_Reveal, 0.999);

                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float rim = pow(1.0 - abs(dot(normalize(i.normalWS), view)), 2.5);

                float3 toFace = (0.5 - abs(i.positionOS)) * i.scale;
                float lo = min(toFace.x, min(toFace.y, toFace.z));
                float hi = max(toFace.x, max(toFace.y, toFace.z));
                float mid = toFace.x + toFace.y + toFace.z - lo - hi;
                float edge = 1.0 - smoothstep(0.0, 0.035, mid);

                float lines = smoothstep(0.86, 1.0, frac(i.positionWS.y * 5.0 - _Time.y * 0.7));
                float flicker = 0.88 + 0.12 * sin(_Time.y * 23.0 + i.positionWS.x * 3.0);
                float waiting = lerp(1.0, 0.45 + 0.25 * sin(_Time.y * 3.0), _Wanting);

                float a = (_Fill + rim * _Rim + edge * _Edge + lines * _Lines) * flicker * waiting + drawing * 2.5;
                half3 colour = _Color.rgb * a * _Intensity;
                colour = MixFogColor(colour, half3(0, 0, 0), i.fog);
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
