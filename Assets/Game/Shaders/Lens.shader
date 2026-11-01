// Space bent around the orb. A quad that always faces the camera draws the sky as it would be
// seen past a point mass: each view ray turns toward the centre by thetaE^2 / theta, so the void
// behind the orb wraps into a ring around it. Drawn after the sky and before anything see-through.
Shader "Game/Lens"
{
    Properties
    {
        _Einstein ("Ring Radius (orb radii)", Float) = 1.3
        _OrbRadius ("Orb Radius (metres)", Float) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Lens"
            Tags { "LightMode" = "UniversalForward" }
            Blend Off
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "VoidSky.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Einstein;
                float _OrbRadius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float halfSize : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centreVS = TransformWorldToView(TransformObjectToWorld(float3(0.0, 0.0, 0.0)));
                float scale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 positionVS = centreVS + float3(v.positionOS.xy * scale, 0.0);
                o.positionCS = TransformWViewToHClip(positionVS);
                o.positionWS = mul(UNITY_MATRIX_I_V, float4(positionVS, 1.0)).xyz;
                o.halfSize = scale * 0.5;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 camera = GetCameraPositionWS();
                float3 ray = normalize(i.positionWS - camera);
                float3 toCentre = UNITY_MATRIX_M._m03_m13_m23 - camera;
                float distance = length(toCentre);
                float3 centre = toCentre / distance;

                float cosTheta = clamp(dot(ray, centre), -1.0, 1.0);
                float theta = acos(cosTheta);
                float thetaRing = _Einstein * asin(saturate(_OrbRadius / distance));
                float thetaEdge = atan(i.halfSize / distance);

                // Full strength near the orb, none at the quad's edge, so it meets the sky unseen.
                float bend = thetaRing * thetaRing / max(theta, 1e-4) * smoothstep(thetaEdge, thetaEdge * 0.55, theta);

                float3 inward = centre - cosTheta * ray;
                float inwardLength = length(inward);
                inward = inwardLength > 1e-5 ? inward / inwardLength : float3(0.0, 0.0, 0.0);
                float3 bent = ray * cos(bend) + inward * sin(bend);
                return half4(VoidSky(bent), 1.0);
            }
            ENDHLSL
        }
    }
}
