Shader "PathOfDawn/Storybook Water"
{
    Properties
    {
        [MainColor] _BaseColor("Clear turquoise",Color)=(.14,.64,.73,1)
        _DeepColor("Lake blue",Color)=(.10,.36,.60,1)
        _FoamColor("Pearl ripples",Color)=(.74,.96,.95,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _DeepColor, _FoamColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half fog:TEXCOORD1; };
            V vert(A a) { V o; VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.world=p.positionWS;o.fog=ComputeFogFactor(p.positionCS.z);return o; }
            half4 frag(V i):SV_Target
            {
                float2 p=i.world.xz;float t=_Time.y;
                half depth=saturate((p.x-4.7)/20);
                half3 col=lerp(_BaseColor.rgb,_DeepColor.rgb,depth*.75);
                float bands=sin(p.y*3.2+p.x*.37+t*.36)+.38*sin(p.y*6.1-p.x*.4-t*.27);
                float edgeWidth=max(fwidth(bands)*1.2,.07);
                float ripples=smoothstep(1.16-edgeWidth,1.31+edgeWidth,bands)*(.4+.6*pow(saturate(sin(p.x*1.8+p.y*.17)),3));
                col=lerp(col,_FoamColor.rgb,ripples*.23);
                float shoreline=(1-smoothstep(.05,.55,p.x-4.7))*smoothstep(.1,.5,sin(p.y*2.3+t*.6));
                col=lerp(col,_FoamColor.rgb,shoreline*.55);
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                col=lerp(col,half3(.69,.84,.96),pow(1-saturate(view.y),3)*.48);
                float glimmer=pow(saturate(sin(p.y*12.8-p.x*3.1+t*.25)),24)*
                    pow(saturate(sin(p.x*4.7+p.y*.19)),10);
                col+=half3(1,.88,.61)*glimmer*.035;
                return half4(MixFog(col,i.fog),1);
            }
            ENDHLSL
        }
    }
}
