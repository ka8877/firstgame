Shader "FirstGame/Galilee/Limestone terraces"
{
 Properties {[MainTexture] _BaseMap("Galilee panorama",2D)="white"{} _Tint("Sky light",Color)=(1,1,1,1)}
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent"}
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _Tint;
   CBUFFER_END
   struct V {float4 p:POSITION;float2 uv:TEXCOORD0;};
   struct F {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
   F Vert(V v){F o;o.p=TransformObjectToHClip(v.p.xyz);o.uv=v.uv;return o;}
   half4 Frag(F i):SV_Target{
    float u=1.-abs(frac(i.uv.x*.5)*2.-1.);
    half3 landscape=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,float2(u,i.uv.y)).rgb;
    landscape=lerp(landscape,half3(.57,.66,.69),.11);
    return half4(landscape*_Tint.rgb,1.-smoothstep(.64,.91,i.uv.y));
   }
   ENDHLSL
  }
 }
}
