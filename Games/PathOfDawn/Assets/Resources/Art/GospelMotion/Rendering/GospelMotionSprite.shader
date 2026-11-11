Shader "FirstGame/GospelMotion/OriginalFlareLayers"
{
    Properties
    {
        [PerRendererData] _MainTex("Unchanged original Flare layer", 2D) = "white" {}
        _Color("Cloth palette", Color) = (1,1,1,1)
        _Colorize("Render cloth in story palette", Range(0,1)) = 0
        _ActorGroundY("Physical foot plane for neutral bodies", Float) = -10000
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Colorize;
                float _ActorGroundY;
            CBUFFER_END
            struct Input { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varying
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half4 color : COLOR;
            };
            struct FragmentOutput { half4 color : SV_Target; float depth : SV_Depth; };
            Varying Vert(Input input)
            {
                Varying output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv; output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            FragmentOutput Frag(Varying input)
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(source.a - .30h);
                half luminance = dot(source.rgb, half3(.2126h,.7152h,.0722h));
                half3 cloth = min(1.12h, luminance * 2.2h) * _Color.rgb;
                half3 rgb = lerp(source.rgb * _Color.rgb, cloth, saturate(_Colorize));
                FragmentOutput output;
                output.color = half4(MixFog(rgb * input.color.rgb, input.fog), source.a * input.color.a);
                output.depth = input.positionCS.z;
                // A projected toe can lie below the billboard's physical foot
                // origin even though the source 3D toe rests on that plane.
                // Move only its depth along the same view ray: screen XY,
                // source UVs, the common anchor and physical controller stay
                // unchanged. Upper body fragments retain ordinary scene depth.
                float lift = max(0, _ActorGroundY - input.positionWS.y);
                if (lift > 0)
                {
                    float3 viewBack = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    float3 depthWS = input.positionWS + viewBack * (lift / max(.15, viewBack.y));
                    float4 depthCS = TransformWorldToHClip(depthWS);
                    float depth = depthCS.z / depthCS.w;
                    #if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                        depth = depth * .5 + .5;
                    #endif
                    output.depth = saturate(depth);
                }
                return output;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
