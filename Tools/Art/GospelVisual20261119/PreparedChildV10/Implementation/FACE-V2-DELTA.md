# Face shader V2 selection

Install **only** `Shaders/FirstGameGospelHeroFaceV2.shader` as the product face shader (the exact product filename may remain `FirstGameGospelHeroFace.shader`). V1 is preserved as the earlier source. Both declare `FirstGame/GospelHero/FaceSdf`; do not install both.

V2 changes the normal fallback to normalized `NdotL * .5 + .5`, matching the inherited lilToon/Hair `_ShadowBorder` property scale, and defaults that threshold to .5. The angular-SDF path, public property names, rig, expressions and material API are unchanged. This avoids interpreting the existing .44 source threshold on a signed -1..1 range. Source-only correction; no actual visual or compilation result is claimed.

V2 SHA256: `644774623c29250aa576e8a9d13f40d33381cb4e46127d7b518c995c1359bae8`.
