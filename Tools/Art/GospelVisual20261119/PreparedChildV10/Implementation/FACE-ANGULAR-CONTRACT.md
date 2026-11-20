# Geometry angular threshold draft

This is new **source only**, not an executed bake, verified UV layout, accepted character, or game screenshot. Root alone runs Blender/Unity. It generates a geometry-derived directional visibility threshold, **not an artist-painted mask or mathematical signed-distance field**. Its purpose is to reject unsuitable source UVs honestly and retain the normal-lighting fallback when they cannot support the face shader.

Entry point: `bake-face-angular-threshold-v1.py`, Blender **4.3**, fresh unsaved background process. No model/texture/.blend/rig/morph is saved or rewritten. Exact source SHA is pinned to `7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99`; only the material named `N00_000_00_Face_00_SKIN` contributes triangles. Root supplied Face metadata (4709 vertices, 8280 polygons, 123 keys) is context, not a result of this draft. The actual report counts keys including Basis explicitly.

```text
blender --background --factory-startup --python-exit-code 2 --python /absolute/bake-face-angular-threshold-v1.py -- --contract /absolute/inspect-contract.json --contract-sha256 EXACT_SHA --out /absolute/new-inspection-directory --inspect-only
```

The JSON contract is schema `1`, revision `gospel-geometry-angular-threshold-v1`. Required fields: `sourceFile` (absolute original VRM/GLB path), `sourceSha256` (above), `faceObject`, `armatureObject`, `headBone` (actual imported names), `faceSkinMaterial` (above), `axisSpace: "BLENDER_POSE_HEAD_LOCAL"`, `headForwardLocal: [x,y,z]`, `headUpLocal: [x,y,z]`, `unitsToMeters`, `axesVerified`, `unitsVerified`, and all `limits` below. No Unity/VRM convention is guessed. Root must verify that the world frame derived from Blender's pose-head-local vectors equals the Unity animated head frame, including any import axis conversion. Head origin defines the left/right reflection plane; a centered source head origin is therefore required for this contract. `axesVerified`/`unitsVerified` can be false during exploration; that inspection will explicitly be incompatible.

Recommended explicit limits (hard caps in code prevent broad relaxation):

```json
{
  "mirrorCoverageMin": 0.995,
  "mirrorPositionMaxMeters": 0.002,
  "mirrorNormalCosMin": 0.95,
  "uvOverlapFractionMax": 0.0001,
  "nonMonotonicFractionMax": 0.001,
  "frontDarkFractionMax": 0.001,
  "backLitFractionMax": 0.001,
  "uvDegenerateTriangleFractionMax": 0.0
}
```

Optional bounds: `resolution` 32–1024 (default256), odd `yawSamples` 9–65 (default33), `rayEpsilonMeters` .000001–.0001 (default.00002), `maxRays` ≤20 million (default20 million), `maxSeconds` 10–1800 (default600). Output parent must exist; output directory must not exist. No overwrite. A deadline/ray-budget exhaustion is failure, never an implicit pass.

The actual neutral evaluated geometry must retain native vertex/polygon/material/UV correspondence. The code reads authored corner normals and original UV0, computes mirrored barycentric surface position and custom-normal errors at four interior samples per skin triangle, and separately rasterizes UV0 to detect overlapping surfaces/normals. It samples the actual nose/face BVH for positive-head-right light yaw `0…π`, with front lighting and first lit→shadow transition. A dark→lit reversal cannot be represented by a single threshold and is counted explicitly. An initially dark or rear-lit texel is also counted. Invalid source anatomy/UVs are not replaced by a generic gradient. Closed/back-facing head surfaces in the skin material can correctly make this strict full-slot contract fail; this draft does not silently remove those surfaces.

Inspect writes only new `inspection.json`. `shaderCompatible` concerns these bounded tests, not artistic quality; `approvedForShader` remains false. An incompatible actual bake writes `bake.json`, no PNG, and exit2. A subsequent bake omits `--inspect-only` and needs `axesVerified: true`, `unitsVerified: true`, `allowGeometryBake: true`, and `acceptedInspect: {"file": "/absolute/inspection.json", "sha256": "EXACT_SHA"}`. Root pins the new bake contract SHA. Source/native fingerprint and analysis settings must match the accepted inspection, and the real checks run again. Source file SHA, contract SHA and native original face/skin influence/shape/rig fingerprints are recorded before/after. Original mesh vertex weights remain complete and untruncated.

On a compatible accepted bake only, `face-angular-threshold-data.png` is new RGBA8 linear data, R=G=B angular threshold, A=1, direct bottom-left UV0; uncovered texels are0. Unity must import sRGB **off**, alpha-as-transparency **off**, Clamp U/V, bilinear, **no mipmaps**. The matched shader is `FirstGame/GospelHero/FaceSdf`: right-side light uses raw `(u,v)`, left-side light uses `(1-u,v)`, `_FaceSdfFlipSide=0`, comparison `t=.5*(1-dot(headForward,planarLight))`. No `_MainTex_ST` is applied to this data. Quantized and discretely sampled boundaries are an approximation: metadata reports angular sample spacing and byte quantization bound. Geometry baking cannot supply an artist's deliberate shadow design. Bilinear edge sampling outside covered face islands may still require an explicitly audited UV gutter bake before installation; no image is enabled automatically. Real shadow-map occlusion can multiply this response in the shader, which must be evaluated for double-darkening in Unity.

Report integration fields are top-level `shaderCompatible` (bool), `sourceSha256`, `contractSha256` and, **only when an actual compatible accepted bake wrote the PNG**, `dataPngSha256`. The material preparer must pin the report file SHA, require `shaderCompatible == true`, check this selected source SHA, and match `dataPngSha256` against the texture source bytes. An inspection has no PNG hash and cannot enable shading. The accepted inspection must also match `generatorSourceSha256`; changing this draft source requires a new inspection. `approvedForShader` means only admission by this mathematical contract and Root's accepted inspection, never artistic approval or a Unity rendering pass.

This source leaves blendshapes, expressions, story ages, animation, actor IDs and save/story systems unchanged. A neutral-pose geometry map is not verified against all facial expressions. No compilation, runtime, output image, aesthetic approval or release is claimed.
