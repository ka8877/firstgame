# Typed paired browser consumer — source-only proposal

`recovery_web_smoke.py` is a functional consumer source draft, not an installed or executed tool. The sole actual owner is `/root/release_recovery`. This folder does not build, publish, merge, seal, repair old evidence, download assets, alter game state, or create a historical success witness. Product sources, existing Tools, Docs, original artifacts and frozen Native results were not changed. All release/baseline readiness values stay false.

The proposed installation is `Tools/Autonomy/recovery_web_smoke.py`. Local execution can also use a reviewed, frozen draft path inside the repository with explicit `--repository-root`. Public execution requires the installed consumer and every actual known producer/helper to be byte-exact in the current clean source commit.

The consumer pins schema SHA `0fbb4c690eeedb64a8871741b6d2fcb211245a227db4f4df5c592ddb80525b6c` and design ledger `c70af45fc10858addd27e859ad9a2faf0dd2d94877ca68980373c6e21c70a19b`. It implements the schema's used structural features without an additional dependency, followed by explicit semantic evidence checks. Static JSON/AST inspection is the only verification performed here. The consumer itself and its schema validator have **not** been imported, run, or tested.

## What it consumes

- Current Card Native04 exact8, Root `0d0cf8c3…`, grade-original review `3b08f5fc…`, fixed FP `42b885cd…`, full source rows and all9 Native contract source/helper/tool hashes.
- Current GospelNative01 exact16, Root `bbbe5fe8…`, fixed FP `255e2d75…`, exactly1,707 original authored rows, actual voice fixture and all136 original regular files /65,953,720B. Native's fade/legacy capture limitations remain limitations; this never promotes Core19 acquisition into readable Web Book approval.
- Both actual new successful Web invocations and actual after-success c597 proof invocations. Original BuildInputs, BuildEvidence, complete runtime, exact5 core roles, restored pipeline Before/After, generated additions, current source observation and original runtime comparison must exist and agree. Gospel additionally requires original legacy isolation/restoration bytes. Card has no Gospel legacy or voice-fixture requirement.
- Explicit original plans and current PC19 append-only plan, unchanged strict audio and classifiers, actual source metadata and complete runtime before/after. Missing genuine new Web evidence blocks even local input. No `sealed_g2`, old `config`, old `current_public` or historical `current_build` is imported or called.

`execution-contract.template.json` has known current Native refs but null future Web/source/ownership refs and `profileConfirmedByRoot=false`. It intentionally fails the executable schema. It is not an execution certificate. The owner fills a new immutable contract with actual completed originals and an explicitly confirmed distinct paired profile/workId. Do not edit the old C1/G2 contract to make it pass.

## Native before/after meaning

The actual Native runners preserve canonical `inputs-before.json` and a binding with `sourceChangedDuringRun=false`; they do not preserve a separate historical after-manifest. No such old file is generated here.

`selectedNative.<game>.inputsAfter` must reference a **genuine later current observation**: that game's actual fresh `BuildInputs.json` or its actual `current-source-after-proof.json`. It must differ from the before ref, have the same complete rows/fingerprint, and have an actual `startedUtc`/`observedUtc` after Native completion. It must also equal the selected actual build input or post-proof source ref. An alias to the old before file is rejected. The Native binding proves unchanged inputs during Native; the newer actual observation plus the consumer's fresh pre/post enumeration proves current equality.

## Fresh build record shape

The existing GospelWeb producer draft and separately prepared CardWeb producer use the same caller/event shape:

```text
web-build-summary.json:
  status: completed-build-and-witness-only
  publishReady: false
  baselineReady: false
  sourceFingerprint: fixed current game FP
  commonBuild: {argv, startedUtc, finishedUtc, exitCode:0, log:{file,sha256,bytes}, ...}
  proofProcess: {argv, startedUtc, finishedUtc, exitCode:0, log:{file,sha256,bytes}, ...}
  processEvents: [the exact commonBuild and proofProcess objects, ...]
  wrapper, contract, commandPlan, wholeAttemptIndex, proof, comparison: actual regular refs
  runtimeIdentity: actual exact-runtime-witness-candidate or paired-recovery-required
```

Each schema `successfulInvocation.completedUtc` is the corresponding event's exact actual `finishedUtc`; the remaining argv/start/exit/log fields also match exactly. `callerResult` is the **same** successful actual summary for build and proof. `wholeAttemptIndex` has `status=complete` and every actual regular file row. The recorded wrapper path contains `/RecordedHarness/Tools/...`; its preserved bytes must match the actual current Tools source.

The `freshBuilds.<game>` fields bind:

| Field | Actual original |
|---|---|
| `buildRoot` | Absolute parent containing `<game>/Web` |
| `inputs`, `evidence` | Persistent raw copies byte-equal to actual build originals |
| `runtimeCoreFiles` | Exactly index/data/wasm/framework/loader identity rows |
| `completeRuntimeManifest` | Actual list, or object with `completeRuntimeFiles` (the c597 index can serve) |
| `durableProofIndex` | Actual `build-proof-index.json`, scope completed-build-proof-only |
| `resourceRestorationLedger` | Actual `<task>/PipelineState/web/record.json`, three restored pipeline files and Before/After |
| `sourceBeforeAfterLedger` | Actual `current-source-after-proof.json` with game/full files/fingerprint/observedUtc after proof |
| `warningLedger` | Actual retained warning/log control; BuildEvidence warning count remains checked |
| `runtimeDiffAgainstRollback` | Actual comparison with completeFreshRuntime and all5 original/current roles |

`GeneratedProjectAdditions/record.json` is the sibling under the same task directory and must have `status=restored-approved-inputs`. Every retained original is checked. Gospel `LegacyResourceExclusions.json` and every restored original source/meta are checked separately.

The comparison binds `oldRestorationReport`, old/new name/SHA/size/stream-equality fields, complete runtime and classification. Changed bytes cannot be labelled exact. The c597 step only preserves a successful build's original witness; its own exit0 is never treated as proof that Unity ran. Both genuine caller process events and logs remain required.

## Plans, audio and ordinary input

| Selection | Exact authored objects | PCM | Actual reload calls |
|---|---:|---:|---:|
| Card PC |162 /48 required|4|2|
| Card touch/public |163 /47|4|2|
| Gospel PC completed-book candidate1db5e4… |154 /63, original117/48 prefix intact|4|3|
| Gospel touch/public |86 /34|4|2|

Initial `page.goto` is outside the plan. Existing plans therefore have initial Navigate1 + actual reload2 = loaded3, navigation0/1/2. Gospel PC adds its separate third reload = loaded4, navigation0/1/2/3. These are not interchangeable counts. Old116/85 versus current117/86 is the v2 addition of one actual `continued-g2-reading` checkpoint and one required name; it is not an initial-navigation convention.

The unchanged JourneySmoke handles actual pointer/key/touch input, screenshot-guided review, reloads and strict request/cache/media classifiers. Its `review_input` still has12 rounds, at most20 ordinary actions and600s response waits. No game fields, save payloads, random seeds, synthetic progression or pose state are injected.

The C1 audio method is copied verbatim with only four additional leading spaces required by the new nested subclass. Removing those exact four spaces yields4,994B / SHA `9c92ff461951534b8396e96da2e8b7e5d24e2d002c4b95ff46b98ee522b575bd`, checked against the original C1 source at actual preflight. All three existing strict Card revision admissions and all predicates are preserved. Existing base sources are pinned. Source loading uses verified raw compile/exec plus a unique absent pycache namespace with bytecode writes disabled for nested imports.

The exact ordered4 PCM labels and immutable raw summaries are required. Finite signals, six samples, running context, isolated music, one source, audible fraction≥.5, RMS>1e-5 and advancing bundled Card source/role diagnostics remain. Gospel's four music PCM windows and the **separate** actual narration key0→playing Book stop→nextkey1 classifier are both required. Music PCM is not human voice acting approval.

Both original reload paths, real Save input, actual viewport/capabilities, every required checkpoint and **exactly one** passed final-runtime gate are checked. Complete runtime comparisons use file/bytes/SHA identities; the inherited selected-game final check also preserves its complete original rows including modification time. Core5 is not confused with the much larger complete Card runtime.

## Ownership, source and outputs

The actual owner supplies an acquired exact-owner common record. The consumer only calls existing `cycle_guard.py check`; it never acquires, releases, steals, extends or repairs a lease. The owner retains normal CAS release responsibility. A unique immutable copy of the acquired record is suitable for the contract; a copy still undergoes the real remote ownership check. The browser attempt also stores its actual acquired record bytes, so later normal release does not erase the observation.

After each remote check the consumer recalculates original deadline minus60s and installs an outer Linux SIGALRM cutoff. This bounds an inherited unanswered600s review wait without changing its predicates. Existing external owner deadline/process-group control remains necessary. No automatic extension or input deletion is used to fit a PC attempt. Failure/partial originals remain in the unique attempt; a fresh bounded batch is the owner's next action.

The entire output root must be absent before invoking inherited main. This prevents its archival helper from moving an earlier attempt. All lexical ancestors are checked for symlinks before resolution. Critical controls, raw reports, source snapshots and copied harnesses remain regular persistent Artifacts files.

The consumer independently enumerates all current authored rows and compares them with both Native and new build full rows before/after. Git HEAD and the recorded **tracked-file** dirty boolean must also remain equal. `--untracked-files=no` deliberately excludes generated Artifacts from that boolean. It is not a claim that untracked code was committed: local execution records actual frozen bytes; public execution separately requires the installed consumer, every known actual producer/helper and Card's9 source/tool files to equal their bytes in the clean manifest source commit. Source art LFS hydration and the broader source integration/CI gates remain the owner's responsibility.

## Local and public are separate phases

Local execution needs both actual current Native+fresh builds, exact rollback and source/owner controls. It does not require a future public audit, final public game report or release receipt.

Public execution additionally requires the exact new paired MF, complete stage15413 paths/<985,000,000B, ordinary current Git blobs and nontruncated tree, actual clean main CoreCI/Pages runs, current Core285+ input/results, explicit runtime diff and all four current local game/profile Root reports. Gospel's actual completed PC19 report is explicitly identified. Portal Root binds the exact stageReady refs; it is not the final game-public Root review or final baseline seal.

`publicReady` references use the schema0fbb fields. The opaque proof formats have these explicit minimum fields:

- `wholeStageAudit`: statuspassed, sourceCommit, manifest ref, fileCountIncludingManifest15413, siteBytesIncludingManifest, nonempty all-passed checks.
- `explicitRuntimeDiff`: statuspassed, current manifest ref, rollback Restore01 ref, exact sorted differences `{file,oldSha256,newSha256}` from old/new MF files.
- `currentCore`: actual existing Core binding with statuspassed/exitCode0/sourceChangedDuringRunfalse, complete inputs and actual TRX results ref. The unchanged current Core enumerator is source-pinned.
- CI/Pages: original GitHub Actions API run objects, completed/success, exact head_sha, main branch and respective `.github/workflows/core-tests.yml` /`publish-web.yml` path.
- `bothGameLocalRootBundle`: statusverified-local-root-bundle, fixed sourceFingerprints, exactly4 attempts with actual reviewed report refs, one per game×PC/touch. `gospelPC19CompletedBookRoot` is that same actual reviewed Gospel PC report ref.
- Current tree: exact commit, truncatedfalse, files map with mode100644/gitBlob. Current whole audit: statuspassed, sourceCommit/deployedCommit/manifest/currentGitTree/baseUrl/gameRevisions, complete files and capturedSources, auditCounts, startedUtc/checkedUtc, screenshots/checks/visualReview. Captured sources use source/file plus preservedFile and SHA; copies/current source/clean source commit must agree.

The complete-public audit has explicit image mode: strict inherited15350 +63 fresh nonimages, or actual fresh-all15350 JPEGs +63 nonimages =15413 current GETs. The strict inherited branch checks all original16 input refs, original fresh response rows/body identity/Git blobs and exact dated provenance/time. Fresh-all requires its own actual current15350-row audit, with no inherited image rows. Missing/changed historical optional chain refs are reported as unavailable; they cannot make the inherited branch pass. The rollback always still verifies essential original MF/tree/receipt/minimum plus every actual regular path, SHA/size/total and original947890 Git blob. This never recreates the missing historical G2 seal.

`public_bytes` is a full override and never calls the legacy method. It verifies the actual canonical current MF body before navigation and after the session, preserves both raw bodies, reuses only the independently bound complete current audit for bulk JPEG delivery and directly TLS-GETs current nonimage runtime bodies. Status200, identity encoding, exact resolved URL/body SHA/size and actual loader URLs remain mandatory. Current source and both actual local runtime scopes are rechecked after input. It uses inherited proxy/CA trust; no certificate or permission bypass is introduced.

## Root attachment and independent verification

Every attempt freezes `mechanical-report.raw.json`, `original-leaves-before-review.json` and `raw-report-checkpoint.json`. Root later changes only `report.json.visualReview`. Root must include exact mechanicalReport, rawReportCheckpoint and originalLeaves refs, current per-game/both-game fingerprints, complete runtimeFiles and full original screenshot rows. `root-review.template.json` stays pending and contains no invented approval.

The verifier checks raw SHA/size, checkpoint/leaf refs and every original leaf. Only the pre-review report.json ledger row is resolved against the immutable raw bytes, since its sole permitted change is the Root attachment. Replacing visualReview in the raw object must equal the reviewed whole JSON. Every approved shot must equal a complete current original row including name, navigation, canvas and time. Card requires all authored semantic required originals; Gospel retains all current Reading/Book/Continue originals and the separate PC19 settled Book/lastspread/save/reload/Continue frames. Native's one-frame fade image cannot substitute.

Review the local reports before moving HEAD. The standalone review verifier deliberately requires the same current source metadata. Later public verification consumes the already reviewed local raw/original evidence with the same full source/native/build identities; metadata-only source integration is separately bound by current clean main/CI/stage evidence. A product or tool change invalidates the relevant frozen proofs.

## Owner-only invocation after adoption

All paths passed to the wrapper's CLI guards are normalized absolute paths. The proposed additional flags are consumed by this wrapper; all other flags reach the unchanged inherited parser.

```text
python3 /workspace/firstgame/Tools/Autonomy/recovery_web_smoke.py \
  --repository-root /workspace/firstgame \
  --paired-contract /workspace/firstgame/Artifacts/.../actual-contract.json \
  --game FantasyCards --release-profile nativity --profile desktop \
  --plan /workspace/firstgame/Tools/Autonomy/capability_desktop_plan.json \
  --build-root /tmp/<actual-fresh-card-root> \
  --output /workspace/firstgame/Artifacts/Validation/Autonomy/<new-absent-attempt> \
  --renderer x11-mesa
```

Gospel PC selects the exact proposed-v2 plan1db5e4… and actual Gospel build root. Touch uses mobile-landscape and exact per-game mobile plan. Public also uses **mobile-landscape**, phasepublic-browser and `--base-url https://ka8877.github.io/firstgame/`; public is not an inherited CLI profile value. The inherited `--release-profile nativity` selects modern implementation behavior while the report records the explicit confirmed paired releaseProfile.

After actual Root review, invoke only:

```text
python3 /workspace/firstgame/Tools/Autonomy/recovery_web_smoke.py \
  --repository-root /workspace/firstgame \
  --paired-contract /workspace/firstgame/Artifacts/.../same-original-contract.json \
  --verify-reviewed-report /workspace/firstgame/Artifacts/.../FantasyCards/report.json
```

The review command launches no browser, performs no live lease check and never grants release readiness. It uses immutable acquired snapshots for the historical browser timing. Current source/metadata and all real native/build/control originals must still match. Do not substitute a different contract path for the same-original contract ref recorded by the attempt.

This proposal has no publish/deploy/seal operation. Runtime imports, real schema validation, actual browser execution, visual Root approval, current public evidence and final release decisions remain unperformed and belong to the execution owner/Root.
