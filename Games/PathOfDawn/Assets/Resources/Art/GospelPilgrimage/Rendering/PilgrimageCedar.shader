Shader "FirstGame/Pilgrimage/FiniteCedar"
{
    Properties
    {
        _BaseMap("Preserved original cedar albedo", 2D) = "white" {}
        _BaseColor("Preserved cedar tint", Color) = (1,1,1,1)
        _Cutoff("Opaque depth cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "FiniteCedarForward"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END
            // This path deliberately reads no imported UV or tangent/normal
            // attributes. Positions already rendered correctly in the plain
            // finite probe. Explicit LOD avoids an implicit derivative lookup.
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half fog:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 metres = float2(input.positionWS.x + input.positionWS.z * .37, input.positionWS.y);
                float2 uv = frac(metres * .62 * _BaseMap_ST.xy + _BaseMap_ST.zw);
                half3 wood = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 0).rgb;
                half3 color = saturate(wood * _BaseColor.rgb);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        // Opaque depth and the original geometric shadow do not sample the
        // imported UVs unless alpha testing is enabled; this material is opaque.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    Fallback Off
}
