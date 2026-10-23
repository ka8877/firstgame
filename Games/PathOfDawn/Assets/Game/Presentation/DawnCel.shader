Shader "PathOfDawn/Cel Lit"
{
    Properties
    {
        [MainColor] _BaseColor("Base color", Color) = (1,1,1,1)
        _ShadeColor("Cool shadow tint", Color) = (.51,.57,.72,1)
        _RimColor("Warm rim tint", Color) = (1,.86,.59,1)
        _InkColor("Silhouette ink", Color) = (.12,.17,.25,1)
        _ShadeThreshold("Shadow band", Range(0,1)) = .28
        _LightThreshold("Light band", Range(0,1)) = .68
        _BandSoftness("Band antialiasing", Range(.001,.1)) = .018
        _RimStrength("Rim strength", Range(0,.5)) = .16
        _InkStrength("Silhouette ink strength", Range(0,1)) = .4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadeColor;
            half4 _RimColor;
            half4 _InkColor;
            half _ShadeThreshold;
            half _LightThreshold;
            half _BandSoftness;
            half _RimStrength;
            half _InkStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "CelForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex CelVertex
            #pragma fragment CelFragment
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct CelAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct CelVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            CelVaryings CelVertex(CelAttributes input)
            {
                CelVaryings output = (CelVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 CelFragment(CelVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half light = saturate(dot(normal, sun.direction)) * sun.shadowAttenuation;
                // Three deliberate value bands, with only a narrow edge for stable antialiasing.
                half middle = smoothstep(_ShadeThreshold-_BandSoftness, _ShadeThreshold+_BandSoftness, light);
                half bright = smoothstep(_LightThreshold-_BandSoftness, _LightThreshold+_BandSoftness, light);
                half3 tone = lerp(_ShadeColor.rgb, half3(.83,.85,.9), middle);
                tone = lerp(tone, half3(1.08,1.04,.96), bright);
                half3 color = _BaseColor.rgb * tone * lerp(half3(1,1,1), sun.color, .14);
                half edge = 1-saturate(dot(normal,view));
                half rim = smoothstep(.73,.78,edge) * middle * _RimStrength;
                color += _RimColor.rgb * rim;
                color = lerp(color, _InkColor.rgb, smoothstep(.95,.99,edge) * _InkStrength);
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
