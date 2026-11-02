# Gospel canopy world — 2026-11-02

The current `GospelPilgrimageWorld` keeps the free XZ field, original 23 story events, 19 passages, encounter coordinates and save semantics. This revision changes presentation and the response of the existing capsule motor. The courtyard, well and Jordan remain traversable 3D spaces.

## Floor and composition

The first courtyard, spring and temple no longer use three hard rectangular stone overlays or individual brown trail strips. One continuous collider mesh per chapter renders a world-space blend of olive ground, ochre earth, eroded stone courts, planted shade and the wet bank. A small runtime linear blend mask follows a smooth curve through the existing encounter positions; its court boundaries have rounded corners and irregular wear. Original limestone and paving images remain intact. The new original 2×2 painted ground atlas is sampled from `Art/GospelPilgrimage/World/ground-atlas` (top left moss, top right earth, bottom left worn stone, bottom right leaf litter). Its original 1254×1254 PNG is retained byte-for-byte in Resources and `Tools/Art/Restage/Sources`; import limits it to 1024 for runtime, with mipmaps and high-quality compression. Inset mirrored repeat avoids a neighbouring atlas quadrant or hard repeat seam without raster edits. The retained texture-based fallback remains usable if the new atlas is unavailable.

Preserved Lost Crypt `Tree1`, `Tree2`, `Tree3`, `Foliage1`, `Rock1` and `Rock3` paintings now supply broad tree, shrub and stone silhouettes. These are upright oblique mesh cards anchored to actual XZ positions, grouped by source texture into six batches per chapter. Real trunk and stone colliders remain separate from their paintings. The center route stays open; cool distant trees, middle planted islands and dark foreground edges supply scale and depth. This is a new composition using existing sample assets, not a reconstruction of Ori's art or code.

`PilgrimageScenery` uses opaque alpha cutout and depth writes to preserve actual occlusion. Anchored roots remain still while only upper branches receive a restrained breeze. Source painting volume is retained instead of applying flat lighting to it. All scenery UVs are authored by this world and texture reads use explicit LOD. The original imported PNGs and GUIDs are unchanged.

`PilgrimageGround` gives the opaque floor broad value variation and moss islands. Its fine grain and leaf shade share world coordinates, avoiding separate rectangle decals. `PilgrimageWater` supplies a quiet jade Jordan surface with slowly travelling reflection ribbons; existing sparse glints and dry-bank plants remain. The previous broad transparent ground masses are no longer rendered, keeping planting, pollen and light shafts batched.

## Camera and movement

Camera yaw remains zero and pitch remains 43 degrees, preserving directional actor mapping and screen-relative XZ input. The initial orthographic size is 5.7. Measured travel supplies a gentle, limited look ahead, with damping when direction changes. The camera retains its ground reference during a jump so the character rises within the composition without lifting the whole landscape by the same amount.

The existing CharacterController still owns collision, gravity, coyote time, jump buffering, bounds and the short exploration dash. Bounded acceleration and braking replace the long exponential stop tail; small resting touch-stick values are ignored. Diagonal movement remains normalized. `Velocity`, `MeasuredSpeed`, `FacingYaw`, `IsGrounded`, `IsDashing` and `DashSequence` retain their interfaces, allowing animation to follow actual displacement and collision.

## Preserved rendering regression

The well's original cedar material, PNG, normal map, tint, scale, offset and source mesh remain untouched. The working runtime `PilgrimageCedar` replacement still reads finite world UVs using explicit LOD. Camera StopNaN is retained as a secondary guard. The existing raw HDR test at Mary/well and Jordan must still run with post processing and StopNaN disabled; a visually clean final screenshot alone does not prove finite source pixels.

## Sources and verification

The exact user reference is the Ori review at <https://pickledchili.tistory.com/3>, dated 2021-03-27. Newly inspected stills are recorded in `Tools/Research/Restage`. They show strong silhouette framing, layered color values, a clear movement plane and controlled focal light. Still images establish composition, not animation quality.

The reused paintings come from Unity's official Lost Crypt sample, pinned commit `96015220e0f35b18b3ad390d38f97ddcc38bb514`: <https://github.com/Unity-Technologies/mobileQA-LostCrypt-AutoPlay-Deterministic>. Copyright Unity Technologies; use remains subject to the Unity Companion License retained in `Assets/Resources/Art/GospelLuminous/World/SOURCE-NOTICE.txt`. Original files are reused directly and not edited. The new 3D shaders, field composition, mask generation, camera and movement changes are this project's implementation. The old URP 7 2D water render-texture example is not represented as a drop-in URP 17 XZ implementation.

`GospelCanopyVisualSmokeTests` contains two new source fixtures: every original encounter retains a continuous physical floor in both chapters, and actual touch XZ movement respects normalized speed, plants promptly after release and exercises a real jump with a steady camera. Actual execution, capture review, finite HDR regression, build and publication belong exclusively to `/root/remaster_release`. Source authoring is not a test-pass or visual-approval claim.
