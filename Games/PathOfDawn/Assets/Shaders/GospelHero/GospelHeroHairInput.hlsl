#ifndef FIRSTGAME_GOSPEL_HERO_HAIR_INPUT_INCLUDED
#define FIRSTGAME_GOSPEL_HERO_HAIR_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "GospelHairFlow.hlsl"

// One shared layout for every pass. No UsePass or package-private CBUFFER.
CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float4 _MainTexHSVG;
    float4 _Color;
    float4 _ShadowColor;
    float4 _ReflectionColor;
    float4 _RimColor;
    float4 _GospelHairFlowVector;
    float _GospelHairFlowMode;
    float _GospelHairFlowRotation;
    float _AlphaClip;
    float _Cutoff;
    float _UseBumpMap;
    float _BumpScale;
    float _UseShadow;
    float _ShadowStrength;
    float _ShadowBorder;
    float _ShadowBlur;
    float _ShadowReceive;
    float _AsUnlit;
    float _LightMinLimit;
    float _LightMaxLimit;
    float _UseReflection;
    float _ApplySpecular;
    float _Smoothness;
    float _UseAnisotropy;
    float _AnisotropyScale;
    float _AnisotropyTangentWidth;
    float _AnisotropyBitangentWidth;
    float _AnisotropyShift;
    float _AnisotropySpecularStrength;
    float _Anisotropy2ndSpecularStrength;
    float _UseRim;
    float _RimBorder;
    float _RimBlur;
    float _RimFresnelPower;
    float _RimMainStrength;
CBUFFER_END

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

float2 GospelHairUV(float2 uv) { return uv * _MainTex_ST.xy + _MainTex_ST.zw; }
void GospelHairClip(float2 uv)
{
    if (_AlphaClip > 0.5)
        clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, GospelHairUV(uv)).a * _Color.a - _Cutoff);
}

float3 GospelHairRGBToHSV(float3 colour)
{
    float4 k = float4(0, -1.0 / 3.0, 2.0 / 3.0, -1);
    float4 p = lerp(float4(colour.bg, k.wz), float4(colour.gb, k.xy), step(colour.b, colour.g));
    float4 q = lerp(float4(p.xyw, colour.r), float4(colour.r, p.yzx), step(p.x, colour.r));
    float d = q.x - min(q.w, q.y);
    return float3(abs(q.z + (q.w - q.y) / (6 * d + 0.000001)), d / (q.x + 0.000001), q.x);
}
float3 GospelHairHSVToRGB(float3 hsv)
{
    float3 p = abs(frac(hsv.xxx + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3);
    return hsv.z * lerp(float3(1, 1, 1), saturate(p - 1), hsv.y);
}
float3 GospelHairTone(float3 colour)
{
    // Retains the H/S/V/gamma property interface. This is project-authored
    // tone correction, not a claim of bit-identical lilToon colour arithmetic.
    float3 hsv = GospelHairRGBToHSV(max(colour, 0));
    hsv.x = frac(hsv.x + _MainTexHSVG.x);
    hsv.y = saturate(hsv.y * max(_MainTexHSVG.y, 0));
    hsv.z *= max(_MainTexHSVG.z, 0);
    return pow(max(GospelHairHSVToRGB(hsv), 0), max(_MainTexHSVG.w, 0.001));
}

float3 GospelHairNormal(float2 uv, float3 normalWS, float4 tangentWS)
{
    float3 n = GospelHairSafeUnit(normalWS, float3(0, 1, 0));
    float3 t, b;
    GospelHairResolveFrame(n, tangentWS.xyz, cross(n, tangentWS.xyz) * tangentWS.w,
        0, float2(1, 0), 0, t, b);
    b *= tangentWS.w < 0 ? -1 : 1;
    if (_UseBumpMap > 0.5)
    {
        float3 localNormal = UnpackNormalScale(
            SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, GospelHairUV(uv)), _BumpScale);
        n = GospelHairSafeUnit(t * localNormal.x + b * localNormal.y + n * localNormal.z, n);
    }
    return n;
}

#endif
