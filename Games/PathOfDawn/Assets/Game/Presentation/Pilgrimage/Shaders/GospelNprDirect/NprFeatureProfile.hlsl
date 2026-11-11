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

// Outline must sample its own source alpha; a white default fills hair cutouts.
#define LIL_FEATURE_OutlineTex

// Retain the existing shadow optimization setting; no forward-add feature is
// introduced. Main/additional-light URP variants otherwise stay in the template.
#define LIL_OPTIMIZE_APPLY_SHADOW_FA

#endif
