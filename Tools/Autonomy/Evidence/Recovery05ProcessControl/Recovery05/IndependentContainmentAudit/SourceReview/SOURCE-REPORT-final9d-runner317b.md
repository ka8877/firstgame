# Final9d / runner317b — source-only final delta review

This is a new independent source review for **FinalInputs02**, not an edit to the earlier AAD source review. The prior machine JSON bf687b4219efb7acae0ec26da545212dd81163bdf76a54bbf4daddbd311ec7f0 and MD67cae2ff… remain unchanged as pre-execution superseded history. The earlier review cannot approve runner317b; helper.validate_acceptance's three-source SHA/size equality rejects that reuse.

| Current frozen source | SHA256 | Bytes |
|---|---|---:|
| helper.py |9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad|44980|
| runner.py |317b7d42af72d74826ff160be1612aae16ebcd9e3cda6b13ca47f0280d5dfac7|40202|
| fixture.py |d029f1f374e2f36a2daf4ec71e06316a1248f916d48a471bbe01bf39861a9760|5568|

The helper and fixture are byte-identical to the complete sources read in SOURCE-REPORT-final9d.md; that preserved report explains held-pidfd identity fencing, kernel ECHILD/allchildren cleanup, UID/start refusal, baseline survival, reserve12/testLanecap15, cancellation130 priority, exact9 cases and unchanged product gates. Those controls remain unchanged. Full current source AST parses, and all three sources still have0 assert nodes: actual need/ValueError checks remain active under Python−O. No candidate import or execution was used to make this determination.

The complete runner diff adds only a stronger interruption-time bound. Immediately after successful signal_identity/pidfd_send_signal return it records sigintCompletedMonotonic. For the before-deadline subcase, completion must be strictly before executionEndMonotonic, so the actual syscall occurred inside the before window rather than merely being requested before it. verify_receipt independently requires completion≥request and, for before, completion<deadline; for after-timeout, request≥deadline and completion≥request. Both still require actual sent=true, correct interrupted/timedOut flags, actual outcome130 and complete kernel/pidfd cleanup. No real124 is relabelled into130. All normal/timeout/escape/adoption/bystander/refusal source behavior and raw source/original bindings remain unchanged.

The proposed Card caller e5ccccc1…/40547B and Gospel caller b36514be…/54692B retain26 AST-identical product functions each against the preserved predecessors. Native8/Root0d/source9/fixed42b and Gospel16/fixed255e/voice/legacy controls remain product gates. This process-source review neither replaces them nor adds a new product Root-test gate. The actual operator's isolated-process-test acceptance remains separate from product/public Root approval.

**No remaining confirmed static blocker was found for these exact three frozen sources.** Actual9-case execution is still0 at this source review and must be performed by the sole actual owner with these exact bytes, fresh unique output and independent result assessment. This source report creates no test success, operator acceptance, publication, baseline, browser/performance/audio or product visual approval. All such readiness remainsfalse. No process query/signal, Unity, network, Git, existing source/test-original mutation or candidate import/run was performed by this auditor.
