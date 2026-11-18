# Gospel hero renderer implementation draft

Concrete source, not an installed/rendered result. Root alone installs, imports, configures actual material assets, executes Unity/Blender/tests and reviews images. Only this new draft folder was written. No existing game file, model, texture, rig, expression, Core/save ID or validation receipt was changed by this source work.

This renderer can be applied to the existing child **without any adult anatomy conversion**, or to a separately admitted professional child model. It does not create a new face topology, proper eyeball, appealing hairstyle or a well-tailored costume by itself. Model and authored texture quality remain prerequisites; there is no AAA or commercial-game-parity claim.

## Files and installation

Copy source files to new product paths only after source review, retaining Unity-generated `.meta` identities on subsequent versions:

| Draft | Proposed product location |
| --- | --- |
| `Runtime/GospelHeroHeadFrame.cs` | `Assets/Game/Presentation/Pilgrimage/GospelHeroHeadFrame.cs` |
| `Runtime/GospelHeroModelProfile.cs` | `Assets/Game/Presentation/Pilgrimage/GospelHeroModelProfile.cs` |
| `Shaders/FirstGameGospelHeroFace.shader` | new `Assets/Game/Presentation/Pilgrimage/Shaders/GospelHero/Face/` |
| `Eyes/FirstGameGospelHeroEyes.shader` | new `.../Shaders/GospelHero/Eyes/` |
| `Hair/GospelHeroHair.shader`, `GospelHeroHairInput.hlsl`, `GospelHairFlow.hlsl` | new `.../Shaders/GospelHero/Hair/` together; includes are relative siblings |
| `Editor/GospelHeroMaterialSetup.cs`, `GospelHeroRendererPatch.cs` | `Assets/Editor/` with their exact filenames |
| `Editor/GospelHeroChildPreparation.cs` | `Assets/Editor/`; independent fresh-source import entry point |
| `Runtime/GospelNprCharacterView.hero-profile.cs` **or** `Patches/GospelNprCharacterView.hero-profile.patch` | Replace the content of the existing `GospelNprCharacterView.cs`, or apply the exact combined diff. Do not install a second copy of this class. The combined source already includes the portrait head-frame allowlist. |

The earlier `hero-head-frame.patch` remains as a narrow retained source draft; do not apply it again after the combined profile patch.

Shader names are exactly `FirstGame/GospelHero/FaceSdf`, `FirstGame/GospelHero/Eyes`, `FirstGame/GospelHero/Hair`. All expose `_MainTex`, its scale/offset, `_Color`, `_MainTexHSVG`, `_AsUnlit`, `_LightMinLimit`, `_LightMaxLimit`, `_ShadowColor`, `_Cutoff`, `_AlphaClip` and `_Cull` where relevant. `FORWARD` pass names preserve the existing View Web diagnostic lookup. They are project-authored URP shaders, not renamed lilToon source or a claim of bit-identical arithmetic. Each shader owns all its passes and one consistent `UnityPerMaterial` layout; no URP Lit `UsePass` with a mismatched layout is borrowed.

The active rigged presentation is `GospelNprCharacterView`. `GospelMotionLayers` is the old Flare sprite-atlas path; do not insert these rigged shaders there.

## Minimal Editor entry point

`GospelHeroRendererPatch.Apply(GameObject workingModel, Animator animator, Plan plan)` is the integrated callable entry point. It accepts explicit renderer/material slots rather than guessing from names. `Plan.actorId` must be one of the existing five logical IDs; it is recorded, not used to change age, source geometry or Core/save state. `outputVersion` selects a fresh material folder under `Assets/Art/GospelVisualRebuild20261119/<version>/<actorId>/`.

For each slot select `Role.Face`, `Role.Eyes` or `Role.Hair`. Face slots use an explicit `Plan.enableFaceSdf` switch, **false by default**; false creates normal-shading fallback and does not fabricate a mask. True requires the real compatible `Plan.authoredFaceSdf` data texture (artist-authored or explicitly recorded as geometry-baked). Eye slots carry `EyeOptions`; hair slots explicitly select TangentU, BitangentV or AuthoredUvVector. Eyelashes, eyebrows and standalone highlight quads must keep their appropriate existing cutout/transparent material unless the new source explicitly replaces those surfaces. The opaque eye shader must not be assigned to a floating highlight-only plane. `EyeOptions.fittedSurfaceVerified` must be true; source MASK retains its cutoff/clipping, while source BLEND is rejected and must remain on the legacy transparent path.

Provide **verified neutral head-local** forward/up vectors. The read-only convenience `HeadLocalAxesFromVerifiedActorFrame(actor, head, out forward, out up)` is correct only after Root verifies that the neutral actor's +Z/+Y correspond to its actual face. A humanoid head bone's local +Z cannot be assumed to be facial forward. `BindHeadFrame` validates the target slots and places a sealed `GospelHeroHeadFrame` on the working model. LateUpdate publishes an orthonormal world-space frame to each face material's property block after animation, preserving the View's existing per-slot `_Color` and unrelated overrides.

Low-level calls are also available:

```csharp
Material face = GospelHeroMaterialSetup.CreateFace(sourceFace, authoredSdf, freshFacePath);
Material eye = GospelHeroMaterialSetup.CreateEyes(sourceEye, explicitEyeOptions, freshEyePath);
Material hair = GospelHeroMaterialSetup.CreateHair(sourceHair,
    GospelHeroMaterialSetup.HairFlow.BitangentV, Vector2.up, 0, freshHairPath);
// Assign only to a new caller-owned prefab instance/contents, with explicit slots.
GospelHeroMaterialSetup.Assign(faceRenderer, faceSlot, face);
// BindHeadFrame(...) is required for SDF; material creation alone is insufficient.
```

Source materials are cloned; their actual main texture, source UV transform, tint and HSVG are retained. Each new path is write-once; existing files/meta are rejected. The helper verifies source material JSON remains unchanged and never changes a texture importer. A failed configuration can leave newly created materials under its fresh version; preserve that attempt, discard its unsaved working model and choose a new version for retry. Do not save a failed working instance over the source prefab.

The integrated patch checks that existing skinned-mesh pointers, Avatar/controller, applyRootMotion and local transforms remain unchanged. It deliberately does not reset animation, reshape a face, change a bone, add an adult beard, remap a morph or move the physical owner.

## Face SDF art contract

Supply a dedicated non-sRGB, ordinary data texture with Clamp U/V. Its raw UV0 matches the **face geometry**, independent of `_MainTex_ST`. R encodes an authored angular face-shadow threshold in `[0,1]`: the face is lit where `R >= 0.5 * (1 - dot(headForward, projectedLightDirection))`. Higher R keeps a region lit farther toward a back-facing key. The texture is authored for a key on head-right; head-left mirrors U. `_FaceSdfFlipSide=1` reverses that convention for an oppositely authored map. The default uses this dedicated R data directly; do not pass a photographic face image or an arbitrary generic gradient and claim an authored mask.

The shader uses the **animated** head forward/right/up frame, projects key direction onto its horizontal plane, provides a finite near-vertical-light fallback, and filters the threshold by the artist edge softness plus screen derivatives. `_FaceSdfStrength` blends normal shading and the authored mask. Cast shadows from actual hair/nose/neck remain separate via `_ShadowReceive`; this is not a painted contact-shadow substitute. If the binder is missing/disabled, `_GospelHeadFrameValid=0` deliberately falls back to normal shading instead of silently treating object axes as verified head axes.

The current portrait backend disables every cloned MonoBehaviour. The provided small View patch permits **only** the sealed head-frame renderer-data binder, preserving the existing imported-gameplay/camera behavior shutdown. It is necessary for the same animated face SDF to work in that backend. Current illustrated UI portraits may use another path; this patch does not replace them or claim their likeness is fixed.

## Eyes and hair

`Eyes/EYES-CONTRACT.md` documents fitted-surface and texture-channel requirements. The shader computes iris/sclera material response, upper-lid occlusion and a main-light/view-dependent corneal highlight. It uses an opaque surface with depth writes to avoid transparent shell sorting. Actual eye curvature, correct lid fit, gaze and blendshapes must come from the model; optional normal curvature is explicitly an approximation and defaults off.

`Hair/README.md` documents the flow frame. Mode0 preserves existing mesh U direction. Mode1 follows V and is the correct explicit choice for the current added locks whose U wraps around/V runs along length. Do not assign V to all source VRoid hair: its authored tangent/UV convention must be checked independently. Mode2 accepts a constant authored UV vector plus rotation. No texture flow map is sampled and this implementation must not be described as having an authored flowmap. Use the new asset's actual UV/tangent data rather than assuming one convention for every mesh.

## Existing preparation and validation boundary

`GospelNprPreparation.PrepareActor` currently requires every character material shader name to contain `lilToon`, then saves the prefab. The new shaders intentionally have different exact identities. Do not silently broaden that legacy check globally or run a new professional model through the old face/beard/neckline distortion chain. Add an explicit **new-source/profile** preparation branch, use this patch on its caller-owned instance, and validate the three exact new shader names against its explicit slot manifest. Retain the old branch and its original checks for existing sample derivations. The new source has its own geometry/rig/expression/import/provenance checks; it cannot honestly inherit old topology/UV/morph hash equality.

No existing expression names or the `MoveSpeed`, `Grounded`, `VerticalSpeed` Animator parameters are altered by this renderer patch. A newly admitted 109-bone/122-shape model still needs its own actual humanoid retarget and expression catalog mapping. A no-morph fused mesh cannot acquire facial expressions through these shaders.

`CHILD-PREPARATION-CONTRACT.md` documents the concrete fresh-source importer. It prepares only the independently admitted child, selects the new resource prefix and per-model expression map, and retains other roles on the old path. No adult source or head-growth transformation is applied. It can preview the new model with normal face shading while a geometry-derived angular threshold map is inspected; enabling SDF requires a compatible pinned geometry-bake report bound to the selected PNG, not a boolean assertion alone.

Required actual Root validation after installation: C#/shader compilation and Native/Web supported variants; correct face-slot/SDF mapping; independently moving head and key light; front/45°/side/back-light transitions and vertical-light poles; source alpha in forward/depth/shadow/normal passes; eye blink/gaze/lid fit and glint movement; U/V/constant-vector hair highlights including mirrored UVs; no unintended body/rig/expression changes; world and portrait activation/deactivation; actual render stability and frame/memory cost. This draft supports main key + SH/probe ambient, fog and explicit main-light shadows. Additional-light/Forward+ cluster loops, lightmaps, deferred G-buffer material output, motion vectors and outlines are not implemented by these three new shaders. Keep old materials where those features are required, or implement and verify them explicitly in the new profile.

Static peer review has been requested/completed per file notes; no Unity compilation, rendering, performance test or aesthetic approval was executed by the source authors.
