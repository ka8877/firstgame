# b5 containment source review — preliminary only

Only source text/AST/JSON and exact file hashes were read. No candidate/fixture import or execution, process inspection/signals, Unity, network, Git or existing source/artifact mutation was performed. All production/acceptance/release readiness remains false. The actual owner’s6 preliminary cases are not a final9-case acceptance, and this source review is not a runtime result.

The immutable b5 helper is38591B, SHA b5b8ec620af79e4bbd9b74f0c3db5cf36938f25850887c211e2b9c9d0056dfd4. At read time both mutable and Proposed copies had the same bytes. Final mutable source may still change; every final helper/fixture/runner change requires a fresh exact9-case actual owner run and new independent review.

## Source controls present

- `signal_identity`114-148 targets only an already-held pidfd for a proved nonbaseline identity, checks pidfd liveness and current startTicks/UID again and refuses mismatches; no numeric PID/group/name fallback is present.
- `configure_subreaper`153-172 requires Linux pidfd/waitid, a dedicated single thread, normalized SIGCHLD, PR_SET/GET_CHILD_SUBREAPER and parent-death signaling, then establishes kernel ECHILD before spawning.
- Tracker parent edges derive from current direct/adopted child parentage, a still-live owned parent or an exact kernel-waited child. Baseline identities exclude preexisting unrelated processes. Unknown or UID-changing descendants produce violations or failure to establish ECHILD; an empty `/proc` scan cannot make completion true.
- Reaping uses WNOWAIT first and exact waitpid for the peeked owned child; root status is reaped separately once and copied into Popen.returncode. `terminal` relies on held pidfd readability, so leader zombie state alone is not exit proof.
- Completion requires kernel ECHILD, no live held-pidfd identities, no violations/discovery errors and still-verified subreaper. Root exit0 is not enough. Controller refuses to return a successful child event without this actual receipt.
- Execution stays inside the inherited safeEnd; cleanup is a separate bounded10seconds, with outer receipt wait capped by executionEnd+12. Outer cancellation targets the exact new supervisor pidfd and never SIGKILLs its group. Missing/unproved cleanup remains125/false and prevents proof/restore continuation.

## Boundaries the final nine-case contract must close

1. At513-516 timedOut precedes interrupted. An interrupt before timeout yields130, but cancellation during timeout cleanup can leave both flags true and return124. The final contract must define that edge honestly; do not simply label every interrupted flag130 without observing actual outcomes.
2. `validate_acceptance`349-385 binds exact9 names, booleans, expected outcomes, helper/fixture/runner/Root, regular source originals and cleanup receipts. It does not yet directly bind each case name to its actual fixture mode or explicit escape acknowledgments. A same pinned fixture executing a simple sleep under a different argv can satisfy a timeout receipt without exercising setsid/doublefork. Final runner/acceptance must cross-check the real mode and its raw acknowledgments/identities.
3. Baseline signal exclusion is checked against receipts, but survivor proof for the actual preexisting bystander and UID/start refusal target must be retained and independently bound, rather than relying only on declared aggregate0/true fields. The fresh actual9-case review is where these observations must be assessed.

## Adapter product gates

AST comparison against preserved originals found only `Lane.child` and `main` changed, `live_group_members` removed and `configure_containment` added. Twenty-six functions per adapter were AST-identical, including Native/Root/source/restore/runtime/proof checks. Card remains fixed42b/exact8/Root0d/source9; Gospel remains fixed255e/exact16/current Root/legacy/fullrows. The new layer introduces no authored FP allowlist or replacement Native result. Imports/containment constants and fresh output namespaces change separately. Gospel's duplicate `import types` is redundant but not a gate change. These are source observations, not successful integration or product execution.

The exact adapter refs and unchanged-function names are in observed-b5-preliminary.json. Production acceptance remains blocked pending final immutable source and actual9-case/Root evidence.
