// Original presentation shader for the new original Radiance earth-bank painting.
// Vertex tint and UV repetition are explicit: Mesh2D-Unlit-Default ignores vertex colors.
Shader "FirstGame/Radiance Painted Ground 2D"
{
    Properties
    {
        _MainTex("Original painted earth bank", 2D) = "white" {}
        _Tint("Ground tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex GroundVertex
            #pragma fragment GroundFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
            CBUFFER_END
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            Varyings GroundVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Tint;
                return output;
            }
            half4 GroundFragment(Varyings input) : SV_Target
            {
                // Repeat through UVs, never by changing the shared source texture/importer.
                half3 painting = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    float2(frac(input.uv.x), saturate(input.uv.y))).rgb;
                // Preserve the authored soil, embedded roots and rock color. The
                // actual vertex tint only eases the lower strata into deep navy.
                return half4(painting * input.color.rgb, input.color.a);
            }
            ENDHLSL
        }
    }
}
