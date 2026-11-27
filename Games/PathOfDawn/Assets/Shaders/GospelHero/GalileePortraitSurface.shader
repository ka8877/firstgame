Shader "FirstGame/Galilee/Portrait surface"
{
    Properties
    {
        [MainTexture] _BaseMap("Painted surface", 2D) = "white" {}
        [MainColor] _Color("Pigment", Color) = (1,1,1,1)
        [HideInInspector] _BaseColor("Shadow tint", Color) = (1,1,1,1)
        _Relief("Fine relief", 2D) = "gray" {}
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
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _Color, _BaseColor;
            float _ReliefStrength, _SurfaceKind, _Cutoff, _Cull, _AlphaClip;
            CBUFFER_END
            struct Vertex {float4 p:POSITION; float3 n:NORMAL; float4 t:TANGENT; float2 uv:TEXCOORD0;};
            struct Varyings {float4 p:SV_POSITION; float3 world:TEXCOORD0; half3 n:TEXCOORD1;
                half4 t:TEXCOORD2; float2 uv:TEXCOORD3; half fog:TEXCOORD4;};
            Varyings Vert(Vertex v)
            {
                Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.p.xyz);
                o.p=p.positionCS; o.world=p.positionWS; o.n=TransformObjectToWorldNormal(v.n);
                o.t=half4(TransformObjectToWorldDir(v.t.xyz),v.t.w*GetOddNegativeScale());
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE front : FRONT_FACE_SEMANTIC):SV_Target
            {
                half4 surface=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_Color;
                #if defined(_ALPHATEST_ON)
                    clip(surface.a-_Cutoff);
                #endif
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
                    rgb=surface.rgb*(.74+.37*wrap)+half3(.7,.78,.82)*glint;
                }
                return half4(MixFog(rgb,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
