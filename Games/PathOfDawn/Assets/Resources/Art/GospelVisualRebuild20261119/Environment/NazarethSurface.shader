Shader "FirstGame/Pilgrimage/NazarethSurface"
{
    Properties
    {
        _BaseMap("Surface colour", 2D) = "white" {}
        _BaseColor("Surface tint", Color) = (1,1,1,1)
        [Normal] _BumpMap("Surface relief", 2D) = "bump" {}
        _BumpScale("Relief strength", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BumpMap_ST;
        float4 _BaseColor;
        float _BumpScale;
        float _Smoothness;
        float _Cull;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    // The old PBR path produced invalid HDR on the flat pavers and on arch
    // insets. Keep a defined basis even on thin, nonuniformly scaled FBX parts.
    float3 SurfaceDirection(float3 value, float3 fallback)
    {
        float squared = dot(value, value);
        return squared > 0.000001 && squared < 1e12 ? value * rsqrt(squared) : fallback;
    }

    struct DepthVaryings
    {
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    DepthVaryings DepthVertex(Attributes input)
    {
        UNITY_SETUP_INSTANCE_ID(input);
        DepthVaryings output = (DepthVaryings)0;
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        return output;
    }

    half4 DepthFragment(DepthVaryings input) : SV_Target { return 0; }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        ZWrite On

        Pass
        {
            Name "NazarethForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_local_fragment _ _NORMALMAP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 screenShadow : TEXCOORD3;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = SurfaceDirection(TransformObjectToWorldNormal(input.normalOS, false), float3(0,1,0));
                output.fog = ComputeFogFactor(output.positionCS.z);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    output.screenShadow = ComputeScreenPos(output.positionCS);
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = SurfaceDirection(input.normalWS, float3(0,1,0));
                float3 axes = abs(normal);
                bool horizontal = axes.y >= max(axes.x, axes.z);
                float3 u = !horizontal && axes.x >= axes.z ? float3(0,0,1) : float3(1,0,0);
                float3 v = horizontal ? float3(0,0,1) : float3(0,1,0);
                float2 uv = float2(dot(input.positionWS, u), dot(input.positionWS, v)) * 0.48;
                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv, _BaseMap)).rgb * _BaseColor.rgb;
                #if defined(_NORMALMAP)
                    float3 tangent = SurfaceDirection(u - normal * dot(normal, u), u);
                    float3 bitangent = SurfaceDirection(cross(normal, tangent), v);
                    bitangent *= dot(bitangent, v) < 0 ? -1 : 1;
                    float3 relief = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap,
                        TRANSFORM_TEX(uv, _BumpMap)), saturate(_BumpScale));
                    normal = SurfaceDirection(normal * relief.z + tangent * relief.x + bitangent * relief.y, normal);
                #endif

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    Light sun = GetMainLight(input.screenShadow);
                #else
                    Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                #endif
                float shadow = sun.shadowAttenuation >= 0 && sun.shadowAttenuation <= 1 ? sun.shadowAttenuation : 1;
                float diffuse = saturate(dot(normal, sun.direction)) * shadow;
                float up = saturate(normal.y * 0.5 + 0.5);
                float3 fill = lerp(float3(.36,.32,.27), float3(.43,.49,.56), up);
                float3 view = SurfaceDirection(GetWorldSpaceViewDir(input.positionWS), normal);
                float3 halfway = SurfaceDirection(sun.direction + view, normal);
                float specular = pow(saturate(dot(normal, halfway)), 24) * _Smoothness * diffuse * .14;
                float3 colour = albedo * (fill + sun.color * (diffuse * .82)) + sun.color * specular;
                return half4(MixFog(max(colour, 0), input.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            DepthVaryings ShadowVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                DepthVaryings output = (DepthVaryings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 position = TransformObjectToWorld(input.positionOS.xyz);
                float3 normal = SurfaceDirection(TransformObjectToWorldNormal(input.normalOS, false), float3(0,1,0));
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 direction = SurfaceDirection(_LightPosition - position, normal);
                #else
                    float3 direction = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(position, normal, direction)));
                return output;
            }
            ENDHLSL
        }
    }
}
