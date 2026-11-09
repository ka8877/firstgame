# Process containment hold after the actual CardWeb01 timeout

The historical Card caller `a6bc743a…` timed out, but Unity had entered a new
session and continued after the original shell process group was empty. The
attempt remains failed. Its evidence and the separate exact-source restoration
are retained in `Docs/Autonomy/Evidence/CardWeb01-timeout-recovery`.

The installed Card caller now exits before `main()`. Do not restore or rerun its
historical bytes. The uninstalled Gospel V2 candidate `6bf95594…` uses the same
group-only cleanup pattern and must also remain uninstalled and unexecuted.
Its earlier static audit does not establish containment of a `setsid` or
double-fork descendant. No new Root approval is implied by these candidates.

Use only a separately reviewed successor that tracks owned process identities,
handles adopted descendants, proves bounded cleanup, and passes isolated real
`setsid`/double-fork/exit-code/unrelated-sibling checks. A new exact contract,
restored complete source, a fresh owned lease and all existing Native/build/
browser/public/Root/source gates remain mandatory. Failed evidence must stay
failed; later Unity output is not a completed common build/proof pass.

The previously completed Native8/Native16 runs and their original approvals are
unchanged. Their frozen historical harnesses are not modified by this hold;
future timeout handling must be reviewed before those harnesses are reused.
Current public G2 remains unchanged and readiness remains false.
