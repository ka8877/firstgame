Shader "FirstGame/GospelHero/Hair"
{
    Properties
    {
        _MainTex("Hair albedo / opacity", 2D) = "white" {}
        _Color("Hair colour", Color) = (1,1,1,1)
        _MainTexHSVG("Hue / saturation / value / gamma", Vector) = (0,1,1,1)
        [Normal] _BumpMap("Authored strand normal", 2D) = "bump" {}
        [Toggle] _UseBumpMap("Use authored normal", Float) = 0
        _BumpScale("Normal scale", Range(-1,1)) = 0.3
        [Enum(Preserve_U,0,Strands_V,1,Authored_UV_Vector,2)] _GospelHairFlowMode("Strand direction", Float) = 0
        _GospelHairFlowVector("Authored UV flow XY", Vector) = (1,0,0,0)
        _GospelHairFlowRotation("Additional UV flow rotation", Range(-180,180)) = 0
        [Toggle] _AlphaClip("Clip authored hair cards", Float) = 0
        _Cutoff("Opacity cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _ZWrite("Depth write", Float) = 1
        [Toggle] _UseShadow("Toon shading", Float) = 1
        _ShadowStrength("Shade strength", Range(0,1)) = 0.72
        _ShadowColor("Shade colour", Color) = (0.57,0.47,0.48,1)
        _ShadowBorder("Shade border", Range(0,1)) = 0.5
        _ShadowBlur("Shade softness", Range(0.001,0.5)) = 0.16
        _ShadowReceive("Receive cast shadows", Range(0,1)) = 1
        _AsUnlit("Unlit blend", Range(0,1)) = 0.03
        _LightMinLimit("Minimum light", Range(0,1)) = 0.12
        _LightMaxLimit("Maximum light", Range(0.1,4)) = 1.2
        [Toggle] _UseReflection("Enable authored highlight", Float) = 1
        [Toggle] _ApplySpecular("Apply specular", Float) = 1
        _ReflectionColor("Highlight colour / strength", Color) = (0.9,0.75,0.6,0.5)
        _Smoothness("Highlight smoothness", Range(0,1)) = 0.4
        [Toggle] _UseAnisotropy("Strand highlight", Float) = 1
        _AnisotropyScale("Anisotropy amount", Range(-1,1)) = 0.65
        _AnisotropyTangentWidth("Strand lobe width", Range(0.05,10)) = 1.2
        _AnisotropyBitangentWidth("Across lobe width", Range(0.05,10)) = 0.42
        _AnisotropyShift("Strand lobe shift", Range(-1,1)) = 0.1
        _AnisotropySpecularStrength("Strand highlight strength", Range(0,2)) = 0.35
        _Anisotropy2ndSpecularStrength("Secondary strand highlight", Range(0,2)) = 0
        [Toggle] _UseRim("Light-facing silhouette rim", Float) = 1
        _RimColor("Rim colour / strength", Color) = (0.67,0.49,0.31,0.1)
        _RimBorder("Rim border", Range(0,1)) = 0.38
        _RimBlur("Rim softness", Range(0.001,0.5)) = 0.24
        _RimFresnelPower("Rim falloff", Range(0.1,8)) = 2.4
        _RimMainStrength("Main-light rim direction", Range(0,1)) = 0.65
    }
    HLSLINCLUDE
    #include "GospelHeroHairInput.hlsl"
    struct HairAttributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 tangentOS : TANGENT;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct HairVaryings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 positionWS : TEXCOORD1;
        float3 normalWS : TEXCOORD2;
        float4 tangentWS : TEXCOORD3;
        float fog : TEXCOORD4;
        float4 screenPosition : TEXCOORD5;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };
    HairVaryings HairVertex(HairAttributes input)
    {
        HairVaryings output = (HairVaryings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
        output.positionCS = TransformWorldToHClip(output.positionWS);
        output.screenPosition = ComputeScreenPos(output.positionCS);
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz),
            input.tangentOS.w * GetOddNegativeScale());
        output.uv = input.uv;
        output.fog = ComputeFogFactor(output.positionCS.z);
        return output;
    }
    float HairStrandLobe(float3 n, float3 strand, float3 halfDirection,
        float shift, float exponent, float acrossExponent)
    {
        float3 shifted = GospelHairSafeUnit(strand + n * shift, strand);
        float cosine = dot(shifted, halfDirection);
        float sine = sqrt(saturate(1 - cosine * cosine));
        return pow(sine, exponent) * pow(saturate(dot(n, halfDirection)), acrossExponent);
    }
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HairVertex
            #pragma fragment HairFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            half4 HairFragment(HairVaryings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                GospelHairClip(input.uv);
                float4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, GospelHairUV(input.uv));
                float3 albedo = GospelHairTone(texel.rgb) * _Color.rgb;
                float side = IS_FRONT_VFACE(frontFace, 1.0, -1.0);
                float3 n = GospelHairNormal(input.uv, input.normalWS, input.tangentWS) * side;
                float3 t, b;
                GospelHairResolveFrame(n, input.tangentWS.xyz,
                    cross(GospelHairSafeUnit(input.normalWS, n) * side, input.tangentWS.xyz) * input.tangentWS.w,
                    _GospelHairFlowMode, _GospelHairFlowVector.xy, _GospelHairFlowRotation, t, b);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    Light light = GetMainLight(input.screenPosition);
                #else
                    Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                #endif
                float facing = saturate(dot(n, light.direction) * 0.5 + 0.5);
                float blur = max(_ShadowBlur, 0.001);
                float band = smoothstep(_ShadowBorder - blur, _ShadowBorder + blur, facing);
                float3 shade = lerp(_ShadowColor.rgb, float3(1,1,1), band);
                shade = lerp(float3(1,1,1), shade, saturate(_UseShadow * _ShadowStrength));
                float received = lerp(1, light.shadowAttenuation, saturate(_ShadowReceive));
                float3 illumination = max(SampleSH(n), 0) * 0.45
                    + light.color * light.distanceAttenuation * received * shade;
                illumination = clamp(illumination, _LightMinLimit, max(_LightMaxLimit, _LightMinLimit));
                float3 colour = albedo * lerp(illumination, float3(1,1,1), saturate(_AsUnlit));
                float3 view = GospelHairSafeUnit(GetWorldSpaceViewDir(input.positionWS), n);
                float3 h = GospelHairSafeUnit(view + light.direction, n);
                float power = lerp(12, 180, saturate(_Smoothness));
                float width = max(_AnisotropyTangentWidth, 0.05);
                float acrossPower = max(1, _AnisotropyBitangentWidth * 3);
                float3 flow = _AnisotropyScale < 0 ? b : t;
                float anisotropic = HairStrandLobe(n, flow, h, _AnisotropyShift,
                    max(1, power / (width * width)), acrossPower);
                float secondary = HairStrandLobe(n, flow, h, -_AnisotropyShift,
                    max(1, power * 0.3), acrossPower);
                float isotropic = pow(saturate(dot(n, h)), power);
                float specular = lerp(isotropic, anisotropic,
                    saturate(abs(_AnisotropyScale) * _UseAnisotropy));
                specular = specular * _AnisotropySpecularStrength + secondary * _Anisotropy2ndSpecularStrength;
                colour += light.color * _ReflectionColor.rgb * _ReflectionColor.a * specular
                    * saturate(_UseReflection * _ApplySpecular) * received * saturate(dot(n, light.direction));
                float rimBase = pow(1 - saturate(abs(dot(n, view))), max(_RimFresnelPower, 0.1));
                float rim = smoothstep(_RimBorder - max(_RimBlur, 0.001),
                    _RimBorder + max(_RimBlur, 0.001), rimBase);
                rim *= lerp(1, saturate(dot(n, light.direction)), saturate(_RimMainStrength));
                // Keep the edge light in the hair's palette on both sides of a strand.
                colour += lerp(albedo, _RimColor.rgb, 0.32) * _RimColor.a * rim * saturate(_UseRim) * received;
                return half4(MixFog(colour, input.fog), texel.a * _Color.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "SHADOW_CASTER"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HairShadowVertex
            #pragma fragment HairShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            HairVaryings HairShadowVertex(HairAttributes input)
            {
                HairVaryings output = HairVertex(input);
                float3 direction = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    direction = normalize(_LightPosition - output.positionWS);
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS,
                    GospelHairSafeUnit(output.normalWS, float3(0,1,0)), direction));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }
            half4 HairShadowFragment(HairVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                GospelHairClip(input.uv); return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DEPTHONLY"
            Tags { "LightMode"="DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HairVertex
            #pragma fragment HairDepthFragment
            #pragma multi_compile_instancing
            half4 HairDepthFragment(HairVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                GospelHairClip(input.uv); return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DEPTHNORMALS"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HairVertex
            #pragma fragment HairDepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 HairDepthNormalsFragment(HairVaryings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                GospelHairClip(input.uv);
                float3 n = GospelHairNormal(input.uv, input.normalWS, input.tangentWS) * IS_FRONT_VFACE(frontFace, 1.0, -1.0);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
