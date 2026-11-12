# Post-checkpoint Card cache space — unexecuted proposal

This new Draft sits outside the frozen `GospelNpr20261111*` Draft directories.
It has not executed any cleanup, test, Unity/browser, Git, deployment or archive
operation. Earlier frozen sources, proof and checkpoint files remain unchanged.

The actual completed checkpoint contains15,302files in81parts, joined archive
2,601,792,134B; parts plus ordinary Git objects need working space. This proposal
uses only legitimate generated Card Library cache data, with actual free-space
measurement afterward. Source sizes and allocated filesystem sizes differ.

| Current selection | Files | Logical bytes | Previously allocated file bytes |
| --- | ---: | ---: | ---: |
| ShaderCache files | 1,624 | 5,811,232 | 10,018,816 |
| BurstCache files | 379 | 91,788,853 | 92,471,296 |
| two PlayerData blobs | 2 | 100,143,974 | 100,147,200 |
| Total | 2,005 | 197,744,059 | 202,637,312 |

Only `PlayerDataCache/WebGL/Data/data.unity3d` and `resources.resource` are
selected there. All five PlayerData YAML/JSON/config files remain, including
ScriptsOnlyCache and DataBuildDirtyInfo. All674 cache directories remain.
Every cache file must be regular, single-link and listed in the admitted
file-type/name scope. Symlinks, specials, extra file types or a changed inventory
fail closed. The result reports actual free bytes including ledger overhead;
the table is not a claimed cleanup or a guaranteed net saving.

`Tools/Build/reclaim-quest-compiler-cache.py`,
`reclaim-chronicle-compiler-cache.py` and `reclaim-polish-compiler-cache.py`
already explicitly allow inactive PlayerDataCache as generated build output.
Existing compiler-cache tools and installed Burst source establish ShaderCache
and BurstCache. This proposal preserves PlayerData metadata rather than
clearing the whole generated cache. A future Unity build must regenerate its
missing blobs/compiled caches; no cold rebuild is being certified here.

The script imports the exact reviewed V2 support modules read-only, never their
cleanup entry points. It retains the narrow Gospel Library alias admission,
Native14/Web10/proof/runtime/LastBuild/DB checks and fresh complete protected
Card verification (15,379files/869,460,781B). Both current source arrays must
equal the already immutable actual CardBee02 source snapshot. Existing complete
source and baseline snapshots are referenced by their exact SHA, avoiding
another14.8MB duplicate and preserving space.

It additionally hashes every retained Card Library Artifacts, PackageCache and
Bee member, including file bytes and file/directory metadata, and compares
their deterministic aggregate before and after. Those trees, all logs/proofs,
the full Card runtime, import DBs and both LastBuild reports are not deleted.
The exact completed checkpoint index and successful archive wrapper/stdout
must already exist and remain unchanged. No old CONTROL original is modified;
only new maintenance inventory/result files are written under active Release.

Root must read the full source and frozen hashes, wait for its current Git
operation to finish, and use a fresh actual owned wrapper. This proposed command
has not been run:

```bash
source /workspace/firstgame/Artifacts/Validation/Autonomy/GospelNprCameraResume-2026-11-11/activate-camera-resume.sh
python3 "$FIRSTGAME_CONTROL_ROOT/run-owned-npr-resume.py" PostCheckpointCardCacheSpace01 -- \
  python3 /workspace/firstgame/Artifacts/Validation/Drafts/GospelNprPostCheckpoint20261111CacheSpace/prune-post-checkpoint-card-caches.py \
  --owned-run-label PostCheckpointCardCacheSpace01 --confirm-post-checkpoint-cache-prune
```

The exact live wrapper and its completed idleBefore are required. Separate
runtime/compiler idle checks run before inventory and immediately before
deletion. A durable `xb`+`fsync` inventory records every cache file's SHA and
metadata, allocated blocks, directories and protected-original references
before any unlink. All selected files are rechecked immediately before unlink;
all retained files/directories and protected originals must match afterward.
No source/baseline duplicate is created. The final result is another new file;
no existing original or failure is overwritten.

The new originals are
`Release/post-checkpoint-card-cache-<label>.{inventory,result}.json`.
Preserve this whole new Draft as Closure `lateOriginals`; actual execution and
maintenance receipts are new CONTROL originals. Do not insert them into the
already frozen checkpoint. This operation conveys no product, performance,
test, rebuild, public, archive or user aesthetic approval.
