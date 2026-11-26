// Project-authored URP shader. No commercial-game source or model is included.
Shader "FirstGame/GospelHero/FaceSdf"
{
    Properties
    {
        [MainTexture] _MainTex("Face color", 2D) = "white" {}
        [MainColor] _Color("Color", Color) = (1,1,1,1)
        _MainTexHSVG("Hue / saturation / value / gamma", Vector) = (0,1,1,1)
        [NoScaleOffset] _FaceSdfTex("Linear R: angular face shadow threshold", 2D) = "white" {}
        [NoScaleOffset] _ComplexionMap("Feature planes (occlusion, cheeks, lips, ridge)", 2D) = "black" {}
        _ComplexionStrength("Painted form strength", Range(0,1)) = 0
        _FaceSdfEnabled("Use authored face SDF", Range(0,1)) = 0
        _FaceSdfStrength("Face SDF mix", Range(0,1)) = 1
        _FaceSdfSoftness("Face shadow edge softness", Range(.001,.25)) = .025
        _FaceSdfFlipSide("Invert light-side UV mirror", Range(0,1)) = 0
        _ShadowColor("Shade color", Color) = (.82,.64,.57,1)
        _ShadowStrength("Shade strength", Range(0,1)) = .65
        _ShadowBlur("Normal fallback softness", Range(.001,.5)) = .2
        _ShadowBorder("Normal fallback threshold", Range(0,1)) = .5
        _ShadowReceive("Real cast-shadow mix", Range(0,1)) = .35
        _AmbientStrength("Sky/probe contribution", Range(0,2)) = .65
        _AsUnlit("Unlit mix", Range(0,1)) = .035
        _LightMinLimit("Minimum light", Range(0,1)) = .2
        _LightMaxLimit("Maximum light", Range(0,4)) = 1.2
        [Toggle] _AlphaClip("Clip source alpha", Float) = 0
        _Cutoff("Source alpha cutoff", Range(0,1)) = .5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [HideInInspector] _GospelHeadForwardWS("Animated head forward", Vector) = (0,0,1,0)
        [HideInInspector] _GospelHeadRightWS("Animated head right", Vector) = (1,0,0,0)
        [HideInInspector] _GospelHeadUpWS("Animated head up", Vector) = (0,1,0,0)
        [HideInInspector] _GospelHeadFrameValid("Animated head frame valid", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
    TEXTURE2D(_FaceSdfTex); SAMPLER(sampler_FaceSdfTex);
    TEXTURE2D(_ComplexionMap); SAMPLER(sampler_ComplexionMap);
    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST, _Color, _MainTexHSVG, _ShadowColor;
        float4 _GospelHeadForwardWS, _GospelHeadRightWS, _GospelHeadUpWS;
        float _ComplexionStrength;
        float _FaceSdfEnabled, _FaceSdfStrength, _FaceSdfSoftness, _FaceSdfFlipSide;
        float _ShadowStrength, _ShadowBlur, _ShadowBorder, _ShadowReceive;
        float _AmbientStrength, _AsUnlit, _LightMinLimit, _LightMaxLimit;
        float _AlphaClip, _Cutoff, _Cull, _GospelHeadFrameValid;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
        half3 normalWS : TEXCOORD1;
        float2 uv : TEXCOORD2;
        half fog : TEXCOORD3;
        float4 screenShadowCoord : TEXCOORD4;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };
    float3 HeroSafeNormalize(float3 v, float3 fallback)
    { return dot(v,v) > 1e-8 ? v * rsqrt(max(dot(v,v),1e-8)) : fallback; }
    float3 HeroRgbToHsv(float3 c)
    {
        float4 k = float4(0,-1.0/3.0,2.0/3.0,-1);
        float4 p = lerp(float4(c.bg,k.wz),float4(c.gb,k.xy),step(c.b,c.g));
        float4 q = lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r));
        float d = q.x-min(q.w,q.y), e=1e-8;
        return float3(abs(q.z+(q.w-q.y)/(6*d+e)),d/(q.x+e),q.x);
    }
    float3 HeroHsvToRgb(float3 c)
    { return c.z*lerp(1.0.xxx,saturate(abs(frac(c.xxx+float3(0,2.0/3.0,1.0/3.0))*6-3)-1),c.y); }
    half3 HeroTone(half3 color)
    {
        float3 hsv=HeroRgbToHsv(max(color,0));
        hsv.x=frac(hsv.x+_MainTexHSVG.x); hsv.y=saturate(hsv.y*_MainTexHSVG.y); hsv.z*=_MainTexHSVG.z;
        return pow(max(HeroHsvToRgb(hsv),0),max(_MainTexHSVG.w,.001));
    }
    half4 HeroAlbedo(float2 rawUv)
    {
        half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,rawUv*_MainTex_ST.xy+_MainTex_ST.zw);
        if (_AlphaClip>.5) clip(c.a*_Color.a-_Cutoff);
        return half4(HeroTone(c.rgb)*_Color.rgb,c.a*_Color.a);
    }
    Varyings HeroVertex(Attributes input)
    {
        Varyings output=(Varyings)0;
        UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
        output.positionCS=TransformWorldToHClip(output.positionWS);
        output.normalWS=TransformObjectToWorldNormal(input.normalOS);
        output.uv=input.uv; output.fog=ComputeFogFactor(output.positionCS.z);
        output.screenShadowCoord=ComputeScreenPos(output.positionCS);
        return output;
    }
    half HeroFaceLit(float2 rawUv, half3 normalWS, float3 lightDirection)
    {
        // Preserve the existing lilToon/Hair normalized NdotL property scale.
        half normalFacing=saturate(dot(normalWS,lightDirection)*.5+.5);
        half normalLit=smoothstep(_ShadowBorder-max(_ShadowBlur,.001),
            _ShadowBorder+max(_ShadowBlur,.001),normalFacing);
        if (_FaceSdfEnabled<.5 || _GospelHeadFrameValid<.5) return normalLit;
        float3 forward=HeroSafeNormalize(_GospelHeadForwardWS.xyz,float3(0,0,1));
        float3 right=HeroSafeNormalize(_GospelHeadRightWS.xyz-forward*dot(forward,_GospelHeadRightWS.xyz),float3(1,0,0));
        float3 up=HeroSafeNormalize(cross(forward,right),float3(0,1,0));
        // The angular mask is independent of mesh normal facets and follows
        // the animated head, including portrait clones. At a vertical-light
        // pole, smoothly approach front-light shading without an arbitrary flip.
        float3 flat=lightDirection-up*dot(up,lightDirection);
        float flatLength=sqrt(max(dot(flat,flat),0));
        float3 planar=HeroSafeNormalize(flat,forward);
        float side=dot(planar,right);
        float mirror=step(side,0);
        mirror=lerp(mirror,1-mirror,step(.5,_FaceSdfFlipSide));
        float2 uv=rawUv; uv.x=lerp(uv.x,1-uv.x,mirror);
        float threshold=.5*(1-clamp(dot(planar,forward),-1,1));
        threshold*=smoothstep(.015,.12,flatLength);
        float sdf=SAMPLE_TEXTURE2D(_FaceSdfTex,sampler_FaceSdfTex,uv).r;
        float soft=max(_FaceSdfSoftness,max(fwidth(sdf),.001));
        half sdfLit=smoothstep(threshold-soft,threshold+soft,sdf);
        return lerp(normalLit,sdfLit,saturate(_FaceSdfStrength));
    }
    half4 HeroFaceFragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        half4 albedo=HeroAlbedo(input.uv);
        if (_ComplexionStrength > 0)
        {
            half4 planes = SAMPLE_TEXTURE2D(_ComplexionMap, sampler_ComplexionMap, input.uv) * _ComplexionStrength;
            albedo.rgb *= lerp(1.0.xxx, half3(.60,.47,.43), planes.r);
            albedo.rgb *= lerp(1.0.xxx, half3(1.035,.85,.81), planes.g);
            albedo.rgb *= lerp(1.0.xxx, half3(.76,.43,.44), planes.b);
            albedo.rgb *= 1 + planes.a * .14;
        }
        half3 n=HeroSafeNormalize(input.normalWS,half3(0,1,0));
        #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
            Light sun=GetMainLight(input.screenShadowCoord);
        #else
            Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
        #endif
        half lit=HeroFaceLit(input.uv,n,sun.direction);
        // Real occluders (hair/nose/neck) remain actual shadow-map inputs.
        lit*=lerp(1,sun.shadowAttenuation,saturate(_ShadowReceive));
        half3 shade=lerp(_ShadowColor.rgb,1.0.xxx,1-(1-lit)*saturate(_ShadowStrength));
        half3 lighting=max(SampleSH(n),0)*_AmbientStrength+sun.color*sun.distanceAttenuation;
        lighting=clamp(lighting,_LightMinLimit,max(_LightMinLimit,_LightMaxLimit));
        half3 color=albedo.rgb*lerp(lighting*shade,1.0.xxx,saturate(_AsUnlit));
        return half4(MixFog(max(color,0),input.fog),1);
    }
    half4 HeroDepthFragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        HeroAlbedo(input.uv); return 0;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HeroVertex
            #pragma fragment HeroFaceFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HeroVertex
            #pragma fragment HeroDepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HeroShadowVertex
            #pragma fragment HeroDepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            Varyings HeroShadowVertex(Attributes input)
            {
                Varyings output=HeroVertex(input);
                float3 lightDirectionWS=_LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    lightDirectionWS=normalize(_LightPosition-output.positionWS);
                #endif
                output.positionCS=TransformWorldToHClip(ApplyShadowBias(output.positionWS,normalize(output.normalWS),lightDirectionWS));
                output.positionCS=ApplyShadowClamping(output.positionCS);
                return output;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HeroVertex
            #pragma fragment HeroNormalFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 HeroNormalFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                HeroAlbedo(input.uv);
                float3 n=normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct=PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                    return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
