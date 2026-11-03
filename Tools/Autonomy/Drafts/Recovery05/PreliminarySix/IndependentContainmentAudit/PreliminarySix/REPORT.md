# Preliminary Candidate03 six-case original audit

This is a read-only audit of the actual immutable b5b8 candidate, not acceptance or a production/product pass. The6 reported passes are consistent with raw receipts, copied helper source, mirrored receipt originals, actual dummy UID/start identities and source driver.

| Case | Event/receipt exit | Owned/reaped identities | Observed cleanup |
|---|---:|---:|---|
| normal | 0 | 1/1 | ECHILD + held-pidfds-exited + violations0 |
| exit-seven | 7 | 1/1 | ECHILD + held-pidfds-exited + violations0 |
| spawn-setsid | 124 | 2/2 | ECHILD + held-pidfds-exited + violations0 |
| doublefork | 124 | 3/3 | ECHILD + held-pidfds-exited + violations0 |
| spawn-ignore-term | 124 | 2/2 | ECHILD + held-pidfds-exited + violations0 |
| parent-exit-orphan | 0 | 2/2 | ECHILD + held-pidfds-exited + violations0 |

All six receipts say subreaper verified before spawn and at completion, no discovery errors/live-owned/violations, no overlap between signalled identities and baseline identities, and complete bounded cleanup. The actual unrelated sentinel is present in each recorded baseline and the source driver directly checks its Popen.poll() stays alive after each case. The driver later intentionally cleans up its own sentinel; this is not a claim of permanent sentinel survival.

The setsid fixture records changed session/group IDs; doublefork also records the adopted descendant. These records match the helper's held identities by PID/startTicks/UID; mutable SID/PGID are not treated as immutable identity. The ignore-TERM descendant records SIGTERM then SIGKILL and a -9 reaped exit. Parent-exit-orphan preserves the root's0 outcome while reaping its actual descendant.

No interrupt, wrong-UID or wrong-start refusal test exists here. This cannot close the required final9-case acceptance. Any helper source change needs a new complete owner-run test set. Candidate01/02 failed originals remain in place. Native/Game/Root product gates are unchanged and are not approved by this result.

No test, signal, candidate import, Unity, browser, network, Git or live process inspection was performed by this auditor. Details and exact refs are in observed-originals.json.
