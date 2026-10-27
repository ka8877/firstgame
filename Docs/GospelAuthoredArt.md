# Gospel authored art sources

Revision: `gospel-authored-2026-10-27`. The playable chapter depicts Jesus at
age twelve in Luke 2. Faces, clothing and courtyard composition are artistic
interpretations, not recovered historical likenesses or an archaeological
reconstruction. World integration and reproduction commands are documented in
[GospelAuthoredWorld.md](GospelAuthoredWorld.md).

The new continuous tunic replaces the former muscle-shaped clothing surface.
Its flat chest, approximately 0.369 m boy shoulder width, narrow shoulder drape
and fitted waist rope use independent geometry and analytic skin weights.
The boy has 125,228 triangles; the adult base 140,728; Mary 135,164. Joseph and
the supporting Temple teacher use separate adult FBXs with adapted faces,
cropped hair and separate skin/hair material entries. All five actors retain
the 65-bone GospelRig transform paths and seven animation takes. Existing
YoungJesus/AdultJesus/Mary `.meta` GUIDs and save/content IDs are preserved.

Anatomical heads, head/eye UVs, skin/hair maps and the retained body/animation
sources are the documented CC0 MakeHuman and Quaternius assets. Original
files and licenses remain in [GospelNatural/SOURCE-NOTICE.txt](../Tools/Art/GospelNatural/SOURCE-NOTICE.txt)
and [Gospel3D/SOURCE-NOTICE.txt](../Tools/Art/Gospel3D/SOURCE-NOTICE.txt).
The cloth uses the existing cream/blue linen albedo plus the unchanged 1K
OpenGL normal map from [Poly Haven Rough Linen](https://polyhaven.com/a/rough_linen),
by colormass (photography) and Rico Cilliers (processing), at normal strength 0.28.
The blue source diffuse is retained as provenance but is not used on the robe.
Exact URLs, MD5 and SHA-256 are recorded in
[linen-source-manifest.json](../Tools/Art/GospelAuthored/linen-source-manifest.json).

Courtyard props derive from Poly Haven CC0 assets:

- [Searsia Lucida](https://polyhaven.com/a/searsia_lucida): James Ray Cock and Jenelle van Heerden. Two tree variants have compact LODs; a smaller variant becomes a shrub. Original RGB and alpha are preserved exactly in the packed atlas. This is an artistic shade tree, not a claim of a historical olive species.
- [Stone Fire Pit](https://polyhaven.com/a/stone_fire_pit): Sebastian Platen. The irregular stone ring becomes a shallow illustrative water basin with a separate quiet-water disc.
- [Ceramic Pot](https://polyhaven.com/a/ceramic_pot): Aron Łyczek. A handled/lidded ceramic scan.
- [Planter Pot Clay](https://polyhaven.com/a/planter_pot_clay): Amal Kumar. A weathered terracotta scan.

The [Poly Haven license](https://polyhaven.com/license) is CC0. Original
sources and API/hash records remain in
[source-manifest.json](../Tools/Art/GospelAuthored/source-manifest.json).
The compact runtime props total 11,074,303 bytes before Unity processing.
Exact geometry/map/normal/alpha settings and author notices are in
[GospelAuthoredProps](../Tools/Art/GospelAuthoredProps/README.md).
Runtime URP Lit materials use explicit 2D maps; leaf cards use double-sided
alpha clipping. Architecture batching excludes prop roots to preserve LODs.

[export-verification.json](../Tools/Art/GospelAuthored/export-verification.json)
checks the actual five exported FBXs: shared 65-bone paths, seven takes,
105 finite posed samples and scalp winding. Its final front/side captures
identify their exact source FBX hashes. Prop derivation separately checks
all eight exported FBXs, UVs, finite bounds and source RGB/alpha identity.
These are Blender asset checks. Unity import, actual game rendering,
conversation/collision/movement and browser/device performance require the
separate release validation; asset checks do not establish those outcomes.
