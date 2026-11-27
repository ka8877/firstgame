Shader "FirstGame/Galilee/Loom"
{
 Properties {
  [MainTexture] _BaseMap("Woven repeat",2D)="white" {}
  [MainColor] _Color("Dye",Color)=(1,1,1,1)
  _Sheen("Fiber sheen",Range(0,1))=.14
  _Lining("Tailored neckline",Range(0,1))=0
  _Undyed("Exposed neckline",Color)=(1,1,1,1)
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Cull Off
  Pass {
   Name "Woven fabric"
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST, _Color, _Undyed; float _Sheen, _Lining;
   CBUFFER_END
   struct Attributes {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float2 facing:TEXCOORD1;half4 color:COLOR;};
   struct Interpolators {float4 p:SV_POSITION;float3 pos:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;half mask:TEXCOORD4;};
   Interpolators Vert(Attributes v){Interpolators o;o.pos=TransformObjectToWorld(v.p.xyz);o.p=TransformWorldToHClip(o.pos);o.n=TransformObjectToWorldNormal(v.n);o.uv=TRANSFORM_TEX(lerp(v.uv,v.facing,_Lining),_BaseMap);o.mask=v.color.r;o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 Frag(Interpolators i, FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target{
    half3 n=SafeNormalize(i.n)*IS_FRONT_VFACE(front,1,-1);
    Light key=GetMainLight(TransformWorldToShadowCoord(i.pos+n*.003));
    half wrap=saturate((dot(n,key.direction)+.38)/1.38);
    half3 view=SafeNormalize(GetWorldSpaceViewDir(i.pos));
    half3 dye=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_Color.rgb;
    if(_Lining>.5)dye=lerp(_Undyed.rgb,dye,i.mask);
    half3 illumination=.37+max(0,SampleSH(n))*.38+wrap*.77*min(key.color,1.5)*lerp(.60,1.,key.shadowAttenuation);
    half edge=pow(1.-saturate(dot(n,view)),3.);
    half gloss=pow(saturate(dot(n,SafeNormalize(key.direction+view))),24.);
    half3 rgb=dye*illumination+dye*(edge*.42+gloss*.32)*_Sheen;
    return half4(MixFog(rgb,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
