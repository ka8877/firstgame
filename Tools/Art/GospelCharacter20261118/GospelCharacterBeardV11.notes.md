# Beard V11 source proposal

Root reported an actual exhaustive diagnostic of 626,400 beard/source morph
comparisons: all 440 differences were exact-zero stored deltas against nonzero
source-anchor displacements of about 6.7–9.95 micrometres. The other 625,960
comparisons had exact zero error. This source-only proposal addresses those
native sparse-storage losses; it is not an execution or visual approval.

`GospelCharacterBeardV10.cs` remains unchanged. V11 keeps the same class/API and
all source face, native skin, anatomical selection, mouth/frame exclusion,
clump topology, quotas, source frame metadata and strict 2-micrometre contract.
At candidate admission, component-exact zero remains legitimate; any other
local source-anchor frame displacement shorter than 10.1 micrometres rejects
that candidate. No delta, barycentric coordinate, influence or source mesh is
rounded, amplified or fabricated. Existing actual vertex candidates undergo
the same check. Precision rejection counts are reported per examined region,
including quota failures; geometry and lip masks are not relaxed.

Every frame is read back for every actual output vertex immediately after its
`AddBlendShapeFrame` call and checked against that anchor's unchanged source
formula with strict error `< 0.000002f`. The exact new derived mesh alone is
saved, synchronously force-reimported and read back again for all source
shapes/frames/vertices. Existing differing assets still fail before import or
overwrite; native palettes retain their previous exact round-trip checks.

Root must change only the beard preparation call's output argument from
`GospelCharacterRefinement.OutputRoot` to `GospelCharacterBeard.OutputRoot`.
The latter is explicitly `Assets/Art/GospelCharacter20261118/v2`; V11 rejects
the v1 root. Current v1 meshes and sidecars remain immutable. This proposal
does not edit the selected Editor source, assets, preparation caller, Native
tests, frozen contract or actual evidence and has not executed Unity/tests.

Root should run fresh preparation, the unchanged full Native contract and
the required runtime/visual review before accepting this proposal. If native
storage still loses a surviving displacement or the remaining actual anchors
cannot satisfy quotas, generation fails rather than changing the contract.
