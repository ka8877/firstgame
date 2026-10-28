# Luminous garden world

`GospelLuminousWorld` composes a real traversable 2D scene from individual sprites.
The sprite pixels are not a flattened screenshot of another game. Twenty-nine
selected environment PNGs are copied byte-for-byte from Unity's official Lost
Crypt sample. Their exact source paths, source GUIDs, hashes, dimensions and
new import GUIDs are in `manifest.json`. The complete Unity Companion License
and source notice ship inside the new resources folder.

The three-bay Jerusalem garden gateway is a separate original image-generation
asset. Its full transparent PNG is copied unchanged, with transparent arch
openings. It is an illustrative game location, not an archaeological claim.

## Runtime contract

- Component: `PathOfDawn.Presentation.GospelLuminousWorld`.
- `Build(Transform parent, Camera camera)` creates the world once.
- Constants: `MinX=-34`, `MaxX=34`, `SpawnX=-18`; instance `Bounds` and `Spawn`.
- `HeightAt(x)` is the gently changing, continuous top walking surface.
- The actual `EdgeCollider2D` follows that surface; side colliders bound the map.
- `SetObjective(x, visible=true)` positions a quiet warm ground glow, nearby
  light and a few motes. It does not alter story state or create a HUD arrow.
- Hero: XY play plane, feet on `HeightAt`, sorting order20, approximately1.8m.
- Background layers are below zero; walking bank10–16; small flowers25;
  low foreground silhouettes65–70. Foreground shrubs sit below waist height.

The ten parallax planes, including the pollen plane, have distinct camera-follow coefficients, from sky1.0
to the near silhouettes-0.12. Individual foliage clusters and grass strips sway
slightly; all actor movement remains the controller's responsibility. Existing
3D scenery and earlier authored assets remain in the project history.

## Rendering

Unity URP's 2D renderer and Sprite Lit/Unlit Default shaders are required.
Foliage uses original normal textures as `_NormalMap` secondary sprite textures
with real, accurately normal-mapped `Light2D` sources. All spatial lights use
the original `SoftGlow` sprite as a continuous radial cookie, imported two
world units high; the actor lamp shares that authored rendering profile. The
historical `NormalPointLight` resource name is retained, but its actual light
type is Sprite, with no Point or Freeform inner-core path. Cool ambient light, broad warm openings, source
painted light shafts, low bloom and drifting pollen create the palette. The
gateway is painted unlit to retain its authored warm focal values. The bank
uses a continuous textured mesh underneath separate original rock sprites and
grass/flower strips, with twenty irregular rock clusters; it is not a sequence
of primitive rectangular blocks. Its actual mesh uses URP's Mesh2D shader,
while sprite renderers use the Sprite shader and explicit source/mask/normal
bindings. The cloud bank's opaque source lower edge is positioned beneath the
walking surface, preserving its pixels without a floating horizon band.

Original SpriteShape grass/flower image padding is addressed by runtime UV
rectangles based on source alpha bounds. PNGs are not cropped, rescaled or
painted by the port script. Unity import maximum dimension is2048 and normal
imports are linear. `port-world.py` reproduces the asset copies and ledger from
the captured official source checkout and the original generated gateway.

Source/import checks are not claims of actual Unity appearance, frame rate,
animation smoothness or user aesthetic approval. Those require the new
runtime validation owned by the release agent.
