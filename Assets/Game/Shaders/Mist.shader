// Mist that hides what is behind it: soft puffs (or one wide sheet) blended over the scene, faded where they meet
// the ground or rock (so they never show a hard edge) and near the camera, taking the scene's fog with distance.
Shader "Game/Mist"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Colour", Color) = (0.12, 0.09, 0.1, 1)
        _Softness ("Soft edge where it meets things (m)", Float) = 3
        _NearFade ("Fade near the camera (m)", Float) = 4
        _Drift ("Drift of the sheet's texture (xy, zw)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Mist"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _Softness;
                float _NearFade;
                float4 _Drift;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screen : TEXCOORD1;
                float eye : TEXCOORD2;
                float fog : TEXCOORD3;
                float2 uv2 : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.screen = ComputeScreenPos(p.positionCS);
                o.eye = -p.positionVS.z;
                o.color = v.color;
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv = uv + _Drift.xy * _Time.y;
                o.uv2 = uv * 0.37 + _Drift.zw * _Time.y;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half shape = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                // A second, slower layer for a sheet, so it never looks like one repeated picture.
                half second = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv2).a;
                shape = lerp(shape, shape * (0.55 + 0.9 * second), step(0.0001, dot(_Drift, _Drift)));
                float2 uv = i.screen.xy / i.screen.w;
                float scene = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                half soft = saturate((scene - i.eye) / max(_Softness, 0.01));
                half nearFade = saturate((i.eye - 0.3) / max(_NearFade, 0.01));
                half a = saturate(shape * i.color.a * _Color.a * soft * nearFade);
                half3 colour = MixFog(_Color.rgb * i.color.rgb, i.fog);
                return half4(colour, a);
            }
            ENDHLSL
        }
    }
}
