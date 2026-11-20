# Optional original linen maps for the child settings bridge

`build-child-settings-v2.py` preserves V1 source and adds an optional pair of
original CC0 Poly Haven rough_linen images. This draft has not run Blender,
Unity, import, rendering or product validation. The text source manifest was
read; texture pixels were not read or modified by the source author.

Add these arguments to the existing V1 invocation when Root chooses the maps:

```text
--linen-diffuse /absolute/source/rough_linen_diff_1k.jpg
--linen-diffuse-sha256 04897d7611d7390ddf5b2c6f7273f448dfc48341fdbd1f4f207fa30b331d7f57
--linen-diffuse-bytes 835801
--linen-normal /absolute/source/rough_linen_nor_gl_1k.jpg
--linen-normal-sha256 e2402166d1a489f7414814e69e1836ab672aa24e30bec2d41fea356f81687e53
--linen-normal-bytes 916843
--linen-tiling 4 4
--linen-normal-strength 0.16
```

Each map is independently optional, but its path, SHA and byte length must be
supplied together and match the admitted original. Tiling is shared by color
and normal UV0, restricted to 1–24 repetitions per axis. Normal strength is
limited to 0–0.5; its option requires the normal map. The existing generated
linear color factor remains in effect. Actual visual inspection must decide
whether the default tiling/tint/normal strength works at gameplay distance.

Only reported generated roles `linen` and `facing` receive the maps and UV
scale. Generated leather, original material slots, their source-image choices,
skin, eyes, hair, and face normal omission rules are unchanged. The new normal
source is explicitly marked for Unity NormalMap import; it is not a color/data
texture. Configuration retains the V1 preparation schema and requires the
existing `GospelHeroChildPreparationV2.cs` normal-binding API. The audit uses
schema 2 and records source pins/license/authors, affected roles and choices.

Without either optional map the emitted preparation Settings retain V1
defaults. The audit includes additional V2 provenance and generated material
decisions. All outputs must be new files; neither bridge overwrites source
images or previous outputs. This adds material detail, not new cloth folds or
an assertion of final quality.

Manifest: `Tools/Art/GospelAuthored/linen-source-manifest.json`.
Source: <https://polyhaven.com/a/rough_linen>, CC0, photography by colormass,
processing by Rico Cilliers. This map grant does not change the character
model's separate source terms.
