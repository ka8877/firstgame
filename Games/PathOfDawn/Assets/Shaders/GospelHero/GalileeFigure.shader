Shader "FirstGame/Galilee/Fitted figure"
{
    Properties
    {
        [MainTexture] _BaseMap ("Registered costume", 2D) = "white" {}
        [MainColor] _Color ("Fabric tint", Color) = (1,1,1,1)
        _AnatomyMap ("Hands and feet", 2D) = "white" {}
        _HideMap ("Sandal leather", 2D) = "white" {}
        [Normal] _WeaveNormal ("Woven fibre", 2D) = "bump" {}
        _VeilWeave ("Linen fibres", 2D) = "white" {}
        _VeilTint ("Head covering dye", Color) = (.32,.42,.4,1)
        _BorderMap ("Tablet woven selvedge", 2D) = "white" {}
        _SkinBalance ("Complexion", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            Name "Fitted figure"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_AnatomyMap); SAMPLER(sampler_AnatomyMap);
            TEXTURE2D(_HideMap); SAMPLER(sampler_HideMap);
            TEXTURE2D(_VeilWeave); SAMPLER(sampler_VeilWeave);
            TEXTURE2D(_BorderMap); SAMPLER(sampler_BorderMap);
            TEXTURE2D(_WeaveNormal); SAMPLER(sampler_WeaveNormal);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _SkinBalance, _VeilTint;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 grain : TEXCOORD0;
                float4 frontBack : TEXCOORD1;
                float4 profileWeights : TEXCOORD2;
                half4 surface : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float4 frontBack : TEXCOORD3;
                float4 profileWeights : TEXCOORD4;
                float2 grain : TEXCOORD5;
                half fog : TEXCOORD6;
                half4 surface : COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w*GetOddNegativeScale());
                o.grain=input.grain; o.frontBack=input.frontBack; o.profileWeights=input.profileWeights;
                o.surface=input.surface; o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 normal = SafeNormalize(i.normalWS)*IS_FRONT_VFACE(facing,1,-1);
                half skin=saturate(i.surface.r), leather=saturate(i.surface.g), metal=smoothstep(.75h,.99h,i.surface.b);
                half border=step(.35h,i.surface.b)*(1.h-step(.75h,i.surface.b));
                // The three static UV sets are sewn to the moving vertices. Visibility was measured in the fitting pose.
                half2 ab=saturate(i.profileWeights.zw);
                half3 diffuse=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.frontBack.xy).rgb*ab.x
                    + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.frontBack.zw).rgb*ab.y
                    + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.profileWeights.xy).rgb*saturate(1-ab.x-ab.y);
                if(i.surface.a>.001h)
                {
                    half3 anatomy = SAMPLE_TEXTURE2D(_AnatomyMap,sampler_AnatomyMap,i.grain).rgb;
                    half3 hide = SAMPLE_TEXTURE2D(_HideMap,sampler_HideMap,i.grain).rgb*half3(.34h,.20h,.12h);
                    diffuse=lerp(diffuse,lerp(anatomy,hide,leather),i.surface.a);
                }
                half veil=step(.15h,i.surface.b)*(1.h-step(.34h,i.surface.b));
                if(veil>.5h) {
                    half3 thread=SAMPLE_TEXTURE2D(_VeilWeave,sampler_VeilWeave,i.grain*float2(3.8,8)).rgb;
                    half yarn=dot(thread,half3(.24h,.57h,.19h));
                    half3 dyedLinen=_VeilTint.rgb*(.66h+1.12h*sqrt(saturate(yarn)));
                    diffuse=lerp(dyedLinen,diffuse*(.88h+.45h*sqrt(saturate(yarn))),.82h);
                }
                if(border>.5h) diffuse=SAMPLE_TEXTURE2D(_BorderMap,sampler_BorderMap,i.grain).rgb*half3(.51h,.50h,.44h);
                diffuse*=lerp(_Color.rgb,_SkinBalance.rgb,skin);
                half textile=saturate(1-max(skin,max(leather,metal)));
                half3 tangent=SafeNormalize(i.tangentWS.xyz-normal*dot(i.tangentWS.xyz,normal));
                half3 bitangent=cross(normal,tangent)*i.tangentWS.w;
                half3 fibre=UnpackNormalScale(SAMPLE_TEXTURE2D(_WeaveNormal,sampler_WeaveNormal,i.grain*6),.24h*textile);
                normal=SafeNormalize(normal*fibre.z+tangent*fibre.x+bitangent*fibre.y);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS+normal*.0018));
                half3 view=SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                half nl=dot(normal,light.direction);
                half wrap=saturate((nl+.22h)/1.22h);
                half shadow=lerp(.43h,1.h,saturate(light.shadowAttenuation));
                half3 indirect=.47h+.32h*clamp(SampleSH(normal),0.h,1.5h);
                half3 direct=.55h*min(light.color,1.65h)*wrap*shadow;
                half3 result=diffuse*(indirect+direct);
                half nv=saturate(dot(normal,view));
                half nh=saturate(dot(normal,SafeNormalize(light.direction+view)));
                half grazing=pow(1-nv,3.h);
                result+=diffuse*(.115h*textile*grazing+.055h*leather*grazing);
                half gloss=lerp(16.h,42.h,max(skin,leather));
                result+=light.color*shadow*pow(nh,gloss)*(.006h+skin*.017h+leather*.031h);
                result+=diffuse*skin*saturate(dot(-normal,light.direction))*.09h*half3(1,.46h,.29h);
                result+=metal*shadow*pow(nh,72.h)*half3(.37h,.29h,.16h);
                return half4(MixFog(max(result,0),i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
