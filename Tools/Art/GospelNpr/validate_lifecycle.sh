#!/usr/bin/env bash
# Root-only execution under the reviewed owned command wrapper.
set -euo pipefail
lifecycle_repository=/workspace/firstgame
lifecycle_attempt="${1:-}"
lifecycle_expected="${2:-}"
[[ "$lifecycle_attempt" =~ ^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$ ]] || { echo 'Unique safe attempt required.'; exit 2; }
[[ "$lifecycle_expected" =~ ^[0-9a-f]{64}$ ]] || { echo 'Selected Native source fingerprint required.'; exit 2; }
[[ "${FIRSTGAME_RELEASE_PROFILE:-}" == gospel-npr ]] || { echo 'Explicit gospel-npr required.'; exit 2; }
cd "$lifecycle_repository"
lifecycle_output="$lifecycle_repository/Artifacts/Validation/GospelNpr-2026-11-11/LifecycleRuns/$lifecycle_attempt"
[[ ! -e "$lifecycle_output" && ! -L "$lifecycle_output" ]] || { echo 'Existing attempt is immutable.'; exit 2; }
mkdir -p "$lifecycle_output/RecordedSources"
export PYTHONDONTWRITEBYTECODE=1 PYTHONOPTIMIZE=0
export PYTHONPYCACHEPREFIX="$lifecycle_output/GeneratedPythonCache"
lifecycle_unity_exit=125
finish_lifecycle() {
    lifecycle_exit=$?
    trap - EXIT
    set +e
    python3 - "$lifecycle_output" "$lifecycle_expected" "$lifecycle_exit" "$lifecycle_unity_exit" <<'PY' >"$lifecycle_output/result.stdout.log" 2>&1
import hashlib, json, shutil, sys
from pathlib import Path
root = Path('/workspace/firstgame')
sys.path.insert(0, str(root / 'Tools/Validation'))
from restoration_source_binding import capture
out, expected, shell_exit, unity_exit = Path(sys.argv[1]), sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
result = {'scope': 'legacy-resource-lifecycle-source-restoration-only', 'state': 'rejected',
          'shellExitCode': shell_exit, 'unityExitCode': unity_exit, 'expectedSourceFingerprint': expected}
def ref(path):
    raw = path.read_bytes()
    return {'file': str(path), 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}
journal = root / 'Games/PathOfDawn/Library/FirstGameLegacyResourceBuild.json'
holding = root / 'Games/PathOfDawn/Assets/Art/Archive/BuildExcludedLegacy'
result.update(journalAbsent=not journal.exists(), holdingAbsent=not holding.exists())
if journal.exists(): shutil.copyfile(journal, out / 'retained-interruption-journal.json')
try:
    after = capture('PathOfDawn')
    (out / 'inputs-after.json').write_text(json.dumps(after, ensure_ascii=False, indent=2) + '\n')
    before = json.loads((out / 'inputs-before.json').read_bytes())
    result.update(sourceFingerprintBefore=before['fingerprint'], sourceFingerprintAfter=after['fingerprint'],
                  completeSourceInputsEqual=before == after)
    log = out / 'lifecycle.log'
    marker = 'FIRSTGAME_LEGACY_LIFECYCLE_VERIFIED normal exception journal-recovery; every source byte and GUID restored'
    result['lifecycleMarkerObserved'] = log.is_file() and marker in log.read_text(errors='replace')
    records = []
    for target, status in (('RestorationLifecycleNormal', 'restored'), ('RestorationLifecycleException', 'restored'),
                           ('RestorationLifecycleRecovery', 'recovered-after-interruption')):
        path = out / 'LifecycleEvidence/PathOfDawn' / target / 'LegacyResourceExclusions.json'
        value = json.loads(path.read_bytes())
        if value.get('revision') != 'gospel-npr-2026-11-11' or value.get('target') != target or value.get('status') != status or not value.get('files') or not value.get('moves'):
            raise ValueError('Incomplete actual restoration record: ' + target)
        records.append(ref(path))
    result['restorationRecords'] = records
    if all((shell_exit == 0, unity_exit == 0, before == after, before['fingerprint'] == expected,
            result['lifecycleMarkerObserved'], result['journalAbsent'], result['holdingAbsent'])):
        result['state'] = 'verified-source-restoration-only'
    result['sourceBindings'] = [ref(out / 'inputs-before.json'), ref(out / 'inputs-after.json')]
except BaseException as error:
    result['error'] = type(error).__name__ + ': ' + str(error)
(out / 'lifecycle-observation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
print(json.dumps(result, ensure_ascii=False))
sys.exit(0 if result['state'] == 'verified-source-restoration-only' else 1)
PY
    lifecycle_check_exit=$?
    cat "$lifecycle_output/result.stdout.log"
    [[ "$lifecycle_exit" -eq 0 ]] || exit "$lifecycle_exit"
    exit "$lifecycle_check_exit"
}
trap finish_lifecycle EXIT
for lifecycle_source in Tools/Build/nativity-task.sh Tools/Validation/restoration_source_binding.py Games/PathOfDawn/Assets/Editor/LegacyResourceIsolation.cs; do
    mkdir -p "$lifecycle_output/RecordedSources/$(dirname "$lifecycle_source")"
    cp -- "$lifecycle_source" "$lifecycle_output/RecordedSources/$lifecycle_source"
done
python3 - "$lifecycle_output" "$lifecycle_expected" <<'PY' >"$lifecycle_output/before.stdout.log" 2>&1
import json, sys
from pathlib import Path
sys.path.insert(0, '/workspace/firstgame/Tools/Validation')
from restoration_source_binding import capture
value = capture('PathOfDawn')
(Path(sys.argv[1]) / 'inputs-before.json').write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')
if value['fingerprint'] != sys.argv[2]: raise ValueError('Current complete source differs from selected Native run')
print(json.dumps({'scope': 'source-snapshot-only', 'fingerprint': value['fingerprint'], 'files': len(value['files'])}))
PY
# Same NPR prepare/native GLCore environment; unique evidence output only.
export XDG_CACHE_HOME="${XDG_CACHE_HOME:-/tmp/firstgame-nativity-runtime-cache}"
export UPM_CACHE_ROOT=/tmp/firstgame-reprise-upm-cache
export TMPDIR=/tmp/firstgame-nativity-temp
export FIRSTGAME_BUILD_ROOT="$lifecycle_output/LifecycleEvidence"
mkdir -p "$TMPDIR"
source Tools/Environment/activate-shell.sh >"$lifecycle_output/activate-shell.stdout.log" 2>&1
python3 Tools/Environment/assert-runtime-idle.py >"$lifecycle_output/idle.stdout.log" 2>&1
python3 Tools/Environment/recover-quest-display.py >"$lifecycle_output/recover-display.stdout.log" 2>&1
bash Tools/Environment/start-display.sh >"$lifecycle_output/display.stdout.log" 2>&1
source Tools/Environment/activate-display.sh
set +e
"$UNITY_EDITOR" -batchmode -force-glcore -projectPath Games/PathOfDawn \
    -executeMethod FirstGame.Editor.LegacyResourceIsolation.ValidateLifecycle -quit \
    -logFile "$lifecycle_output/lifecycle.log" >"$lifecycle_output/unity.stdout.log" 2>&1
lifecycle_unity_exit=$?
set -e
exit "$lifecycle_unity_exit"

