Shader "PathOfDawn/Gospel Toon Character"
{
 Properties
 {
  [MainColor] _BaseColor("Paint colour", Color) = (1,1,1,1)
  [MainTexture] _BaseMap("Paint texture", 2D) = "white" {}
  _ShadeColor("Warm violet shadow", Color) = (.80,.78,.85,1)
  _RimColor("Sun edge", Color) = (1,.89,.70,1)
  _ShadeThreshold("Cel transition", Range(0,1)) = .38
  _ShadeSoftness("Soft transition", Range(.01,.25)) = .07
  _RimStrength("Sun edge strength", Range(0,.4)) = .055
  _OutlineWidth("Reserved outline width", Float) = 0
  _HairPaint("Sculpted lock paint", Range(0,1)) = 0
  [HideInInspector] _Smoothness("Smoothness", Range(0,1)) = .1
  [HideInInspector] _Cull("Cull", Float) = 0
  [HideInInspector] _Cutoff("Cutoff", Range(0,1)) = .5
  [HideInInspector] _AlphaClip("Alpha clip", Float) = 0
  [HideInInspector] _AlphaToMask("Alpha to mask", Float) = 0
  [HideInInspector] _Surface("Surface", Float) = 0
  [HideInInspector] _SrcBlend("SrcBlend", Float) = 1
  [HideInInspector] _DstBlend("DstBlend", Float) = 0
  [HideInInspector] _ZWrite("ZWrite", Float) = 1
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;
   half4 _BaseColor;
   half4 _ShadeColor;
   half4 _RimColor;
   half _ShadeThreshold;
   half _ShadeSoftness;
   half _RimStrength;
   half _OutlineWidth;
   half _HairPaint;
   half _Smoothness;
   half _Cutoff;
   half _AlphaClip;
   half _AlphaToMask;
   half _Surface;
  CBUFFER_END
  ENDHLSL
  Pass
  {
   Name "GospelToonForward"
   Tags { "LightMode"="UniversalForward" }
   Cull [_Cull] ZWrite On ZTest LEqual
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
   Varyings Vert(Attributes a)
   {
    Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(a); UNITY_TRANSFER_INSTANCE_ID(a,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.fog=ComputeFogFactor(p.positionCS.z);return o;
   }
   half4 Frag(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half4 paint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
    half3 n=normalize(i.normalWS)*IS_FRONT_VFACE(face,1,-1);
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    // One broad value group and a restrained warm edge. Soft threshold width
    // includes derivatives so narrow faces/strands remain stable on WebGL.
    half diffuse=dot(n,sun.direction)*.5+.5;
    half width=max(_ShadeSoftness,fwidth(diffuse)*1.2);
    half band=smoothstep(_ShadeThreshold-width,_ShadeThreshold+width,diffuse);
    half cast=lerp(.88,1,sun.shadowAttenuation);
    half3 tone=lerp(_ShadeColor.rgb,half3(1.035,1.015,.975),band)*cast;
    half ridge=1-abs(i.uv.x*2-1);
    half lockPaint=lerp(.68,1.07,pow(saturate(ridge),.65));
    paint.rgb*=lerp(1,lockPaint,_HairPaint);
    half3 color=paint.rgb*tone*lerp(half3(1,1,1),sun.color,.13);
    half3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
    half rim=smoothstep(.65,.94,1-saturate(dot(n,view)))*band*_RimStrength;
    color+=_RimColor.rgb*rim;
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster"
   Tags { "LightMode"="ShadowCaster" }
   ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
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
   ZWrite On ColorMask R Cull [_Cull]
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
