# Gospel authored actor and courtyard revision

Revision: `gospel-authored-2026-10-27`.

The playable twelve-year-old actor now wears one continuous loose tunic with
a flat chest, narrower shoulders, open collar/cuffs/hem and a narrow shoulder
drape. The old superhero pectoral surface is not used for the new clothing.
The waist rope is fitted against the actual robe and mantle surfaces. A subtle
1K photographed linen normal adds weave to the retained cream/blue cloth.
The current boy cloth shoulder width is approximately 0.369 m at a 1.45 m height.

The bounded garment topology keeps total actor geometry near the previous
budget: 125,228 triangles for the boy, 140,728 for the adult base and 135,164
for Mary. The anatomical head/eye/skin sources and Quaternius locomotion rig remain.
A small neutral head/brow adjustment changes the boy's presentation without
changing the animation transform paths. Mary has her separate head and veil.
Joseph and the Temple teacher now use dedicated adult FBXs with different
face proportions, cropped hair and skin/hair material entries. The guide and
two teachers share the supporting teacher resource, with individual robe colors.
These are artistic depictions; no historically recovered likeness is claimed.

The courtyard replaces the procedural ball-shaped tree crowns, smooth lathed
water basin and untextured pottery with compact authored Poly Haven CC0
assets: two broadleaf tree variants with LODs, a shrub, rough stone ring and
two ceramic scans. Tree roots retain `LODGroup` and authored alpha/normal
materials by being excluded from architecture mesh combination. Their small
trunk colliders leave foliage open. The stone ring is used as an illustrative
quiet-water basin; the source tree is an artistic shade tree rather than a
claim of a particular historical species. Existing limestone architecture,
landmarks, routes, lighting, camera/motor and Scripture progression remain.
Raised paving and processional tiles now have collision at their visible top
surfaces; NPC roots sit on the matching surface height.

Five physical NPC roots carry the app's `GospelNpcAnchor`: `temple-guide`,
`teacher-reading`, `teacher-listening`, `mary`, `joseph`. The new guide stands
beside the first marker at (1.2,0,-5). The app owns proximity, input, conversation
UI and exact Scripture; the world provides actor identities and positions.

Reproduce the actor source/export and read-only FBX validation:

```sh
blender -b -t 4 --python-exit-code 1 --python Tools/Art/GospelAuthored/build-authored-actors.py -- --root . --no-render
blender -b -t 4 --python-exit-code 1 --python Tools/Art/GospelAuthored/build-npc-variants.py -- --root .
blender -b -t 4 --python-exit-code 1 --python Tools/Art/GospelAuthored/verify-exported-actors.py -- --root .
```

The validation imports all five actual exported FBXs, checks the same 65-bone
paths and seven takes, checks finite bounds at three samples per take (105
posed samples in total), and checks opaque scalp face directions. Front/side
renders identify their exact source FBX hash in `export-verification.json`.
The props pipeline separately reimports all eight derived FBXs and checks
triangle counts, UVs, finite bounds and exact source RGB/alpha packing.
These are asset checks, not Unity/browser or physical-device performance tests.
Unity import/material/animation, actual world/NPC/UI interaction and Web build
validation are performed by the release workflow after the source freeze.

Sources and licenses: [actor/cloth notice](../Tools/Art/GospelAuthored/SOURCE-NOTICE.txt),
[prop notice](../Tools/Art/GospelAuthoredProps/SOURCE-NOTICE.txt),
[prop derivation](../Tools/Art/GospelAuthoredProps/README.md).
Original sources, prior revisions, existing actor GUIDs and save/content IDs
remain available. Prepared Unity materials use the recorded URP Lit map,
normal and alpha settings; no embedded Standard material is relied on.
