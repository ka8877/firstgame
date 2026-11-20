# Human character art derivation

These are source-only Editor authoring helpers. Root alone imports them, generates
assets, runs Unity/tests/builds and reviews the actual game. Their reports do not
establish artistic approval, historical likeness or equivalence to a commercial
game's character production.

The retained bases remain ActorBake09's licensed VRoid AvatarSample_C and pixiv
VRM1 female sample. The original FBX, textures, source manifests, source meshes,
Avatar, controller, bone transforms, motion clips and notices are not replaced.
The four male roles intentionally receive different face proportions. Mary keeps
the female source's head-height proportion and receives a restrained eye/face pass.

The selected refinement source is `GospelCharacterRefinementV2.cs`. Root installs
its bytes as the sole `Assets/Editor/GospelCharacterRefinement.cs`; the retained
`GospelCharacterRefinement.cs` in this Tools directory is a source-only v1
candidate and must not be compiled alongside V2. V2 bounds the fringe-lift gain
at 0.42 to avoid collapsing vertical spacing, and reports source revision v2.
It keeps the initially unused versioned asset root and mesh V1 names. Actual
per-vertex affine determinant gates remain; this source fix grants no visual pass.

The selected beard source is `GospelCharacterBeardV3.cs`; Root likewise installs
it as the sole `Assets/Editor/GospelCharacterBeard.cs`. Its geometry, skinning,
morphs and rendering are identical to the retained v1 source. The retained V2 additionally
writes its deterministic flat Report to the mesh-adjacent `.anchors.json` file,
with write-once/identical-reuse semantics and actual synchronous TextAsset byte
verification. This permits source-anchor checks even when a valid facial morph
has zero displacement in the selected beard region.
V3 retains that sidecar and only replaces writable NativeArray `using` variables
with ordinary variables plus `try/finally` disposal. Root's actual first import
found CS1654 in V2; no successful compile is inferred from this source correction.

## Root integration order

1. Prepare the existing FBX/Avatar/controller and execute the existing V5
   `GospelIllustrationTailoring.Apply` on a fresh unanimated actor. Keep its
   source-rest proof. Do not edit V5 or an earlier generated candidate in place.
2. Apply Root's separate closed-neck/tunic construction before measuring the new
   hair envelope, if that construction changes the shoulder/back surface.
3. Call
   `GospelCharacterRefinement.Apply(instance, actorId, layeredHairMaterial)`.
   The class is in `Firstgame.GospelNpr.Editor`. Adult Jesus requires a separately
   created persistent lilToon material for his new strand UVs. The other four
   actors do not add this shoulder-length hair layer.
4. For JesusAdult, Joseph and Teacher only, call
   `GospelCharacterBeard.Apply(instance, animator, actorId, beardMaterial,
   GospelCharacterRefinement.OutputRoot)`. Use a fresh neutral male bind rest.
   Supply a non-outline opaque material with a plain or tileable fiber texture;
   a repeated hair-atlas rectangle is not a matching beard UV layout.
5. Apply Root's new material polish without losing the actual `Face_...SKIN` and
   `FaceMouth` semantic material-slot names used for anatomical measurements.
   Save the existing stable character prefab with its original identity.
6. Root runs this directory's `generate_expression_bindings.py --root REPO`.
   It writes a new small Resources catalog under
   `Art/GospelCharacter20261118/ExpressionBindings`; select that resource in the
   new runtime revision. The old illustration catalog/generator stay intact.
   The new map has five source face presets per actor and another five for each
   of the three beard renderers: 40 rows in total. Metadata alone is not proof
   that the new renderer/morphs exist; runtime's unique resolver must establish it.

Generated mesh assets use the versioned
`Assets/Art/GospelCharacter20261118/v1/<actor>/` directory, outside Resources.
Existing stable prefabs reference the selected meshes. A repeat may reuse an
identical mesh; a differing prior asset is preserved and rejected. Select a new
reviewed output version before changing the art parameters of persisted assets.
Nothing runs automatically at import time.

## Face and head shaping

The face helper reads actual eye/head/neck bones, drawn facial-skin triangles,
modeled mouth and rendered scalp/garment vertices. Unreferenced vertices in V5's
full-buffer cloth partitions do not define the visible envelope. The original
neutral renderer must agree with an actual temporary `BakeMesh(false)` sample
within 0.5 mm; this catches a wrong skin frame or an effective skin-quality
mismatch, including the female source's legitimate nonidentity palette.

Adult profiles reduce the shared oversized eye aperture and distinguish jaw,
chin, cheek, brow and nasal projection. A measured, bounded head-only vertical
correction targets 6.6 head units for the boy and 7.35 for the adult men. These
numbers are design targets, not claimed outcomes: source and derived scalp-to-
chin measurements are reported, and the scale correction is limited to
0.90–1.025. It does not rescale the physical owner or edit a rig transform.

Each vertex's sculpt/skin-space affine map is fixed from its actual Basis/rest
point. Every source morph delta uses that same linear map. Masks are never
recomputed from an individual expression frame, so simultaneous morph
combinations retain linearity. Authored normal and tangent deltas use fixed
corresponding maps. Mesh topology, all UV channels and their attribute formats,
color values/formats, complete native influence count/order/index/value and
bindposes are checked against the retained mesh. Every shape name/frame/weight
and renderer default weight remains; original source fingerprints are rechecked.

Existing renderer names remain `<id>__Face` and `<id>__Hair`. Their new mesh names
are `<id>__HumanFaceV1` and `<id>__HumanHairV1`. No source face/skin polygon is
hidden to create the appearance of a new head.

## Shoulder-length layered hair

Adult Jesus receives 24 closed, curved, tapered elliptical locks. Their roots
attach to actual referenced source scalp vertices; every vertex retains its
anchor's full native skin palette and that palette is read back after Unity's
`SetBoneWeights`. The helper fails if Unity reencodes it differently. Lower back
tips sit outside the actual retained garment/back envelope in the authored rest.
Source hair remains present and receives its separate eye-clearance/crown pass.

The new renderer is `<id>__LayeredHairV1`. UV0 U goes around the lock and V goes
root to tip. Standard recalculated tangents follow UV U; Root's separate material
must choose the correct anisotropy direction/widths for this basis. Avoid a heavy
inverted outline around every lock or highly reflective plastic highlights.
These head-bound locks do not introduce a spring-bone simulation. Actual head
turns, run, jump and shawl clearance still require visual inspection.

## Fitted volumetric beard

The separate helper does not restore the earlier rejected flat jaw/moustache
patches. It creates up to 224 closed curved clumps / 4,480 vertices, fitted to
actual facial skin and bounded by the modeled lips, chin and eyes. Barycentric
anchors are allowed only on triangles whose vertices have exactly the same
complete native skin palette; mixed palettes use actual original vertex anchors.
All source morph frames and defaults are carried onto the unique
`<id>__CharacterBeard` renderer. See
`Artifacts/Validation/Drafts/ActorQuality20261118/BEARD-SOURCE.md` for that helper's
proof boundary and material/catalog requirements.

## Required actual review

Root must compare ordinary game front, profile and three-quarter views at the
same scale, then examine idle/walk/run/jump, stopping, NPC approach, head turns
and a complete A/I/O speaking cycle. Reject unhuman facial proportions, separated
eyelids/lining, collapsed morphology, scalp gaps, rope-like locks, triangular
beard blocks, cloth penetration, broad exposed boy shoulders and material washout.
The helpers do not fix the entire costume silhouette or supply new clothing
textures; those are Root's separate geometry/material work. Existing movement,
collisions, story, saves and UI portraits must continue to function. No earlier
Native/Web screenshot approves these newly generated meshes.
