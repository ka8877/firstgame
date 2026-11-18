# Successful child-model construction preview

Source draft `render-success-child-static-v1.py` adapts the frozen failed-cloth
diagnostic renderer. Root supplies the successful bake report, saved `.blend`,
original source and exact baker source pins. The author has not read actual
model binaries, actual bake reports, images or runtime outputs for this task.

```text
blender --background --factory-startup --python /absolute/render-success-child-static-v1.py --
  --blend /absolute/successful.blend --blend-sha256 ACTUAL_SHA
  --report /absolute/child-bake-report.json --report-sha256 ACTUAL_SHA
  --baker-source /absolute/bake-hinzka-child-v10.py --baker-source-sha256 ACTUAL_SHA
  --source /absolute/original.vrm
  --output-root /absolute/new-render-directory --threads 4
```

`--source` defaults to the pinned report's `sourceFile`. Original source SHA is
fixed to the admitted V110 source, and its byte length must match the report.
The report's `optionalBlend` must pin the supplied blend exactly. Only schema2
JesusChild with exact preserved face/hair/body-native, original109-bone rest
rig, source-preserved artist upper and 1.42m/-Y preview contracts is accepted.

The script appends unlinked object datablocks, rejects any active Cloth
modifier, links only reported shipping meshes/rig/ancestors, and performs one
safe dependency-graph update before capturing world-coordinate baselines.
It never advances frames, simulates, poses, edits geometry/materials or saves
the blend. The artist upper intentionally has the original Body native vertex
buffer, so it is explicitly excluded from the Body-native hash lookup.

Native geometry, all shipping world coordinates, original109-bone rest data,
native object/pose channels, material graphs and input file bytes are guarded
before and after rendering. Own cameras, lights and world provide three CPU
Cycles32 images: front and quarter fullbody at1280, front face at1024. Denoising
and OIDN remain disabled. All outputs use `CONSTRUCTION_PREVIEW` labels and are
new files. Output PNGs depict the saved Blender materials, which precede the
Unity-only linen texture/brow configuration. They are not Unity game captures,
final visual quality or aesthetic approval.

The successful source body's retained topology differs from the original
modern clothing by the admitted material-face filter. Exact body-native
positions, full weights and morph payload are still matched to the successful
report; all current shipping topology/UV/normals are additionally guarded
against alteration by this renderer.
