Shader "FirstGame/Pilgrimage/FiniteCedar"
{
    Properties
    {
        _BaseMap("Preserved original cedar albedo", 2D) = "white" {}
        _BaseColor("Preserved cedar tint", Color) = (1,1,1,1)
        _Cutoff("Opaque depth cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    HLSLINCLUDE
    // All three passes use this exact layout. This opaque shader never clips
    // alpha, including in its own depth and geometric shadow passes.
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        float _Cutoff;
        float _Cull;
    CBUFFER_END

    struct CedarDepthAttributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct CedarDepthVaryings
    {
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };
    CedarDepthVaryings CedarDepthVertex(CedarDepthAttributes input)
    {
        CedarDepthVaryings output = (CedarDepthVaryings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        return output;
    }
    half CedarDepthFragment(CedarDepthVaryings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return input.positionCS.z;
    }
    ENDHLSL
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
            #pragma multi_compile_instancing
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            // This path deliberately reads no imported UV or tangent/normal
            // attributes. Positions already rendered correctly in the plain
            // finite probe. Explicit LOD avoids an implicit derivative lookup.
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fog : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 metres = float2(input.positionWS.x + input.positionWS.z * .37, input.positionWS.y);
                float2 uv = frac(metres * .62 * _BaseMap_ST.xy + _BaseMap_ST.zw);
                half3 wood = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 0).rgb;
                half3 color = saturate(wood * _BaseColor.rgb);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "FiniteCedarDepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CedarDepthVertex
            #pragma fragment CedarDepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "FiniteCedarShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CedarShadowVertex
            #pragma fragment CedarShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            // Set by URP ShadowUtils.SetupShadowCasterConstantBuffer.
            float3 _LightDirection;
            float3 _LightPosition;
            CedarDepthVaryings CedarShadowVertex(CedarDepthAttributes input)
            {
                CedarDepthVaryings output = (CedarDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                // Use the actual installed URP17.3 helper, including reversed-Z clamping.
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }
            half4 CedarShadowFragment(CedarDepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
