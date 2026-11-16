// Project-owned feature profile for the actually prepared Gospel NPR actors.
// Uses the pinned lilToon lighting/input code; it does not replace that renderer.
// Reviewed material usage and source references are recorded beside this draft.
#ifndef FIRSTGAME_GOSPEL_NPR_FEATURE_PROFILE_INCLUDED
#define FIRSTGAME_GOSPEL_NPR_FEATURE_PROFILE_INCLUDED

// Main texture, source UV scale/offset, color and alpha remain upstream core.
// Preserve the authored HSVG transform used by the iris/hair materials.
#define LIL_FEATURE_MAIN_TONE_CORRECTION

// Preserve authored normal-based one-band shading and actual main-light shadows.
// The second/third authored shade alpha is zero. No face-SDF texture is enabled.
#define LIL_FEATURE_SHADOW
#define LIL_FEATURE_RECEIVE_SHADOW
#define LIL_FEATURE_OUTLINE_RECEIVE_SHADOW

// The pinned renderer's restrained light-facing rim and directional hair
// specular are prepared per material role. Skin/eyes do not receive these
// highlights; no matcap, emission or environment-cubemap substitute is added.
#define LIL_FEATURE_RIMLIGHT
#define LIL_FEATURE_RIMLIGHT_DIRECTION
#define LIL_FEATURE_REFLECTION
#define LIL_FEATURE_ANISOTROPY

// lilToon's documented override keeps its actual reflection/specular code.
// These source meshes already have UV-aligned tangents; no tangent map is
// authored. The upstream default normal-map fallback points along N on GLES,
// which would make OrthoNormalize(N,N) degenerate. Use the real mesh tangent
// with an explicit finite orthogonal fallback instead of sampling that map.
#define OVERRIDE_ANISOTROPY \
    if(_UseAnisotropy) \
    { \
        float3 nprHairTangent = fd.TBN[0] - fd.N * dot(fd.N, fd.TBN[0]); \
        if(dot(nprHairTangent, nprHairTangent) < 0.000001) \
            nprHairTangent = cross(abs(fd.N.y) < 0.95 ? float3(0,1,0) : float3(1,0,0), fd.N); \
        fd.T = nprHairTangent * rsqrt(max(dot(nprHairTangent, nprHairTangent), 0.000001)); \
        fd.B = cross(fd.N, fd.T); \
        fd.anisotropy = clamp(_AnisotropyScale, -0.85, 0.85); \
        if(_Anisotropy2Reflection) fd.perceptualRoughness = saturate(1.2 - abs(fd.anisotropy)); \
    }

// Outline must sample its own source alpha; a white default fills hair cutouts.
#define LIL_FEATURE_OutlineTex

// Retain the existing shadow optimization setting; no forward-add feature is
// introduced. Main/additional-light URP variants otherwise stay in the template.
#define LIL_OPTIMIZE_APPLY_SHADOW_FA

#endif
