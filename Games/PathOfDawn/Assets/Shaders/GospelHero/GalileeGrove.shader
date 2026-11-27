Shader "FirstGame/Galilee/Olive grove"
{
 Properties {
  [MainTexture] _BaseMap("Branch",2D)="white" {}
  [MainColor] _Color("Plant pigment",Color)=(1,1,1,1)
  [Toggle(_OLIVE_LEAVES)] _Leaves("Silver leaves",Float)=0
 }
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
 CBUFFER_START(UnityPerMaterial)
 half4 _Color;
 CBUFFER_END
 struct Attributes {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;};
 struct Vary {float4 p:SV_POSITION;float3 pos:TEXCOORD0;half3 n:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;half fog:TEXCOORD3;};
 Vary Vert(Attributes v){Vary o;o.pos=TransformObjectToWorld(v.p.xyz);o.p=TransformWorldToHClip(o.pos);o.n=TransformObjectToWorldNormal(v.n);o.uv=v.uv;o.color=v.color;o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 Growth(Vary i, FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target {
  half3 n=SafeNormalize(i.n)*IS_FRONT_VFACE(front,1,-1);Light key=GetMainLight(TransformWorldToShadowCoord(i.pos));
  half wrap=saturate(.56+.44*dot(n,key.direction));half3 tint;
  #if defined(_OLIVE_LEAVES)
   half4 sample=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(sample.a-.32);
   tint=sample.rgb*lerp(.75,1.10,i.color.g)*IS_FRONT_VFACE(front,half3(.93,1.,.90),half3(1.10,1.11,1.16));
  #else
   float crease=i.uv.x*11.+.32*sin(i.uv.y*5.);
   half grain=sin(crease*6.283)*saturate(1.-fwidth(crease)*2.);
   tint=SRGBToLinear(i.color.rgb)*(.97+.12*grain);
  #endif
  half3 tone=.35+SampleSH(n)*.32+min(key.color,1.5)*wrap*.78*lerp(.68,1.,key.shadowAttenuation);
  return half4(MixFog(tint*tone*_Color.rgb,i.fog),1);
 }
 half4 Depth(Vary i):SV_Target{
  #if defined(_OLIVE_LEAVES)
   clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a-.32);
  #endif
  return 0;
 }
 ENDHLSL
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"} Cull Off
  Pass {Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment Growth
   #pragma shader_feature_local_fragment _OLIVE_LEAVES
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   ENDHLSL
  }
  Pass {Tags {"LightMode"="ShadowCaster"} ColorMask 0
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Depth
   #pragma shader_feature_local_fragment _OLIVE_LEAVES
   ENDHLSL
  }
  Pass {Tags {"LightMode"="DepthOnly"} ColorMask R
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Depth
   #pragma shader_feature_local_fragment _OLIVE_LEAVES
   ENDHLSL
  }
 }
}
