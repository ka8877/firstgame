# Authored Gospel actors and selected courtyard sources

Revision: `gospel-authored-2026-10-27`.

`build-authored-actors.py` starts from the preserved natural actor source and
replaces the complete muscle-shaped linen mesh with one continuous loose
tunic. The new surface has a flat chest, exact collar/cuff/hem cuts, bounded
cloth triangle density, a narrow shoulder mantle, a rope seated against both
cloth layers and independent analytic skin weights. It preserves the original
65-bone transform paths and seven locomotion takes. Existing actor GUIDs stay
unchanged. `build-npc-variants.py` derives the separate Joseph/teacher resources.

`verify-exported-actors.py` imports the actual five exported FBXs and records
bone paths, take names, 105 finite posed bounds, scalp winding and source-hashed
asset renders in `export-verification.json`. Its `--no-render` option checks
geometry/rig only. `--selected-fit-views` captures only the boy's front and side.
These are Blender asset checks; they do not claim Unity/browser or physical
hardware validation.

`manifest.json` records the new chest profile and actor triangle counts.
`npc-variants.json` records supporting resource hashes and distinctions.
`SOURCE-NOTICE.txt` links preserved MakeHuman/Quaternius sources and licenses.
`linen-source-manifest.json` verifies the subtle CC0 photo-weave normal; the
blue source diffuse is retained for provenance but not used on the cream robe.

`fetch-authored-props.py` and `source-manifest.json` retain selected ordinary
public Poly Haven downloads, source metadata, license and hashes under Source.
The compact FBX/LOD derivation, actual exported prop preview, normal/alpha map
spec and runtime helper are isolated in `Tools/Art/GospelAuthoredProps` and
`Resources/Art/GospelAuthoredProps`. Source files and previous actor revisions
remain available without placing the original high-density source pack in
runtime Resources. See `Docs/GospelAuthoredWorld.md` for world/NPC integration.
