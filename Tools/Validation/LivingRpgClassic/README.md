# Living RPG and classic client validation sources

These are source plans and explicit release selectors, not execution results or
approval records. The completed CanonArt release and its artifacts remain a
separate historical baseline.

The Root execution owner starts from the repository root:

```sh
source Tools/Validation/LivingRpgClassic/activate-current.sh
```

This selects `living-classic`, revisions `fm-classic-ui-2026-11-10` and
`gospel-living-rpg-2026-11-10`, the new `LivingRpgClassic-2026-11-10` evidence
root, `/tmp/firstgame-living-classic-20261110-builds`, the actual new owned
control folder's `BuildProof`, and `Artifacts/LivingRpgClassicWebPlaytest`.
It does not acquire or extend a lease, execute anything, or modify old outputs.

Root must inspect the two helper proposal sources and copy their exact bytes
into the currently owned control folder before executing them. They retain the
existing accepted process-controller source, guard/idle/cleanup receipts, shared
execution deadline and exclusive new attempt directories. The review helper
never views or approves an image: `mark` is only permitted after Root actually
views the named current original, and `finalize` requires the completed passed
wrapper and every required explicitly marked original. Neither proposal runs
from this source folder; the owned-command helper explicitly requires its real
control location.

After product sources and native test names are frozen, use new attempts:

```sh
python3 Tools/Validation/run-canon-native.py FantasyCards LivingNative01 --prepare
python3 Tools/Validation/run-canon-native.py PathOfDawn LivingNative01 --prepare
```

The explicit sets retain all prior eight card and twenty-one Gospel tests, plus
the real classic-profile/economy/RNG test and four persistence, ambient-person,
moving-encounter save, and live-minimap tests. They require exactly nine and
twenty-five passed, unique test names with zero failures or skips. Their source
presence is not a test pass. Root selects only an actual successful attempt,
retains exact XML/inputs/binding and current originals at the new canonical
paths, then writes a separate explicit current Native review.

```sh
python3 Tools/Validation/run-canon-web.py FantasyCards LivingWeb01 --native-run "$FIRSTGAME_VALIDATION_ROOT/NativeRuns/FantasyCards/LivingNative01"
python3 Tools/Validation/run-canon-web.py PathOfDawn LivingWeb01 --native-run "$FIRSTGAME_VALIDATION_ROOT/NativeRuns/PathOfDawn/LivingNative01"
python3 Tools/Validation/run-canon-browser.py FantasyCards Desktop720 LivingBrowser01
python3 Tools/Validation/run-canon-browser.py PathOfDawn Desktop720 LivingBrowser01
python3 Tools/Validation/run-canon-browser.py FantasyCards Mobile844 LivingBrowser01
python3 Tools/Validation/run-canon-browser.py PathOfDawn Mobile844 LivingBrowser01
```

The new browser plans retain every former required checkpoint, the complete
ordinary-input starter/shop/deck/battle flow, both real reloads, and four actual
isolated advancing card-music PCM windows. They add the real classic profile
and shop views, and real-coordinate compact/expanded map and persistent-person
views. Card coordinates are browser top-origin; the product's normalized Unity
bottom-origin anchors were converted only where the new client layout changed.
Original catalog/detail/starter/owned-editor controls keep their former inputs.
Root supplies ordinary input only when an authored review request is pending;
there is no runtime, save, DOM-state or JavaScript game-state injection.

The shared shipping harness includes the new exact revisions in all existing
viewport, PCM/role, lifecycle/error, and complete public image-audit guards.
Only explicit revision eligibility changed. The old forty-one positive and
negative strict reload cases remain present; the same cases also exercise the
new revision without translating intentionally invalid revision inputs. Root
must execute the actual suites and retain the real results.

After current Native/Web/browser validation, all required original reviews,
source commit and its actual required CI are complete:

```sh
python3 Tools/Validation/canon_release_evidence.py --check builds
python3 Tools/Web/stage-living-classic.py
```

The new stage entry selects the profile and reuses the same strict two-game
stage implementation, byte checks, license notices and unchanged 985MB budget
including its manifest. The existing publisher and explicitly dispatched Pages
workflow must complete normally; a stage or push is not deployment completion.
Then run the existing generic complete public HTTPS GET verifier against the
new stage, directly review both portal originals, and execute both new public
touch suites:

```sh
python3 Tools/Validation/run-canon-browser.py FantasyCards PublicMobile844 LivingPublic01
python3 Tools/Validation/run-canon-browser.py PathOfDawn PublicMobile844 LivingPublic01
python3 Tools/Validation/canon_release_evidence.py --check all
```

Root must preserve the actual whole command stdout and take only the last
nonempty JSON line as the final seal envelope: the builds phase can emit an
earlier separate JSON object. No parsed/reconstructed summary replaces a raw
report. Failed and interrupted attempts remain unchanged, are explicitly
relocated with exact byte ledgers before a new attempt, and never receive a
fabricated verdict, final guard or cleanup result. Physical mobile performance,
human audio listening, and the user's aesthetic approval remain separate from
software browser/Native checks.
