Shader "FirstGame/Galilee/Portrait surface"
{
    Properties
    {
        [MainTexture] _BaseMap("Painted surface", 2D) = "white" {}
        [MainColor] _Color("Pigment", Color) = (1,1,1,1)
        [HideInInspector] _BaseColor("Shadow tint", Color) = (1,1,1,1)
        _Relief("Fine relief", 2D) = "gray" {}
        _GroomPigment("Painted hair fibers", 2D) = "black" {}
        _HasGroomPigment("Use groom painting", Float) = 0
        _GroomHairline("Painted hairline", Float) = 0
        _StrokeOpacity("Fiber opacity", Float) = 1
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _ZWrite("Write depth", Float) = 1
        _ReliefStrength("Relief strength", Range(0,1)) = 0
        [Enum(Skin,0,Fiber,1,Eye,2,Enamel,3)] _SurfaceKind("Surface", Float) = 0
        _Cutoff("Strand cutout", Range(0,1)) = .26
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Cutout", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Culling", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "Portrait"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Relief); SAMPLER(sampler_Relief);
            TEXTURE2D(_GroomPigment); SAMPLER(sampler_GroomPigment);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _Color, _BaseColor;
            float _GroomHairline, _StrokeOpacity, _SrcBlend, _DstBlend, _ZWrite;
            float _ReliefStrength, _SurfaceKind, _Cutoff, _Cull, _AlphaClip, _HasGroomPigment;
            CBUFFER_END
            struct Vertex {float4 p:POSITION; float3 n:NORMAL; float4 t:TANGENT; float2 uv:TEXCOORD0; float4 painting:TEXCOORD1;};
            struct Varyings {float4 p:SV_POSITION; float3 world:TEXCOORD0; half3 n:TEXCOORD1;
                half4 t:TEXCOORD2; float2 uv:TEXCOORD3; half fog:TEXCOORD4; float4 painting:TEXCOORD5;};
            Varyings Vert(Vertex v)
            {
                Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.p.xyz);
                o.p=p.positionCS; o.world=p.positionWS; o.n=TransformObjectToWorldNormal(v.n);
                o.t=half4(TransformObjectToWorldDir(v.t.xyz),v.t.w*GetOddNegativeScale());
                o.painting=v.painting; o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE front : FRONT_FACE_SEMANTIC):SV_Target
            {
                half4 surface=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_Color;
                #if defined(_ALPHATEST_ON)
                    if (_HasGroomPigment<.5) clip(surface.a-_Cutoff);
                #endif
                if (_HasGroomPigment>.5) {
                    half4 fiberPaint=SAMPLE_TEXTURE2D(_GroomPigment,sampler_GroomPigment,saturate(i.painting.xy));
                    surface.rgb=lerp(surface.rgb,fiberPaint.rgb,fiberPaint.a*saturate(i.painting.z));
                    // Retain the volume of the back and let the painted fringe transition into real skin.
                    half faceSide=smoothstep(.15,.83,i.painting.w)*smoothstep(.16,.35,i.painting.y)*_GroomHairline;
                    surface.a*=lerp(1.,smoothstep(.12,.53,fiberPaint.a),faceSide);
                    surface.a*=1.-.94*smoothstep(.70,.93,i.painting.z)*faceSide;
                    clip(surface.a-_Cutoff);
                }
                half3 n=SafeNormalize(i.n)*IS_FRONT_VFACE(front,1,-1);
                half3 t=SafeNormalize(i.t.xyz), b=SafeNormalize(cross(n,t))*i.t.w;
                if (_ReliefStrength>0) {
                    half2 detail=SAMPLE_TEXTURE2D(_Relief,sampler_Relief,i.uv).rg*2-1;
                    n=SafeNormalize(n+_ReliefStrength*(detail.x*t+detail.y*b));
                }
                Light key=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 view=SafeNormalize(GetWorldSpaceViewDir(i.world)), midway=SafeNormalize(view+key.direction);
                half nl=dot(n,key.direction), nv=saturate(dot(n,view));
                half wrap=saturate((nl+.35)/1.35), shadow=lerp(.57,1.,key.shadowAttenuation);
                half3 fill=.37+min(.29,max(0.,SampleSH(n))*.43);
                half3 light=fill+.66*wrap*shadow*lerp(half3(1,1,1),min(key.color,1.65),.43);
                half3 rgb=surface.rgb*light;
                if (_SurfaceKind < .5) {
                    half grazing=pow(1.-nv,3.);
                    rgb+=surface.rgb*half3(.15,.052,.027)*(1.-wrap)*(.55+.45*grazing);
                    rgb+=half3(.075,.058,.045)*pow(saturate(dot(n,midway)),32.)*shadow;
                } else if (_SurfaceKind < 1.5) {
                    half sheen=pow(saturate(1.-abs(dot(b,midway))),11.)*saturate(nl*.65+.35);
                    rgb+=surface.rgb*(.20*sheen+.11*pow(1.-nv,3.));
                } else if (_SurfaceKind < 2.5) {
                    half glint=pow(saturate(dot(n,midway)),96.);
                    half sclera=smoothstep(.23,.63,min(surface.r,min(surface.g,surface.b)));
                    half lid=lerp(1.,.83,smoothstep(.558,.60,i.painting.y));
                    rgb=surface.rgb*(.73+.35*wrap)*lid*lerp(half3(1,1,1),half3(.96,.87,.83),sclera);
                    rgb+=half3(.55,.61,.64)*glint;
                }
                return half4(MixFog(rgb,i.fog),surface.a*_StrokeOpacity);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
