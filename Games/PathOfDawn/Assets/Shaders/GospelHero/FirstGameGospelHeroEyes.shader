Shader "FirstGame/GospelHero/Eyes"
{
    Properties
    {
        [MainTexture] _MainTex("Eye albedo / retained atlas", 2D) = "white" {}
        [MainColor] _Color("Albedo tint", Color) = (1,1,1,1)
        _MainTexHSVG("Hue / saturation / value / gamma", Vector) = (0,1,1,1)
        [NoScaleOffset] _IrisTex("Dedicated iris colour", 2D) = "white" {}
        [NoScaleOffset] _IrisMask("Linear R iris / G pupil / B cornea / A lid occlusion", 2D) = "white" {}
        [Toggle] _UseIrisTexture("Use dedicated iris colour", Float) = 0
        [Toggle] _UseIrisMask("Use authored eye masks", Float) = 0
        [Enum(CombinedFittedEye,0,IrisAtlasSurface,1,ScleraAtlasSurface,2)] _EyeSurface("Explicit fitted surface", Float) = 1
        _IrisUvRect("Raw UV eye rectangle: origin XY / size ZW", Vector) = (0,0,1,1)
        _IrisCenter("Iris centre in eye-local UV", Vector) = (.5,.5,0,0)
        _IrisRadius("Iris radius in eye-local UV", Range(.02,.49)) = .24
        _PupilRadius("Pupil radius in eye-local UV", Range(.005,.3)) = .075
        _IrisColor("Iris tint", Color) = (1,1,1,1)
        _ScleraColor("Sclera tint", Color) = (.97,.96,.94,1)
        _PupilColor("Pupil colour", Color) = (.018,.014,.012,1)
        _LimbalColor("Limbal edge colour", Color) = (.055,.035,.025,1)
        _LimbalStrength("Optional iris-edge definition", Range(0,1)) = 0
        _LimbalWidth("Iris-edge width / radius", Range(.005,.25)) = .06
        _CorneaSmoothness("Corneal highlight smoothness", Range(0,.99)) = .88
        _CorneaFresnel("Corneal normal-incidence reflectance", Range(.01,.08)) = .027
        _CorneaNormalBlend("Optional normal-only curvature approximation", Range(0,.4)) = 0
        _HighlightStrength("Main-light corneal highlight", Range(0,2)) = .9
        _HighlightColor("Corneal highlight tint", Color) = (1,.995,.98,1)
        _UpperLidOcclusion("Upper-lid occlusion amount", Range(0,1)) = .25
        _UpperLidBoundary("Fallback lid boundary in eye-local V", Range(0,1)) = .68
        _UpperLidSoftness("Fallback lid gradient", Range(.005,.3)) = .10
        _OcclusionColor("Upper-lid contact tint", Color) = (.52,.46,.44,1)
        _DiffuseWrap("Soft globe lighting wrap", Range(0,1)) = .25
        _ShadowStrength("Receive main-light shadow", Range(0,1)) = .45
        _ShadowColor("Soft eye shade tint", Color) = (.63,.66,.73,1)
        _AmbientStrength("Diffuse probe contribution", Range(0,2)) = 1
        _AsUnlit("Diffuse-only unlit blend", Range(0,1)) = .10
        _LightMinLimit("Diffuse minimum", Range(0,1)) = .16
        _LightMaxLimit("Diffuse maximum", Range(0,4)) = 1.05
        [Toggle] _AlphaClip("Retained atlas cutout", Float) = 0
        _Cutoff("Atlas alpha cutoff", Range(0,1)) = .5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

    TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
    TEXTURE2D(_IrisTex); SAMPLER(sampler_IrisTex);
    TEXTURE2D(_IrisMask); SAMPLER(sampler_IrisMask);

    // All custom passes below share this exact material layout. No URP Lit
    // UsePass is borrowed with a different UnityPerMaterial declaration.
    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        float4 _Color;
        float4 _MainTexHSVG;
        float4 _IrisUvRect;
        float4 _IrisCenter;
        float4 _IrisColor;
        float4 _ScleraColor;
        float4 _PupilColor;
        float4 _LimbalColor;
        float4 _HighlightColor;
        float4 _OcclusionColor;
        float4 _ShadowColor;
        float _UseIrisTexture;
        float _UseIrisMask;
        float _EyeSurface;
        float _IrisRadius;
        float _PupilRadius;
        float _LimbalStrength;
        float _LimbalWidth;
        float _CorneaSmoothness;
        float _CorneaFresnel;
        float _CorneaNormalBlend;
        float _HighlightStrength;
        float _UpperLidOcclusion;
        float _UpperLidBoundary;
        float _UpperLidSoftness;
        float _DiffuseWrap;
        float _ShadowStrength;
        float _AmbientStrength;
        float _AsUnlit;
        float _LightMinLimit;
        float _LightMaxLimit;
        float _AlphaClip;
        float _Cutoff;
        float _Cull;
    CBUFFER_END

    struct HeroEyeAttributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 tangentOS : TANGENT;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct HeroEyeVaryings
    {
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
        half3 normalWS : TEXCOORD1;
        half4 tangentWS : TEXCOORD2;
        float2 rawUV : TEXCOORD3;
        half fog : TEXCOORD4;
        float4 shadowCoord : TEXCOORD5;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };
    struct HeroEyeDepthVaryings
    {
        float4 positionCS : SV_POSITION;
        float2 rawUV : TEXCOORD0;
        half3 normalWS : TEXCOORD1;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };

    float3 HeroEyeSafeNormal(float3 value, float3 fallback)
    {
        float lengthSquared = dot(value, value);
        return lengthSquared > 0.000001 ? value * rsqrt(lengthSquared) : fallback;
    }

    // Project-local HSVG adaptation, with the same public neutral vector as
    // the retained materials. This is not an assertion of lilToon bit parity.
    float3 HeroEyeTone(float3 colour)
    {
        colour = max(colour, 0);
        float high = max(colour.r, max(colour.g, colour.b));
        float low = min(colour.r, min(colour.g, colour.b));
        float chroma = high - low;
        float hue = 0;
        if (chroma > 0.00001)
        {
            if (high == colour.r) hue = (colour.g - colour.b) / chroma;
            else if (high == colour.g) hue = 2 + (colour.b - colour.r) / chroma;
            else hue = 4 + (colour.r - colour.g) / chroma;
            hue /= 6;
        }
        float saturation = high > 0.00001 ? chroma / high : 0;
        hue = frac(hue + _MainTexHSVG.x);
        saturation = saturate(saturation * _MainTexHSVG.y);
        float brightness = max(0, high * _MainTexHSVG.z);
        float3 ramps = saturate(abs(frac(hue + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1);
        float3 graded = brightness * lerp(float3(1,1,1), ramps, saturation);
        return pow(max(graded, 0), max(_MainTexHSVG.w, .001));
    }

    float2 HeroEyeAtlasUV(float2 uv) { return uv * _MainTex_ST.xy + _MainTex_ST.zw; }
    float2 HeroEyeLocalUV(float2 uv) { return (uv - _IrisUvRect.xy) / max(abs(_IrisUvRect.zw), .0001); }

    void HeroEyeClip(float2 rawUV)
    {
        if (_AlphaClip > .5)
            clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, HeroEyeAtlasUV(rawUV)).a * _Color.a - _Cutoff);
    }

    HeroEyeVaryings HeroEyeVertex(HeroEyeAttributes input)
    {
        HeroEyeVaryings output = (HeroEyeVaryings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
        VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
        output.positionCS = position.positionCS; output.positionWS = position.positionWS;
        output.normalWS = normal.normalWS;
        output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
        output.rawUV = input.uv; output.fog = ComputeFogFactor(position.positionCS.z);
        output.shadowCoord = GetShadowCoord(position);
        return output;
    }

    HeroEyeDepthVaryings HeroEyeDepthVertex(HeroEyeAttributes input)
    {
        HeroEyeDepthVaryings output = (HeroEyeDepthVaryings)0;
        UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.rawUV = input.uv; output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        return output;
    }

    float HeroEyeCornea(float3 normal, float3 view, float3 light)
    {
        float nl = saturate(dot(normal, light)), nv = saturate(dot(normal, view));
        float3 halfway = HeroEyeSafeNormal(view + light, normal);
        float nh = saturate(dot(normal, halfway)), vh = saturate(dot(view, halfway));
        float roughness = max(.035, 1 - saturate(_CorneaSmoothness));
        float alpha = roughness * roughness;
        float variance = max(dot(ddx(normal), ddx(normal)), dot(ddy(normal), ddy(normal)));
        // Filter subpixel glints instead of painting a permanent white mark.
        float alphaSquared = clamp(alpha * alpha + variance * .25, .0015, 1);
        float denominator = nh * nh * (alphaSquared - 1) + 1;
        float distribution = alphaSquared / max(PI * denominator * denominator, .000001);
        float k = (roughness + 1) * (roughness + 1) * .125;
        float visibility = (nl / max(nl * (1 - k) + k, .0001)) * (nv / max(nv * (1 - k) + k, .0001));
        float f0 = clamp(_CorneaFresnel, .01, .08);
        float fresnel = f0 + (1 - f0) * pow(1 - vh, 5);
        return min(4, distribution * visibility * fresnel / max(4 * nl * nv, .0001)) * nl;
    }

    half4 HeroEyeFragment(HeroEyeVaryings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        HeroEyeClip(input.rawUV);
        float2 eyeUV = HeroEyeLocalUV(input.rawUV);
        float radius = max(_IrisRadius, .02);
        float2 irisPosition = eyeUV - _IrisCenter.xy;
        float distanceToCentre = length(irisPosition);
        float edge = max(fwidth(distanceToCentre), .0005);
        float iris = 1 - smoothstep(radius - edge, radius + edge, distanceToCentre);
        float pupil = 1 - smoothstep(_PupilRadius - edge, _PupilRadius + edge, distanceToCentre);
        float coat = lerp(.24, 1, iris);
        float upperLid = smoothstep(_UpperLidBoundary - max(_UpperLidSoftness, .005), _UpperLidBoundary + max(_UpperLidSoftness, .005), eyeUV.y);
        float limbal = iris * smoothstep(radius * (1 - saturate(_LimbalWidth)) - edge, radius - edge, distanceToCentre);
        if (_EyeSurface > .5)
        {
            // An existing iris/sclera atlas submesh is not assumed to contain
            // a centred whole-eye UV disk. Its original painted pupil remains.
            iris = _EyeSurface < 1.5 ? 1 : 0; pupil = 0; limbal = 0;
            coat = _EyeSurface < 1.5 ? 1 : .24;
            upperLid = 0;
        }
        if (_UseIrisMask > .5)
        {
            half4 masks = SAMPLE_TEXTURE2D(_IrisMask, sampler_IrisMask, saturate(eyeUV));
            iris = saturate(masks.r); pupil = saturate(masks.g); coat = saturate(masks.b); upperLid = saturate(masks.a);
            // Authored masks/albedo define their own edge; an unrelated radial
            // limbal ring must not be imposed on an asymmetric UV layout.
            limbal = 0;
        }
        half3 base = HeroEyeTone(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, HeroEyeAtlasUV(input.rawUV)).rgb) * _Color.rgb;
        half3 irisColour = base * _IrisColor.rgb;
        if (_UseIrisTexture > .5)
        {
            float2 irisUV = saturate(irisPosition / (2 * radius) + .5);
            irisColour = SAMPLE_TEXTURE2D(_IrisTex, sampler_IrisTex, irisUV).rgb * _IrisColor.rgb;
        }
        half3 albedo = lerp(base * _ScleraColor.rgb, irisColour, iris);
        albedo = lerp(albedo, _PupilColor.rgb, pupil);
        albedo = lerp(albedo, _LimbalColor.rgb, limbal * saturate(_LimbalStrength));
        float3 normal = HeroEyeSafeNormal(input.normalWS, float3(0,0,1));
        float3 tangent = input.tangentWS.xyz - normal * dot(normal, input.tangentWS.xyz);
        float3 tangentFallback = HeroEyeSafeNormal(cross(abs(normal.y) < .95 ? float3(0,1,0) : float3(1,0,0), normal), float3(1,0,0));
        tangent = HeroEyeSafeNormal(tangent, tangentFallback);
        float3 bitangent = cross(normal, tangent) * (input.tangentWS.w < 0 ? -1 : 1);
        // Default zero: use the actual fitted globe/corneal mesh normal.
        // Nonzero is explicitly a normal-only approximation, never geometry.
        float2 disk = clamp(irisPosition / radius, -1, 1);
        float3 corneaNormal = HeroEyeSafeNormal(normal + (tangent * disk.x + bitangent * disk.y) * _CorneaNormalBlend * iris, normal);
        float3 view = HeroEyeSafeNormal(GetWorldSpaceViewDir(input.positionWS), normal);
        #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
            Light key = GetMainLight(input.shadowCoord);
        #else
            Light key = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
        #endif
        float3 lightDirection = HeroEyeSafeNormal(key.direction, float3(0,1,0));
        float wrap = saturate((dot(normal, lightDirection) + saturate(_DiffuseWrap)) / (1 + saturate(_DiffuseWrap)));
        float mainShadow = lerp(1, key.shadowAttenuation, saturate(_ShadowStrength));
        half3 diffuseKey = key.color * key.distanceAttenuation * mainShadow * lerp(_ShadowColor.rgb * .12, half3(1,1,1), wrap);
        half3 diffuse = max(half3(0,0,0), SampleSH(normal)) * max(0, _AmbientStrength) + diffuseKey;
        float minimum = saturate(_LightMinLimit), maximum = max(minimum, _LightMaxLimit);
        diffuse = clamp(diffuse, minimum.xxx, maximum.xxx);
        diffuse = lerp(diffuse, half3(1,1,1), saturate(_AsUnlit));
        float occlusion = saturate(upperLid * _UpperLidOcclusion);
        half3 colour = albedo * diffuse * lerp(half3(1,1,1), _OcclusionColor.rgb, occlusion);
        float specular = HeroEyeCornea(corneaNormal, view, lightDirection) * coat * max(0, _HighlightStrength);
        colour += key.color * key.distanceAttenuation * key.shadowAttenuation * _HighlightColor.rgb * specular * (1 - occlusion);
        return half4(MixFog(colour, input.fog), 1);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+5" }
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull] ZWrite On ZTest LEqual Blend One Zero
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HeroEyeVertex
            #pragma fragment HeroEyeFragment
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
            Cull [_Cull] ZWrite On ZTest LEqual ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HeroEyeDepthVertex
            #pragma fragment HeroEyeDepthFragment
            #pragma multi_compile_instancing
            half4 HeroEyeDepthFragment(HeroEyeDepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); HeroEyeClip(input.rawUV);
                return half4(input.positionCS.z,0,0,0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull [_Cull] ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HeroEyeDepthVertex
            #pragma fragment HeroEyeNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 HeroEyeNormalsFragment(HeroEyeDepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); HeroEyeClip(input.rawUV);
                float3 normal = HeroEyeSafeNormal(input.normalWS, float3(0,0,1));
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = saturate(PackNormalOctQuadEncode(normal) * .5 + .5);
                    return half4(PackFloat2To888(oct),0);
                #else
                    return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull] ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HeroEyeShadowVertex
            #pragma fragment HeroEyeShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            HeroEyeDepthVaryings HeroEyeShadowVertex(HeroEyeAttributes input)
            {
                HeroEyeDepthVaryings output = (HeroEyeDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = HeroEyeSafeNormal(_LightPosition - positionWS, float3(0,1,0));
                #else
                    float3 direction = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direction));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                output.rawUV = input.uv; output.normalWS = normalWS;
                return output;
            }
            half4 HeroEyeShadowFragment(HeroEyeDepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); HeroEyeClip(input.rawUV);
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
