# Original first-slice proxy art

Requires Blender 4.3.2 or compatible Blender 4.x with FBX import/export. Run from the repository root:

```sh
blender --background --python Tools/Art/generate_proxies.py -- --root . --render
blender --background --python Tools/Art/verify_proxies.py -- --root .
```

Generation exports five FBX models to each project's `Assets/Art/Generated`, keeps editable sources in `Source/FirstSliceProxies.blend`, and writes `AssetManifest.json`. `--render` adds three actual Cycles source renders in `Previews`. CPU rendering disables denoising because the available distribution does not include OpenImageDenoise. A scene render is not a Unity gameplay screenshot.

No third-party meshes, textures or external files were used by this generator. Every output is a **temporary original prototype asset**, not an approved final visual. Static flat-shaded materials, simple rigid skinning and a four-second idle cycle are deliberate first-slice stand-ins. Regeneration overwrites generated FBXs, previews, manifests and the .blend source; copy an asset to a separately named working source before manual art production. Existing Unity `.meta` files are preserved.

See `Docs/ArtDirection.md` for direction and `Docs/AssetRegister.md` for replacement and provenance contracts.
