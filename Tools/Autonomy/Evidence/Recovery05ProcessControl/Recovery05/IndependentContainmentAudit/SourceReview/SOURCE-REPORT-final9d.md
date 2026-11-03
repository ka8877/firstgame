# Final9d containment source audit — source-only

The three frozen sources were read completely as text and parsed as AST; none was imported or executed. No fixture, process query, signal, Unity, network, Git or Tools/Game/Docs modification was performed by this auditor. The actual owner alone performs the new nine-case run and assesses its original results. At this source review, actualFinalNine01=0. This review reports no remaining confirmed static blocker in these exact sources; it is not test acceptance or product/public readiness.

| Source | SHA256 | Bytes |
|---|---|---:|
| helper |9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad|44980|
| runner |aadcb08ee7d462b2fa2e44da8312a873b8885315ea77f9bae40c35032f45adea|39757|
| fixture |d029f1f374e2f36a2daf4ec71e06316a1248f916d48a471bbe01bf39861a9760|5568|

The exact repository-relative FinalInputs01 refs are in independent-source-review-final9d.json. Copied source paths may differ in the actual result, but both SHA and byte size must match the independently reviewed three-source set. The b5 six-case results and cdb failed attempts remain historical evidence and cannot satisfy this new9d nine-case gate. Any source change requires a new full nine-case result and new source review.

## Owned identity and complete cleanup

The helper requires Linux held pidfds and waitid, a dedicated single-threaded new supervisor, verified PR_SET/GET_CHILD_SUBREAPER, parent-death cancellation and kernel ECHILD before launch. It never selects a signal target by name, numeric PID alone, session or PGID. Admission uses an actual direct/adopted child or a still-live held-pidfd owned parent, with matching startTicks and all4 UID values around pidfd_open. The global baseline is exclusion evidence, not ownership. Signal delivery rechecks the original held fd and current identity; UID/start mismatches fail without a fallback signal.

Root wait status is consumed exactly once, separately from child cleanup; Popen.returncode is set from that actual wait. Kernel WNOWAIT identifies a real waitable child before exact waitpid. A None waitid response means children remain, never empty. Process leader zombie state alone is insufficient: held pidfd exit plus stable subreaper kernel ECHILD is required. Complete additionally requires no live identities, ownership violations or discovery errors. Unattributable/UID-changing/D-state descendants cannot become successful cleanup. Controller refuses successful continuation if the actual receipt does not prove these conditions. No old group-empty scan substitutes for them.

The new transition journal preserves a tracked child's actual PPID/SID/PGID changes, so an already admitted process establishing a new session or being adopted is represented. The fixture/observer use that real kernel identity and token to prove the escape happened before teardown; merely preventing a fork is not the tested behavior.

## Budget and interruption

Controller now refuses spawn when shared remaining≤12, uses budget=min(cap,remaining−12), and fixes executionEnd≤lane.safe_end−12. Cleanup remains≤10seconds and hard receipt end=executionEnd+12, both inside the inherited shared900/lease−60 safeEnd. This reserves cleanup/report time rather than extending the shared execution boundary. Test Lane separately includes cap+12+3 setup seconds, preserving the original .5..2s execution cap; the former cap+2 mismatch is gone. Unproved supervisor cleanup remains a failure, with no broad kill/automatic retry or claim of idle.

Cancellation now takes130 priority even when timeout already began. The source emits the actual cleanup-began event and supervisor identity original. The runner's interrupt group has two separate real invocations: held-pidfd SIGINT before execution deadline, and held-pidfd SIGINT after an observed timedOut=true cleanup-began. Both require outcome130, while the latter must retain timedOut=true as an independent observation. It does not relabel an actual124 as130.

## Exact nine real exercises and acceptance separation

The runner uses only pinned Python dummy argv, a new absent batch and raw compile/exec of exact hashed helper bytes. All gate checks use need/ValueError calls; helper, runner and fixture contain0 assert AST nodes, so Python−O cannot silently erase their gates. Source copies and original helper/fixture/runner refs are retained and checked again after the run. No production Controller is constructed by the runner.

Nine groups cover normal0, nonzero7, live setsid timeout124, real doublefork adoption124, root-exit orphan0, actual TERM-ignore followed by TERM+KILL124, both interruption130 timings, preexisting unrelated bystander survival124, and actual live dummy startTicks/UID refusal. The bystander is created before the supervisor, must be in baseline but absent from owned identities, must remain the same living identity after test teardown, and receives separate exact-owner cleanup. Refusal uses a real held dummy fd and transparently counts actual pidfd_send_signal calls; altered metadata must cause two real refusals, zero sends and unchanged live identity. It explicitly does not claim actual host PID reuse or changed credentials. The fixture observer verifies live escape/adoption and writes an atomic ACK before the root-exit fixture may finish.

The helper production gate requires the exact9 name set and allactualpasses, matching source triplet, per-case original refs/actual command mode/expected outcome/cleaned receipt, escaped SID/PGID/token and journal ownership, preserved bystander/refusal originals and both interrupt receipts. It additionally requires this same-source independent static review and the actual sole operator's later accepted test review with matching report/source refs. This source-only report provides only the independent source half; it creates no operator acceptance, fake test result or Root product approval.

## Product and scope preservation

AST comparison of proposed Card e5ccccc18f0ada598d4659ffc381201140ffb0d8b2e335ff1c0b04e33330b716 /40547B against preserved a6bc, and Gospel b36514be4ef93806bb47f4cef9973781c6d10489123c5b67cd867d85034e63a5 /54692B against preserved6bf955, found26 unchanged functions each. Only Lane.child/main changed, live_group_members was removed and configure_containment was added, alongside source constants/imports/new output paths. Product Native/Root/source/restore/runtime/proof functions are unchanged. Card remains exact8/Root0d/source9/fixed42b/full35334rows/all46originals; Gospel remains exact16/current product Root/full1707/fixed255e/voice/legacy controls. No authored FP allowlist or old failed result conversion was added. New process-test operator acceptance cannot replace those existing product Root gates.

No publication, baseline seal, current public game test, browser visual/audio/performance approval or UI improvement is established by this review. All those remain independent outstanding gates. Runtime results are deliberately reviewed separately by the actual parent owner/auditor.
