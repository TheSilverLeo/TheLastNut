Shader "The Last Rivet/Nut Highlight"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.45, 0.08, 0.16)
        _SurfaceOffset ("Surface Offset", Float) = 0.0004
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "NutHighlight"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            ColorMask RGB
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _SurfaceOffset;
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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float3 toCamera = GetCameraPositionWS() - world;
                float distanceSqr = dot(toCamera, toCamera);
                float3 view = distanceSqr > 1e-8 ? toCamera * rsqrt(distanceSqr) : float3(0, 0, 1);
                world += view * _SurfaceOffset;
                output.positionCS = TransformWorldToHClip(world);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = view;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = _BaseColor.a;
                float normalSqr = dot(input.normalWS, input.normalWS);
                if (normalSqr > 1e-6)
                {
                    float facing = saturate(dot(normalize(input.normalWS), normalize(input.viewWS)));
                    float rim = pow(1.0 - facing, 3.0);
                    alpha = lerp(alpha, min(alpha * 2.0, 0.32), rim);
                }

                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
