// The young Sun's surface: boiling granulation, dark spots (it was more active than today), and a
// limb that darkens toward the edge. Bright enough to bloom; its corona is Corona.shader.
Shader "Game/Sun"
{
    Properties
    {
        _Seed ("Seed", Vector) = (3, 1, 7, 0)
        _Deep ("Deep", Color) = (1.0, 0.5, 0.18, 1)
        _Bright ("Bright", Color) = (1.0, 0.92, 0.76, 1)
        _Spots ("Spots", Range(0, 1)) = 0.6
        _Intensity ("Intensity", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Sun"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GameNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Seed;
                half4 _Deep;
                half4 _Bright;
                float _Spots;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 local : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.local = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.local);
                float t = _Time.y;
                float granules = GameFbm4(n * 26.0 + float3(0.0, t * 0.02, t * 0.01)) * 0.5 + 0.5;
                float spots = smoothstep(0.5, 0.64, GameFbm3(n * 2.4 + _Seed.xyz) * 0.5 + 0.5) * _Spots;
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                float mu = saturate(dot(normalize(i.normalWS), V));
                float limb = 0.3 + 0.7 * pow(mu, 0.45);
                half3 colour = lerp(_Deep.rgb, _Bright.rgb, granules) * limb * (1.0 - 0.7 * spots);
                return half4(colour * _Intensity, 1.0);
            }
            ENDHLSL
        }
    }
}
