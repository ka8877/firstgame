This unexecuted source proposal preserves the historical Card Web01 build while
freeing only its owned temporary duplicate. Root must review the source and run
it under the original current owned wrapper, while canonical Native03 and its
Root review are still selected. No product/test/visual/publication approval is
created. The repaired current C# is deliberately not compared to the old full
source fingerprint: this authenticates an old artifact, never a new pass.

```bash
python3 /workspace/firstgame/Artifacts/Validation/Autonomy/SharpIllustration-2026-11-17/run-owned-sharp-illustration-tmp.py FRESH_STORAGE_LABEL -- python3 /workspace/firstgame/Tools/Validation/SharpIllustration/preserve-card-web01.py --owned-run-label FRESH_STORAGE_LABEL --confirm-preserve-then-remove-owned-tmp-web01
```

The label is a placeholder, not an execution result. The source fixes the exact
old temporary path and exclusive persistent destination:

```text
/tmp/firstgame-sharp-illustration-20261117-builds/FantasyCards/Web
→ Artifacts/Validation/Autonomy/SharpIllustration-2026-11-17/PreservedBuilds/FantasyCards-Web01
```

The actual completed CardWeb01 outer wrapper and all five inner contained
guard/idle/build/proof/guard receipts must pass their original argv/spec/cleanup
checks. The original Native03 inputs, actual11-case XML, captured frozen source
contract, canonical selection and existing Root original review must agree with
historical fingerprint `7a5fca96…`. The old proof index, its copied originals,
summary sidecar and BuildInputs/Evidence must remain byte-identical.

The complete no-ignore `os.walk` inventory must contain exactly15,367 regular
files/877,559,937 bytes: declared runtime15,365 plus the two original top-level
metadata files. Any unknown, unique, linked or special temporary member aborts.
Retained sources are independently verified against old runtime SHA/bytes and
the historical input rows:

- Gallery15,350 JPEGs and Audio10 files: exact existing GameSource assets.
- Core5, including the generated `index.html`: exact original proof Runtime.
- BuildInputs/Evidence: about8MB of bounded JSON/text copies from the original
  byte-identical retained proof, preserving the old file modes.

The destination is new and regular on the retained sources' filesystem. Sources
are linked without chmod, utime, content writes, renames or removals. Original
source inode, device, bytes, mode, owner and mtime must remain unchanged; exact
nlink increments and inevitable ctime changes are recorded. Files and new
directories are fsynced, then the entire persistent build is reread by SHA256.
Protected proof, old Native, canonical images/selection, old Web runs and actual
contained raw receipts are inventoried before/after. Only the five proof Runtime
files may acquire the recorded link-count/ctime changes.

Before the first removal, the old temporary whole tree is reread unchanged,
the historical authentication is repeated, and a durable
`persistent-complete.json` records every old-to-persistent path, identity, mode
and retained source. Then the helper unlinks only each exact authenticated old
temporary file and removes only its enumerated empty directories. It uses no
recursive remover or broad cache/Library cleanup. A final persistent readback
must still match. All original source, proof, Native, raw, old absolute references
and historical statuses remain in place.

Evidence stays under the fresh wrapper's `CardWeb01Preservation/`. Failures retain
the new partial/full persistent tree and every remaining old temporary file;
there is no automatic retry, rollback deletion or inferred success. An
interruption during removal can leave some duplicate temporary paths absent,
but removal starts only after the complete persistent copy and its readback
receipt are durable. Root inspects actual evidence before any fresh attempt.

Web02 must select a genuinely new proof output. This helper never renames,
rewrites or overwrites the original `BuildProof/FantasyCards-Web` tree. No helper,
test, Git, network, product hashing, product storage operation or cleanup was
executed while authoring this source.
