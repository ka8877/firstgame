# Proposed isolated containment tests — UNEXECUTED

These files are a source-only candidate. Their author has not imported or run
the helper, fixture or runner, queried live processes, sent a signal, or run
Unity, network, Git or install operations. Only the actual sole execution owner
may choose a fresh batch and execute this candidate. This directory contains no
test result and confers no approval. `publishReady` and `baselineReady` stay false.

The helper source inspected when drafting was
`b5b8ec620af79e4bbd9b74f0c3db5cf36938f25850887c211e2b9c9d0056dfd4`
(38,591 bytes). A later reviewed metadata gate change requires the actual owner
to pin its new SHA; the runner does not hardcode an obsolete helper hash.
The predecessor fixture was
`62b7b3596c7d2d233632fac8cbddc7c9e6930b2e3eba857a14f6756a5b4054f3`
(1,933 bytes). It and all prior attempts remain untouched.

The actual owner should copy the three candidate files into a new immutable
inputs folder if desired, obtain SHA-256 values by source inspection, and invoke
the runner with these required absolute arguments:

```text
python -B /absolute/path/isolated_containment_runner.py
  --repository-root /workspace/firstgame
  --helper /absolute/path/owned_process_containment.py
  --helper-sha256 <actual-reviewed-helper-sha256>
  --fixture /absolute/path/process_fixture_successor.py
  --fixture-sha256 <actual-fixture-sha256>
  --runner-sha256 <actual-runner-sha256>
  --batch-root /workspace/firstgame/Artifacts/Validation/Autonomy/<new-absent-batch>
  --cap 2
```

This is an invocation template, not a claim that it ran. Use a new absent batch
for every attempt, including failures. The runner refuses an existing batch or
unmatched source SHA, captures exact source copies, and loads the helper with
`ModuleType`, `__file__`, `sys.modules` and
`exec(compile(exact_hashed_raw_bytes, exact_helper_path, 'exec'), module.__dict__)`.
It never imports the helper or reads cached helper bytecode. It constructs only
`Controller(policy, actual_repository_root, test_mode=True)` so original receipt
references are directly repository-relative. Every evidence path, fake Lane
output and dummy output remains under the new batch. The fixture never opens
game files. Python dummy argv starts with the actual interpreter and pinned fixture.

Each fake Lane has a shared monotonic envelope of `case_cap + 12 + 3` seconds:
the unchanged case execution cap, the helper's cleanup10/report2 reservation,
and three seconds of setup margin. Its UTC datetime deadline is another 60
seconds beyond that envelope. The helper still receives only the selected
`.5..2` second case cap and must keep execution plus cleanup inside shared safeEnd.

| Required case | Actual fixture behavior | Required observation |
| --- | --- | --- |
| normal-exit | Records its identity and exits | Root/outcome 0 and cleanup proof |
| nonzero-exit | Records its identity and exits 7 | Root/outcome 7 and cleanup proof |
| setsid-timeout | Root spawns a leaf that calls setsid | Live escaped identity before teardown; outcome 124 |
| doublefork-timeout | First child calls setsid, forks, then exits | Live second child adopted by the subreaper before teardown; outcome 124 |
| root-exit-orphan | Same real doublefork; root exits after observer ACK | Preserved root/outcome 0 and orphan cleanup |
| ignore-term | Live setsid leaf installs SIG_IGN for TERM | Actual TERM then KILL of that exact leaf; outcome 124 |
| interrupt | Two separate sleep roots ignore TERM after readiness | Actual held-pidfd SIGINT before deadline, then a fresh invocation after timeout cleanup begins; both outcome 130 and proved cleanup |
| bystander | Runner starts an unrelated sentinel before supervisor launches a setsid tree | Live escaped child observed; sentinel same identity alive after timeout, no supervisor signal; separate owner cleanup |
| stale-identity | Runner owns a live sentinel and holds its pidfd | Real startTicks/UID metadata mismatch refusals, no signal call, alive before/after; separate exact cleanup |

The successor fixture exposes ready files atomically without overwrite. A
separate observer verifies actual live kernel identities and writes an ACK
before a root-exit may proceed. It requires each escaped identity to appear in
the actual supervisor receipt and observes readiness before any teardown signal.
Each escaped command exposes `escapeEvidence` and `escapeAck` full references;
the actual escape JSON has flat PID/PPID/PGID/SID/startTicks/UID/token/label fields.
The doublefork proof includes the intermediate identity, initial grandchild
parent edge, actual intermediate exit, changed parent edge to the supervisor,
and a session distinct from the root. Prevented or unobserved forks fail.

Only the interrupt observer signals a supervisor. It verifies the supervisor is
the runner's direct child, the root's actual parent, and the command-spec parent
and argv match, then holds and revalidates the supervisor pidfd/UID/startTicks.
It sends SIGINT through the helper's unchanged `signal_identity` implementation.
The after-timeout subcase holds that same exact supervisor pidfd from readiness,
waits for the actual `cleanup-began` journal event and passed execution deadline,
then sends SIGINT. The helper must publish `supervisor-identity.json`, the cleanup
event, and cancellation-priority classification. Its actual after-timeout result
must contain `timedOut=true`, `interrupted=true` and outcome 130. The report binds
both `containmentReceipt` and `afterTimeoutReceipt`; no timeout is relabelled by
the runner. The bystander has a separate `real-preexisting-bystander` receipt
with actual before/after live identities, non-targeting and owner cleanup proof.
The runner never signals a numeric PID, name, process group, session, licensing
client or process discovered by a broad scan. The helper itself performs its
reviewed baseline/descendant metadata scans in the actual owner's execution.

For stale metadata tests only, a transparent helper-local `signal` proxy counts
every actual `pidfd_send_signal` call and forwards it unchanged. Both refused
calls target the actual held dummy pidfd but declare a changed startTicks or UID.
The signal is SIGCONT, so an erroneous accepted call is counted as a real
regression. A pass requires both real ValueError refusals, zero actual calls,
and unchanged live identity before and after. This does not claim host PID reuse
or an actual credential change. The original signal module is restored before
the actual owner's separate exact-identity TERM/KILL cleanup and exact Popen wait.

No global wait is added: the runner waits only its own exact direct sentinel
Popen handles; Controller retains its exact supervisor wait and the supervisor
retains its exact root status. A containment failure stops subsequent commands.
The runner still attempts the separate cleanup of its own known sentinel.
Failures, skipped cases, source copies, readiness observations, owner cleanup,
stdout and all supervisor originals remain in that unique attempt.

The report binds actual full references for helper, fixture and runner, and
each command case retains the untouched supervisor result, command-spec JSON,
lineage JSONL, actual command stdout and supervisor stdout, plus fixture and
observer records. Stale identity gets its actual identity-refusal receipt.
Only nine actual passes with zero production invocations, zero baseline signals
and proved cleanup can produce report status `passed`. The runner creates no
Root approval, independent audit, production acceptance or product readiness.
The actual sole operator and independent source reviewer must supply whatever
review receipts the final exact helper requires after actual results exist.
