Shader "FirstGame/GospelNpr/Ground"
{
    Properties
    {
        _BlendMap("World path, court, grove and shore", 2D) = "black" {}
        _StoneAlbedo("Retained limestone paving colour", 2D) = "white" {}
        _StoneNormal("Retained limestone paving normal", 2D) = "bump" {}
        _StoneDetailStrength("Paving detail contrast", Range(0,1)) = .72
        _StoneNormalStrength("Paving normal strength", Range(0,1)) = .24
        _MeadowScan("Galilee grass and stones", 2D) = "white" {}
        _TrailScan("Travelled gravel", 2D) = "white" {}
        _MeadowRelief("Grass and stones relief", 2D) = "bump" {}
        _TrailRelief("Gravel relief", 2D) = "bump" {}
        _LandscapeDetail("Surface photographs", Range(0,1)) = 0
        _WorldRect("Ground origin and reciprocal extent", Vector) = (-64,-48,.007352941,.009803922)
        _MeadowColor("Sunlit meadow", Color) = (.39,.53,.28,1)
        _EarthColor("Warm travelled earth", Color) = (.73,.61,.42,1)
        _StoneColor("Ivory limestone court", Color) = (.83,.79,.65,1)
        _ShadowColor("Cool soft shadow", Color) = (.67,.76,.83,1)
    }
    HLSLINCLUDE
    // Every pass in this shader shares one material layout. Do not UsePass
    // URP/Lit passes: their LitInput UnityPerMaterial has a different layout.
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        float4 _WorldRect;
        float4 _MeadowColor;
        float4 _EarthColor;
        float4 _StoneColor;
        float4 _ShadowColor;
        float _StoneDetailStrength;
        float _StoneNormalStrength;
        float _LandscapeDetail;
    CBUFFER_END

    struct NprDepthAttributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct NprDepthVaryings
    {
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };
    NprDepthVaryings NprDepthVertex(NprDepthAttributes input)
    {
        NprDepthVaryings output = (NprDepthVaryings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        return output;
    }
    half NprDepthFragment(NprDepthVaryings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return input.positionCS.z;
    }
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "NprGroundForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            TEXTURE2D(_BlendMap); SAMPLER(sampler_BlendMap);
            TEXTURE2D(_StoneAlbedo); SAMPLER(sampler_StoneAlbedo);
            TEXTURE2D(_StoneNormal); SAMPLER(sampler_StoneNormal);
            TEXTURE2D(_MeadowScan); SAMPLER(sampler_MeadowScan);
            TEXTURE2D(_TrailScan); SAMPLER(sampler_TrailScan);
            TEXTURE2D(_MeadowRelief); SAMPLER(sampler_MeadowRelief);
            TEXTURE2D(_TrailRelief); SAMPLER(sampler_TrailRelief);
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float Hash(float2 p)
            {
                p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y);
            }
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.positionWS.xz;
                half4 mask=SAMPLE_TEXTURE2D(_BlendMap,sampler_BlendMap,saturate((p-_WorldRect.xy)*_WorldRect.zw));
                half broad=Noise(p*.12), detail=Noise(p*2.3), fine=Noise(p*9.1);
                half3 meadow=_MeadowColor.rgb*lerp(.91,1.08,broad);
                half3 earth=_EarthColor.rgb*lerp(.94,1.04,detail*.32+broad*.68);
                float2 grassUV=p*.182, trailUV=p*.56;
                half3 grassPhoto=SAMPLE_TEXTURE2D(_MeadowScan,sampler_MeadowScan,grassUV).rgb;
                half3 gravel=SAMPLE_TEXTURE2D(_TrailScan,sampler_TrailScan,trailUV).rgb;
                half dry=smoothstep(.27,.82,broad*.72+detail*.28);
                meadow=lerp(meadow,grassPhoto*lerp(half3(.88,.99,.76),half3(1.13,.99,.78),dry)*1.37+.026,_LandscapeDetail*.90);
                earth=lerp(earth,gravel*half3(1.26,1.14,.96)+half3(.13,.105,.074),_LandscapeDetail*.82);
                half3 stone=_StoneColor.rgb*lerp(.96,1.035,detail);
                half3 paving=SAMPLE_TEXTURE2D(_StoneAlbedo,sampler_StoneAlbedo,p*.36).rgb;
                stone*=lerp(half3(1,1,1),.55+paving*.75,_StoneDetailStrength);
                // A quiet worn-court pattern is sized in metres. Its contrast
                // stays below the characters and real architectural stonework.
                float2 tile=frac(p/float2(1.5,1.1));
                half joint=1-smoothstep(.012,.026,min(min(tile.x,1-tile.x),min(tile.y,1-tile.y)));
                stone*=1-joint*.075;
                half path=saturate(mask.r*(.95+detail*.05));
                half court=smoothstep(.15,.9,mask.g);
                half3 albedo=lerp(lerp(meadow,earth,path),stone,court);
                albedo=lerp(albedo,meadow*.93,mask.b*(1-court)*.16);
                albedo=lerp(albedo,half3(.56,.65,.52),mask.a*.24);
                albedo*=.985+fine*.03;
                half3 normal=normalize(input.normalWS);
                half3 pavingNormal=UnpackNormalScale(SAMPLE_TEXTURE2D(_StoneNormal,sampler_StoneNormal,p*.36),1);
                normal=normalize(normal+half3(pavingNormal.x,0,pavingNormal.y)*court*_StoneNormalStrength);
                half3 grassN=UnpackNormal(SAMPLE_TEXTURE2D(_MeadowRelief,sampler_MeadowRelief,grassUV));
                half3 trailN=UnpackNormal(SAMPLE_TEXTURE2D(_TrailRelief,sampler_TrailRelief,trailUV));
                half2 rough=lerp(grassN.xy,trailN.xy,path);
                normal=SafeNormalize(normal+half3(rough.x,0,rough.y)*(.45*_LandscapeDetail*(1-court)));
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half facing=smoothstep(-.25,.75,dot(normal,sun.direction));
                half shadow=smoothstep(.05,.95,sun.shadowAttenuation);
                half3 shadowTint=lerp(_ShadowColor.rgb,half3(1,1,1),shadow);
                half3 ambient=SampleSH(normal);
                half3 illumination=ambient*.48+sun.color*(.30+.34*facing);
                half3 color=albedo*illumination*shadowTint;
                return half4(MixFog(max(color,0),input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "NprGroundDepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex NprDepthVertex
            #pragma fragment NprDepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "NprGroundShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex NprShadowVertex
            #pragma fragment NprShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            // Set by URP ShadowUtils.SetupShadowCasterConstantBuffer.
            float3 _LightDirection;
            float3 _LightPosition;
            NprDepthVaryings NprShadowVertex(NprDepthAttributes input)
            {
                NprDepthVaryings output = (NprDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                // Use the actual installed URP17.3 helper, including reversed-Z clamping.
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }
            half4 NprShadowFragment(NprDepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
