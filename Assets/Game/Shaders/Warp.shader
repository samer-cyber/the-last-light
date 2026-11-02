// Space bent around the orb, as a full-screen pass before post-processing. Each pixel shows what
// lies thetaE^2 / theta closer to the orb, as past a point mass, so the dust and the void wrap into a
// ring around it; inside that ring the image turns over. On top: a slow swirl, ripples sent out with
// each symbol, and light split into colours where the bending is strongest. The orb's own core, and
// anything close to the camera (the notebook, the hands), are left as they are.
// SpaceWarp.cs sets the globals every frame.
Shader "Game/Warp"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Warp"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _WarpCenter;     // xy: orb centre in the viewport (0..1); z: strength 0..1; w: aspect
            float4 _WarpShape;      // x: core radius, y: ring radius (viewport heights); z: swirl (radians); w: colour split
            float4 _WarpRipples;    // radii of up to four ripples (viewport heights; 0 = none)
            float4 _WarpRippleAmp;  // their strength
            float _WarpNear;        // nothing nearer than this (metres) is bent

            // Where to look for the colour seen at offset d from the centre (viewport heights), with the
            // bending scaled by k (for the colour split). z: how far the bent ray passes from the core's
            // edge, in core radii; near zero is the ring where the image turns over, drawn dark.
            float3 Source(float2 d, float r, float k)
            {
                float core = _WarpShape.x, ring = _WarpShape.y;
                float protect = smoothstep(core * 0.9, core * 2.4, r);
                float bend = ring * ring / max(r, core) * protect * _WarpCenter.z * k;

                float ripple = 0.0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float at = _WarpRipples[i];
                    float x = (r - at) / (0.012 + 0.12 * at);
                    ripple += at > 0.0 ? _WarpRippleAmp[i] * x * exp(-x * x) : 0.0;
                }

                // Only the turned-over side is limited: far from the orb the image must stay where it is,
                // even when the orb's centre lies many screens away (seen from the side).
                float rs = max(r - bend + ripple * protect, -0.75);
                // Never take colour from inside the core: its own light does not bend around it.
                float clear = core * 1.35;
                float passes = abs(rs) / max(clear, 1e-5);
                rs = rs >= 0.0 ? max(rs, min(r, clear)) : min(rs, -clear);
                float swirl = _WarpShape.z * protect * _WarpCenter.z * ring / max(r, ring * 0.5);
                float s = sin(swirl), c = cos(swirl);
                float2 dir = r > 1e-6 ? d / r : float2(0.0, 0.0);
                dir = float2(dir.x * c - dir.y * s, dir.x * s + dir.y * c);
                return float3(dir * rs, r < clear ? 10.0 : passes);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_WarpCenter.z <= 0.0) return original;
                if (LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams) < _WarpNear) return original;

                float2 toView = float2(1.0 / _WarpCenter.w, 1.0);
                float2 d = (uv - _WarpCenter.xy) * float2(_WarpCenter.w, 1.0);
                float r = length(d);

                float3 g = Source(d, r, 1.0);
                float2 uvG = _WarpCenter.xy + g.xy * toView;
                // Do not pull the notebook or the hands into the bent image.
                if (LinearEyeDepth(SampleSceneDepth(uvG), _ZBufferParams) < _WarpNear) return original;
                // Less colour split where the image turns over, where it would only stripe.
                float split = _WarpShape.w * saturate(g.z - 0.5);
                float2 uvR = _WarpCenter.xy + Source(d, r, 1.0 - split).xy * toView;
                float2 uvB = _WarpCenter.xy + Source(d, r, 1.0 + split).xy * toView;

                half3 colour;
                colour.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvR).r;
                colour.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvG).g;
                colour.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvB).b;
                // The shadow ring: light that would pass through the core never arrives.
                colour *= lerp(0.08, 1.0, smoothstep(0.35, 1.6, g.z));
                return half4(colour, original.a);
            }
            ENDHLSL
        }
    }
}
