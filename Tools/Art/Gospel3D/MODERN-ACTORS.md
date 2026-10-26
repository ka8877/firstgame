# Gospel actor art revision

The `gospel-modern-2026-10-26` actors replace the previous copied muscle shirt,
straight crown ridges and isolated cloth strips with tailored linen, continuous
shoulder coverage, woven edging, draped blue/crimson mantles and curved hair
locks. Young Jesus remains the twelve-year-old protagonist of Luke 2, without
an adult beard. Adult Jesus is a separate long-haired, bearded adult model.
Mary is a separate adult adaptation with a narrowed profile, blue mantle,
long linen robe and skinned ivory head veil.

These are artistic interpretations. Neither the models nor their faces are
presented as reconstructed historical likenesses. The licensed Quaternius CC0
anatomy and professionally authored animation remain the basis; no third-party
game model was copied. The existing sources and previous editable actors are
preserved under `Source` and `History/before-modern-2026-10-26`.

Run from the repository root:

```bash
blender -b -t 4 --python Tools/Art/Gospel3D/modernize-actors.py -- --root . --render
blender -b -t 4 --python Tools/Art/Gospel3D/verify-actions.py -- --root .
```

The script always begins with the archived source, making repeated generation
deterministic in geometry. It preserves all 65 bones and the seven take names:
Idle, Walk, Run, JumpStart, JumpAir, JumpLand and Practice. All three FBXs export
the same `GospelRig` armature node and bone transform paths so Generic animation
bindings can be shared. Runtime binding is verified separately in Unity.

`material-spec.json` is the authoritative Unity material mapping. New material
names are `Gospel_BlueMantle`, `Gospel_WovenTrim`, `Gospel_HairWave` and
`Gospel_Veil`. `Gospel_Skin` now references `body-soft-albedo.png`, a generated
colour refinement of the CC0 UV atlas. Existing UVs and the original atlas are
retained. Source and generation metadata are in `soft-skin-generation.json`.

`Previews/Modern` contains direct Blender renders of the current geometry and
retained poses. They are art review evidence, not gameplay screenshots, measured
runtime performance, physical-device tests or user aesthetic approval.

The first actual browser review identified a bare crown from above/rear. The
cause was inward scalp winding, which double-sided Blender previews concealed.
The generator now uses outward faces and closes the tiny polar boundary.
`fix-scalp-winding.py` applied the same focused correction to the existing
actors without changing vertex positions, skin weights, face, clothing or rigs.
`verify-scalp-culling.py` imports the actual exported FBXs, checks outward crown
normals for all three and renders the hero from top/rear with opaque HairCap
backface culling enabled. Exact images and hashes are recorded in
`scalp-winding-verification.json`; gameplay confirmation remains separate.
