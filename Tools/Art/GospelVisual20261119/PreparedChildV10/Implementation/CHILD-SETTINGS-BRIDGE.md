# Actual bake report → Editor SettingsV2

`build-child-settings-v1.py` is a pure standard-library converter. Root runs it only after a successful actual child bake emits the complete model/report. The source author did not execute it or read an unfinished bake as a success.

```sh
python3 /ABS/CharacterImplementation/build-child-settings-v1.py \
  --bake-report /ABS/successful-new-bake/child-bake-report.json \
  --bake-report-sha256 FULL_ACTUAL_REPORT_SHA \
  --baker-source-sha256 FULL_ACTUAL_BAKER_SOURCE_SHA \
  --controller-asset Assets/Resources/Art/GospelNpr/Animation/NprLocomotion.controller \
  --output-version NEW_REVIEWED_VERSION \
  --hair-flow-mode 0 \
  --flat-normal-pixels /ABS/original-normal-pixels01.json \
  --flat-normal-pixels-sha256 161f680e93a1b6b6adef575465bfff25be973057c88f1a0dcf9ad5c6da1dcf32 \
  --output /ABS/NEW-child-settings.json
```

`--hair-flow-mode` is explicit: 0 preserves U, 1 uses V, 2 uses `--hair-uv-flow X Y`. This source does not infer strand flow from a texture filename. `--brow-linear-tint R G B` is an optional reviewed color multiplier for the original brow albedo/factor. Its default is **unchanged**. It changes no source PNG, UV, morph or geometry, and its exact choice appears in the bridge sidecar. Brown-hair export factors already include the linear brown multiplier; the bridge uses those once and leaves `artisticTintSrgb` at identity, preventing double tint or linear/SRGB confusion.

Eyes remain Legacy by default. After actual topology/fit review, Root may repeat `--verified-eye-material N00_000_00_EyeIris_00_EYE` and/or `--verified-eye-material N00_000_00_EyeWhite_00_EYE`. The highlight-only material cannot opt in through this bridge. Source BLEND retains the legacy shader even when a material is listed; MASK keeps its actual cutoff. These choices do not create a curved eyeball or prove fit by themselves.

The converter verifies the real report pin, selected original VRM/FBX pins, actual original inspection pin, 109 actual bone aliases, Basis+122 morph names/defaults and actual source preset binds. Exported original and derived material names come from the report's **active FBX export** slots, including the export-only hair shim mapping; they are not guessed from beauty-render names.

It parses the exact original GLB texture table to resolve texture indices to image indices. All new color/normal texture FilePins are verified against actual extracted originals. Genuine normal maps retain raw `scale` and require the same UV0 scale/offset as their base texture. Rotated/non-UV0/different-normal-transform inputs fail until actually baked into geometry. Normal import uses SettingsV2; install `GospelHeroChildPreparationV2.cs` content as the one product `GospelHeroChildPreparation.cs`, not as a duplicate class.

Original FaceSkin image SHA `f8f4e1d…db85e53` is proven by Root's exact pixel report to be one uniform `(128,128,255)` color, 1024², with zero spatial variation; generic flat image `d76fc537…2cc95` is uniform `(127,127,255)`, 8². The bridge omits those normals only for new Face/Eyes surfaces after verifying the pinned report's exact hash, extrema, color count and pixel count and matching the original image SHA. It records `uniform-original-normal-has-no-spatial-detail` with evidence. This does not claim bit-identical constant tangent offsets or that the new face shader implements bump mapping. Nonflat Body/Hair maps are retained. Source images remain unchanged.

Color copies are separated by opaque versus alpha use, and normal copies use their own import keys. This prevents Unity's alpha RGB dilation or NormalMap import from changing an opaque color use of the same source image. New config and `.bridge.json` audit files use CreateNew semantics. No Unity, Blender, rendering, model edits, image editing, project files, runtime state or existing receipts are touched by this converter.

SDF remains false in this first settings bridge. The independent geometry-bake compatibility path can be enabled only after its actual UV/visibility report is accepted and bound to its exact new PNG. Neither config generation nor a successful Editor preparation is visual, Native/Web, performance or release approval.
