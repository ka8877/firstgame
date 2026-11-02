Shader "FirstGame/Pilgrimage/PaintedWater"
{
    Properties { _BaseMap("Retained quiet ripples", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half fog:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.fog=ComputeFogFactor(output.positionCS.z); return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.positionWS.xz;
                float t=_Time.y*.11;
                float bend=sin(p.x*.18+p.y*.11)*.9;
                half ripple=SAMPLE_TEXTURE2D_LOD(_BaseMap,sampler_BaseMap,float2(p.x*.035+t*.025,(p.y+bend)*.09-t*.04),0).r;
                half ribbon=.5+.5*sin((p.y+bend)*3.7+sin(p.x*.36+t)*.5-t);
                half3 deep=half3(.065,.25,.255), reflected=half3(.27,.51,.46);
                half3 color=lerp(deep,reflected,smoothstep(.36,.86,ripple)*.73);
                color+=half3(.10,.18,.15)*pow(saturate(ribbon),17)*(.25+.75*ripple);
                half farBank=smoothstep(17,30,p.y);
                color=lerp(color,half3(.10,.27,.25),farBank*.45);
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
