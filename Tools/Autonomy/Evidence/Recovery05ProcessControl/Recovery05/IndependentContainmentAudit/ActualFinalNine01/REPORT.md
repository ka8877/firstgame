# Actual FinalNine01 result audit

The exact9d/317b/d029 owner-run originals are consistent with the report statuspassed. This is an independent read-only evidence audit, not another process run or sole-operator acceptance. It grants no Native/game/Web/browser/public readiness.

The report SHA is4261f8df0ce993358fa6a40af9ff0499202e831d899cef99b45841457eddcab6 /69,227B. There are9 named case groups,9 actual supervisor receipts,1 separate negative-refusal receipt with2 actual refusal attempts. Counts are derived from original files rather than coerced to a target receipt number.

| Case | Actual outcome | Owned/reaped | Timeout / interrupted |
|---|---:|---:|---|
| bystander / single | 124 | 2/2 | True / False |
| doublefork-timeout / single | 124 | 3/3 | True / False |
| ignore-term / single | 124 | 2/2 | True / False |
| interrupt / before-deadline | 130 | 1/1 | False / True |
| interrupt / after-timeout-during-cleanup | 130 | 1/1 | True / True |
| nonzero-exit / single | 7 | 1/1 | False / False |
| normal-exit / single | 0 | 1/1 | False / False |
| root-exit-orphan / single | 0 | 3/3 | False / False |
| setsid-timeout / single | 124 | 2/2 | True / False |

Each supervisor records subreaper verification before spawn and at completion, ECHILD, all held pidfds exited, complete cleanup, no live-owned/discovery-error/violation/error, and all owned PIDs reaped. Every journal signal targets a recorded owned PID/start/UID identity outside the baseline. Each event reserves12s and executionEnd+12 remains within its original sharedSafeEnd. All command/spec/helper/fixture paths and raw source copies match the independently reviewed source bytes. All case original references, stdout/spec/journal/source artifacts and actual established setsid/doublefork/adoption acknowledgements were rehashed.

The two interrupt observations include successful SIGINT syscall completion time. Before-deadline completion precedes executionEnd; the after-timeout request follows actual cleanup-began with timedOuttrue. Both actual outcomes are130, and the late receipt preserves timedOuttrue. This proves the test's send phase and recorded supervisor handling; it is not an independent kernel handler timestamp.

The real unrelated bystander retains the same PID/start/UID before/after helper cleanup and is in the recorded baseline. The test owner later cleans up only its own fixture. The negative fixture is alive before/after2 deliberately wrong metadata declarations (startTicks and UID vector); both calls are refused before any forwarded signal syscall. It later has separate exact-owner cleanup. It does not claim actual host PID reuse or a kernel UID change.

All observed production invocations, helper baseline signals and unresolved-owned counts are0. Existing b5six and earliercdb failures are not relabelled. The source-only review remains source-only. Sole-operator acceptance and product/Root/public decisions are separate. No candidate/fixture import, process execution, signal, live-process inspection, network, Git, Unity or browser operation was performed by this auditor.
