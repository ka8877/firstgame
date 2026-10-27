# Authored Gospel courtyard props

`GospelAuthoredProps.Initialize()` creates or clones explicit URP Lit materials.
All authored roots receive `GospelAuthoredPropRoot`; world static batching must
exclude every renderer below that marker so tree LODs and material settings survive.

Runtime API (all return a `GameObject`, with local positions below `parent`):

- `AddTree(parent, position, height, yaw = 0, variant = 0)` — authored A/B shade trees, compact LOD0/LOD1 and a narrow trunk collider.
- `AddShrub(parent, position, height, yaw = 0, variant = 0)` — authored small shrub, no route-blocking foliage collider.
- `AddBasin(parent, position, diameter, yaw = 0)` — shallower irregular stone ring, quiet water and simple collision.
- `AddCeramicVessel(parent, position, height, yaw = 0)` — handled/lidded ceramic scan.
- `AddClayPlanter(parent, position, height, yaw = 0)` — worn terracotta planter scan.

The helper normalizes the actual imported mesh bounds rather than assuming FBX
centimetres. Tree LOD roots must stay separate from the world's merged meshes.
`OnDestroy` disposes only the helper's material clones and quiet-water mesh;
authored resource meshes/textures remain shared.

`material-spec.json` lists exact FBX material names, 1K maps, roughness constants
and alpha settings. OpenGL normal maps use `flipGreenChannel=false`. Leaf/twig
materials use double-sided alpha cutout; bark remains opaque. Prepared material
assets under `Art/GospelAuthoredProps/Materials` preserve shader variants, while
the helper still explicitly applies maps and settings at runtime.

Reproduce the derivation and exported-FBX asset preview:

```sh
blender -b -t 4 --python-exit-code 1 --python Tools/Art/GospelAuthoredProps/build-props.py -- --root .
```

`--no-render` skips only the final studio render. The final manifest verifies
derived FBX triangles, UV layers, bounds and finite coordinates after importing
the exported files. This is asset preparation, not Unity/browser verification.
Original sources/license records remain in `Tools/Art/GospelAuthored`.
