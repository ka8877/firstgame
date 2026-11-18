#ifndef FIRSTGAME_GOSPEL_HERO_HAIR_FLOW_INCLUDED
#define FIRSTGAME_GOSPEL_HERO_HAIR_FLOW_INCLUDED

// Project-authored functions. No lilToon package-private fields or uniforms.
// UV U/V are the *mesh* tangent/bitangent before the strand-flow selection.
float3 GospelHairSafeUnit(float3 value, float3 fallback)
{
    float lengthSquared = dot(value, value);
    // The upper bound also makes NaN/Infinity select the finite fallback.
    if (lengthSquared > 0.00000001 && lengthSquared < 100000000.0)
        return value * rsqrt(lengthSquared);
    return fallback;
}

float2 GospelHairSafeFlow(float2 value)
{
    float lengthSquared = dot(value, value);
    if (lengthSquared > 0.00000001 && lengthSquared < 100000000.0)
        return value * rsqrt(lengthSquared);
    return float2(1, 0);
}

// mode 0: preserve U; mode 1: strands follow V; mode 2: explicit UV XY vector.
// This does not sample a flow map. Per-texture flow needs a separately authored
// sampling contract; a constant-vector property must not be labelled a map.
void GospelHairResolveFrame(float3 normalWS, float3 tangentUWS,
    float3 tangentVWS, float mode, float2 authoredUV, float rotationDegrees,
    out float3 strandWS, out float3 acrossWS)
{
    float3 n = GospelHairSafeUnit(normalWS, float3(0, 1, 0));
    float3 fallbackU = GospelHairSafeUnit(
        cross(abs(n.y) < 0.95 ? float3(0, 1, 0) : float3(1, 0, 0), n),
        float3(1, 0, 0));
    float3 u = GospelHairSafeUnit(tangentUWS - n * dot(n, tangentUWS), fallbackU);
    // Keep the actual mesh handedness for V. Do not assume +cross(N,U) when
    // mirrored UVs or an odd-negative object transform supplied the opposite.
    float3 vProjected = tangentVWS - n * dot(n, tangentVWS);
    vProjected -= u * dot(u, vProjected);
    float3 v = GospelHairSafeUnit(vProjected, cross(n, u));
    float2 flow = mode < 0.5 ? float2(1, 0)
        : mode < 1.5 ? float2(0, 1) : GospelHairSafeFlow(authoredUV);
    float angle = abs(rotationDegrees) < 1000000.0 ? radians(rotationDegrees) : 0;
    float sine, cosine;
    sincos(angle, sine, cosine);
    flow = float2(cosine * flow.x - sine * flow.y,
        sine * flow.x + cosine * flow.y);
    strandWS = GospelHairSafeUnit(u * flow.x + v * flow.y, u);
    // The anisotropic lobe uses an orthonormal *shading* frame. Its handedness
    // need not equal the UV frame: V was already selected with the mesh sign.
    acrossWS = GospelHairSafeUnit(cross(n, strandWS), cross(n, u));
}

#endif
