Shader "FirstGame/Pilgrimage/Galilee Woven Cloth"
{
    Properties
    {
        [MainTexture] _MainTex("Weave value", 2D) = "white" {}
        [Normal] _BumpMap("Yarn relief", 2D) = "bump" {}
        _UseBumpMap("Use woven normal", Range(0,1)) = 0
        _BumpScale("Yarn relief strength", Range(0,1)) = .22
        [MainColor] _Color("Dyed yarn", Color) = (.65,.48,.34,1)
        _ThreadColor("Border yarn", Color) = (.68,.57,.43,1)
        [Enum(Plain,0,Mantle,1,Binding,2)] _Border("Woven border", Float) = 0
        _Wool("Wool weight", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
    TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST, _BumpMap_ST;
        float _UseBumpMap, _BumpScale;
        half4 _Color, _ThreadColor;
        float _Border, _Wool, _Cull;
    CBUFFER_END
    struct Vertex { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 tangent : TANGENT; };
    struct Surface { float4 pos : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; float2 uv : TEXCOORD2; half fog : TEXCOORD3; half4 tangent : TEXCOORD4; };
    Surface Vert(Vertex i)
    {
        Surface o;
        o.world = TransformObjectToWorld(i.pos.xyz);
        o.pos = TransformWorldToHClip(o.world);
        o.normal = TransformObjectToWorldNormal(i.normal);
        o.tangent = half4(TransformObjectToWorldDir(i.tangent.xyz, false), i.tangent.w * GetOddNegativeScale());
        o.uv = i.uv;
        o.fog = ComputeFogFactor(o.pos.z);
        return o;
    }
    // Filter undersampled yarns out at travel distances. Broader folds remain legible.
    float Thread(float cycles)
    {
        return sin(6.283185 * cycles) * saturate(1 - 2 * fwidth(cycles));
    }
    float Stripe(float edge, float center, float width)
    {
        float aa = max(fwidth(edge), .001);
        return 1 - smoothstep(width, width + aa, abs(edge - center));
    }
    half4 Draw(Surface i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
    {
        half3 n = normalize(i.normal) * IS_FRONT_VFACE(facing, 1, -1);
        float3 receiver = i.world + n * .009 + _MainLightPosition.xyz * .012;
        Light sun = GetMainLight(TransformWorldToShadowCoord(receiver));
        float2 uv = i.uv;
        float3 tangent = i.tangent.xyz - n * dot(i.tangent.xyz, n);
        if (_UseBumpMap > .5 && dot(tangent,tangent) > .0001)
        {
            tangent = normalize(tangent);
            float3 yarnNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, TRANSFORM_TEX(uv, _BumpMap)), _BumpScale * .40);
            n = normalize(n * yarnNormal.z + tangent * yarnNormal.x + cross(n,tangent) * i.tangent.w * yarnNormal.y);
        }
        float weaveValue = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(uv,_MainTex)).rgb);
        float warp = Thread(uv.x * 96 + .16 * sin(uv.y * 31));
        float weft = Thread(uv.y * 111 + .09 * sin(uv.x * 55));
        float slub = Thread(uv.y * 13 + .08 * sin(uv.x * 19));
        float pleat = uv.x * 8 + .20 * sin(uv.y * 5.6) + .09 * sin(uv.x * 11 + uv.y * 12);
        float folds = Thread(pleat) + .35 * Thread(pleat * 2 + .2);
        float dye = 1 + (.047 + _Wool * .026) * folds + .014 * slub + .022 * warp + .018 * weft;
        half3 yarn = _Color.rgb * lerp(.94,1.02,weaveValue);
        if (_Border > .5)
        {
            float edge = min(saturate(uv.y), saturate(1 - uv.y));
            bool binding = _Border > 1.5;
            float border = Stripe(edge, binding ? .15 : .07, binding ? .048 : .012)
                         + Stripe(edge, binding ? .31 : .103, binding ? .018 : .006);
            yarn = lerp(yarn, _ThreadColor.rgb, saturate(border) * .72);
        }
        float incidence = dot(n, sun.direction);
        half direct = smoothstep(-.49,.85,incidence) * .69 + .14;
        half3 sky = max(SampleSH(n), half3(.065,.063,.07));
        half shadow = lerp(.66,1,smoothstep(.04,.96,sun.shadowAttenuation));
        half3 light = clamp(.22 + sky * .50 + sun.color * direct * shadow, .24, 1.20);
        return half4(MixFog(yarn * dye * light, i.fog),1);
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
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Draw
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Cull Back
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Shadow
            #pragma fragment Depth
            float3 _LightDirection;
            float4 Shadow(Vertex i) : SV_POSITION
            {
                float3 p = TransformObjectToWorld(i.pos.xyz);
                float3 n = TransformObjectToWorldNormal(i.normal);
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p,n,_LightDirection)));
            }
            half4 Depth() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            half4 Depth() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
