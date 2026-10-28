// Adapted for an ordinary deforming MeshRenderer from Unity's URP 17
// Mesh2D-Lit-Default contract. Copyright Unity Technologies ApS.
// Unity Companion License for Unity-dependent projects. See SOURCE-NOTICE.txt.
Shader "FirstGame/Luminous Cloth 2D Lit"
{
    Properties
    {
        _MainTex("Painted linen", 2D) = "white" {}
        _MaskTex("Light mask", 2D) = "white" {}
        _NormalMap("Normal map", 2D) = "bump" {}
        _Tint("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex ClothVertex
            #pragma fragment ClothFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            struct Attributes { COMMON_2D_INPUTS };
            struct Varyings { COMMON_2D_LIT_OUTPUTS };
            // Deliberately follows Mesh2D's non-SpriteRenderer material contract.
            float4 _Tint;
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            Varyings ClothVertex(Attributes input) { return CommonLitVertex(input); }
            half4 ClothFragment(Varyings input) : SV_Target { return CommonLitFragment(input, _Tint); }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #pragma vertex ClothNormalVertex
            #pragma fragment ClothNormalFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_NORMALS_INPUTS };
            struct Varyings { COMMON_2D_NORMALS_OUTPUTS };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            float4 _Tint;
            Varyings ClothNormalVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normal);
                output.tangentWS = TransformObjectToWorldDir(input.tangent.xyz);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangent.w * GetOddNegativeScale();
                return output;
            }
            half4 ClothNormalFragment(Varyings input) : SV_Target
            {
                half4 paint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Tint;
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                half3 normalWS = TransformTangentToWorld(normalTS,
                    half3x3(input.tangentWS, input.bitangentWS, input.normalWS));
                // No unity_SpriteColor or unity_SpriteProps: this is a MeshRenderer.
                return half4(normalWS * .5h + .5h, paint.a);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex ClothForwardVertex
            #pragma fragment ClothForwardFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS };
            struct Varyings { COMMON_2D_OUTPUTS };
            float4 _Tint;
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            Varyings ClothForwardVertex(Attributes input) { return CommonUnlitVertex(input); }
            half4 ClothForwardFragment(Varyings input) : SV_Target { return CommonUnlitFragment(input, _Tint); }
            ENDHLSL
        }
    }
}
