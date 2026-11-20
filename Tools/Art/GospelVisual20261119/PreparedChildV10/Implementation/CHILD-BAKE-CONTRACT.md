# Child-first artist-base bake contract

These are new source proposals. Only Root runs Blender/imports models, owns source assets and records actual output/review. No Blender execution, source-model read, compilation or visual approval was performed while writing these files.

## Frozen inspector V1

`bake-hinzka-child-v1.py` is inspection only. It fails before any input/output if `--inspect-only` is absent or `--contract` is supplied. It imports the exact selected GLB/VRM into a new Blender 4.3 background scene, enumerates all actual meshes/109-bone armature/raw humanoid semantics/materials/source expressions/native groups/morph defaults, and extracts embedded PNG/JPEG bytes without pixel edits. It does not normalize, age, warp, tailor, tint, export or generate clothing.

```sh
blender --background --factory-startup --python /ABS/CharacterImplementation/bake-hinzka-child-v1.py -- \
  --source /ABS/selected-source.vrm \
  --source-sha256 7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99 \
  --output-root /ABS/NEW-inspection-directory --inspect-only
```

The parent of `--output-root` must exist; the output directory must not exist. All paths must be absolute and have no symlink components. V1 emits `source.glb`, `Textures/source-image-NNN.png|jpg`, `inspection.json`, and `inspection-complete.json`. Source bytes and image bytes are immutable inputs; the JSON contains actual imported values rather than guessed bone/material names. Select the three artist mesh objects from the report; debug collider meshes are not automatically admitted.

## Source proposal V2

`bake-hinzka-child-v2.py` is a separate concrete Blender 4.3 bake entrypoint, subject to Root source review and actual simulation/import/render validation. It does not change V1. It requires an exact JSON contract before producing a model.

```sh
blender --background --factory-startup --python /ABS/CharacterImplementation/bake-hinzka-child-v2.py -- \
  --source /ABS/selected-source.vrm \
  --source-sha256 7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99 \
  --contract /ABS/root-reviewed-child-contract.json \
  --inspection /ABS/actual-V1-inspection/inspection.json \
  --inspection-sha256 FULL_ROOT_SUPPLIED_INSPECTION_JSON_SHA256 \
  --output-root /ABS/NEW-child-bake-directory
```

`--save-blend` additionally creates a new beauty-preview `.blend`. It is off by default. `--inspect-only` can enumerate V2's source interfaces without a bake, but production use must choose exactly one of `--inspect-only` and `--contract`.

The following is a schema example, not an admitted source contract. Root must replace the three object names and face glTF mesh index with exact V1 inspection values. The semantic map is extracted from the actual raw VRM0 `humanBones` records; do not substitute manually guessed joints.

```json
{
  "schema": 1,
  "sourceSha256": "7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99",
  "expectedBones": 109,
  "objects": { "face": "ROOT_EXACT_FACE_OBJECT", "body": "ROOT_EXACT_BODY_OBJECT", "hair": "ROOT_EXACT_HAIR_OBJECT" },
  "faceGltfMeshIndex": -1,
  "hairMaterials": ["N00_000_00_HairBack_00_HAIR", "N00_000_Hair_00_HAIR"],
  "hairMaterialSlots": { "body": ["N00_000_00_HairBack_00_HAIR"], "hair": ["ROOT_EXACT_HAIR_OBJECT_MATERIAL_NAMES"] },
  "bodySkinMaterials": ["N00_000_00_Body_00_SKIN"],
  "removeBodyClothingMaterials": ["N00_001_01_Bottoms_01_CLOTH", "N00_004_01_Shoes_01_CLOTH", "N00_004_01_Tops_01_CLOTH"],
  "collisionBodyMaterials": ["N00_000_00_Body_00_SKIN", "N00_004_01_Tops_01_CLOTH"],
  "weightDonorBodyMaterials": ["N00_000_00_Body_00_SKIN", "N00_001_01_Bottoms_01_CLOTH", "N00_004_01_Shoes_01_CLOTH", "N00_004_01_Tops_01_CLOTH"],
  "pattern": { "rows": 28, "columns": 32, "sleeveRows": 14, "hemAboveKneeRatio": 0.018, "sideEaseRatio": 0.018, "sleeveArmFraction": 0.6 },
  "simulation": { "frames": 90, "quality": 12, "maximumSeamGap": 0.018 }
}
```

Exact existing material names are required, with no missing-material fallback. Root's actual V1 inspection JSON is required by full SHA, original source path/SHA/bytes, and the actual imported per-object native/morph/topology/UV hashes. HairBack belongs to Body in the actual selected source and must remain after removing the three modern clothing materials. The explicit Body/Hair material-slot map tints all source hair slots; its union must exactly cover `hairMaterials`. Root must replace the example Hair-object list with actual V1 values. A retained Body HairBack region hash additionally protects its polygon vertices/smooth flags/corner UV/corner normals across modern-face deletion. Collision material selection must provide a real torso/waist envelope; Root must check the actual source shape before admitting these example choices. Native weight donor triangles are captured before modern clothing faces are removed, so an explicit original Tops/Bottoms/Shoes donor remains available if underlying skin is cut out. Donor vertex positions, all original named influences and all morph data remain unchanged. These donors use the original 109-bone palette; derived garment weights barycentrically interpolate all actual influences without truncation.

V2 admits only the supplied 4,709-vertex/8,280-polygon Face and 123 shape keys (Basis plus 122). It requires the actual `Fcl_EYE_Close`, `Fcl_MTH_A`, `Fcl_MTH_I`, `Fcl_MTH_O` aliases, exact source morph target order, and real VRM expression groups `i`, `o`, `fun`, `sorrow`, `joy`. Output presets map `ih→i`, `oh→o`, `relaxed→fun`, `sad→sorrow`, `happy→joy` using the original groups/binds/weights. Missing, renamed, reordered or cross-mesh bindings fail rather than producing an invented expression.

## Actual modeling and preservation boundaries

The artist's face, hair silhouette, body proportions, all 109 rest bones, source native influence arrays, 122 face morphs and defaults remain intact. Preview orientation changes only through a shared root rigid π rotation around Blender Z (source +Y becomes preview −Y). Height becomes 1.42 m through uniform shared-root scaling. No local face/body/head-age deformation or old parametric sculpt is used. Modern clothing faces are removed only by the contract's exact source material names; body positions/native weights/morph payload are compared before and after. Complete face/hair hashes include topology, UV layers, custom-normal flag/corner normals, morph data/defaults and native named weights.

The new tunic uses two discretized front/back body pattern panels and four sleeve panels. Actual loose sewing edges are passed to Blender Cloth with `use_sewing_springs`, gravity, a body collision proxy and self-collision. Frames are evaluated sequentially; the final simulated mesh is retained. Only actual seam pairs are welded after the measured maximum seam gap passes the contract. No periodic ring/fold displacement function is used. Millimetre fabric thickness is added before binding.

The belt is functional in the bake: body-derived waist cross-section dimensions fit a waist preform/pin with weight 1 during simulation. The report contains unconstrained panel width, actual belt constraint width/depth, settled width/depth and pin counts. The visible woven belt follows that settled constrained contour. Neckline, cuff and hem facings follow settled surface tangents/normals. Actual seam closure, folds, fit and neck silhouette remain subject to Root's visual review.

Sandals select actual retained body-skin vertices influenced by the source foot/toe bones and bounded above the foot landmark, with explicit sole/strap dimensions and a calf-height rejection. A source without enough real retained bare-foot geometry fails. No fake skin reconstruction is substituted.

Brown hair uses copied materials and a color multiplier; source hair mesh/UVs/PNG bytes stay unchanged. The beauty graph multiplies the actual source signal. For FBX export only, that added node is bypassed so the original signal plus diffuse tint can be represented, then restored. The report provides exact original image SHA/index/path plus tint and HSVG identity for the new Unity material API to bind explicitly. Root must verify actual FBX texture/color interpretation; FBX render equivalence is not assumed.

## Outputs and actual acceptance

Production success emits `JesusChild-Hinzka-Sewn-v2.fbx` and `child-bake-report.json`, together with immutable `source.glb`, original contract bytes, extracted original images and inspection JSON. The report includes source/FBX full SHA and sizes, all source bone rest records and normalized humanoid semantics, all actual face morph names/defaults, raw VRM expressions, source material factors/raw maps/images, face/hair identity hashes, retained body payload hashes, cloth/functional-belt/footwear statistics, native derived-weight metrics, source mesh exclusions and explicit unverified Unity/art flags. Optional `.blend` bytes/SHA are included only when requested.

All output directories/files are new. A failure retains partial output for diagnosis and does not claim a production model, accepted folds or completed Unity import. The script changes no repository Assets, native tests, runtime state, source files or old proof. Root must independently check 109-bone import, all 122 morphs/defaults, source normals/UV/native weights, forward orientation, real source preset expressions, clip retargeting/movement, cloth penetration/neck/foot silhouettes, texture references/color, game-camera appearance and actual performance. This source proposal makes no commercial-game quality parity claim or license expansion claim.
