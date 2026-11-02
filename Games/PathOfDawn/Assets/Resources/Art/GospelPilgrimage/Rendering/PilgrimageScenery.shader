Shader "FirstGame/Pilgrimage/PaintedScenery"
{
    Properties
    {
        _BaseMap("Preserved Lost Crypt painted source", 2D) = "white" {}
        _Cutoff("Paint edge", Range(0,1)) = .22
        _Wind("Anchored canopy breeze", Range(0,.2)) = .045
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Name "PaintedSceneryForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _Cutoff, _Wind;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; half fog:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 p=input.positionOS.xyz;
                // Only the upper painted branches move; feet and tree roots stay put.
                p.x+=sin(_Time.y*.65+p.x*.27+p.z*.31)*input.uv.y*input.uv.y*_Wind;
                output.positionCS=TransformObjectToHClip(p); output.uv=input.uv;
                output.color=input.color; output.fog=ComputeFogFactor(output.positionCS.z); return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 paint=SAMPLE_TEXTURE2D_LOD(_BaseMap,sampler_BaseMap,input.uv,0);
                clip(paint.a-_Cutoff);
                // Source paintings already contain their own volume and texture.
                // A light cool/warm grade preserves this instead of flat 3D relighting.
                half luminance=dot(paint.rgb,half3(.22,.66,.12));
                half3 color=lerp(luminance.xxx,paint.rgb,.75)*input.color.rgb;
                color*=lerp(.78,1.13,smoothstep(.18,.95,input.uv.y));
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
