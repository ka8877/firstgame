"""Current Native validation with the reviewed owned-descendant supervisor.

Each attempt retains actual source bindings and images. Historical approvals
are used only for the unchanged process supervisor, never for product success.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import ast
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time
import types
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT / 'Tools/Validation') not in sys.path:
    sys.path.insert(0, str(ROOT / 'Tools/Validation'))
from LivingRpgClassic import profile as RELEASE

CONTROL = Path(os.environ.get('FIRSTGAME_CONTROL_ROOT', str(RELEASE.CONTROL))).resolve()
assert CONTROL.is_relative_to(ROOT / 'Artifacts/Validation/Autonomy')
OUT = RELEASE.OUT
HELPER_SHA = '9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad'
# User explicitly authorized sustained work without the previous short cutoff.
EXECUTION_LIMIT_SECONDS = int(os.environ.get('FIRSTGAME_EXECUTION_LIMIT_SECONDS', '3600'))
assert 900 <= EXECUTION_LIMIT_SECONDS <= 21600


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('x') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


def restore_controls():
    index = json.loads((ROOT / 'Tools/Autonomy/Evidence/Recovery05ProcessControl/index.json').read_bytes())
    for row in index['files']:
        original, saved = row['original'], row['preserved']
        source, target = ROOT / saved['file'], ROOT / original['file']
        raw = source.read_bytes()
        assert len(raw) == saved['bytes'] and sha(raw) == saved['sha256']
        assert len(raw) == original['bytes'] and sha(raw) == original['sha256']
        assert target.is_relative_to(ROOT / 'Artifacts')
        if target.exists():
            assert target.read_bytes() == raw
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open('xb') as stream:
                stream.write(raw)


def gospel_names():
    tree = ast.parse((ROOT / 'Tools/Autonomy/recovery-gospel-native.py').read_bytes())
    names = next(ast.literal_eval(n.value) for n in tree.body if isinstance(n, ast.Assign)
        and any(isinstance(t, ast.Name) and t.id == 'EXACT_NAMES' for t in n.targets))
    return list(names)


class Lane:
    def __init__(self, run, lease):
        self.started = time.monotonic()
        self.safe_end = self.started + EXECUTION_LIMIT_SECONDS
        self.deadline = datetime.fromisoformat(lease['lease']['deadlineUtc'])
        self.run, self.events, self.logs = run, [], []
        self.env = dict(os.environ)

    def remaining(self):
        now = time.monotonic()
        self.safe_end = min(self.safe_end, now + (self.deadline - datetime.now(timezone.utc)).total_seconds() - 60)
        assert self.safe_end > now, 'Owned execution deadline reached'
        return self.safe_end - now


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('game', choices=['PathOfDawn', 'FantasyCards'])
    parser.add_argument('attempt')
    parser.add_argument('--prepare', action='store_true')
    args = parser.parse_args()
    npr_names = None
    if RELEASE.IS_GOSPEL_NPR:
        if args.game not in RELEASE.CURRENT_GAMES:
            raise ValueError('NPR preserves Card; a new Card Native run is outside this release scope')
        from GospelNpr.contract import native_full_names
        npr_names = native_full_names()
    restore_controls()
    lease_path = CONTROL / 'lease.json'
    lease = json.loads(lease_path.read_bytes())
    run = OUT / 'NativeRuns' / args.game / args.attempt
    run.mkdir(parents=True, exist_ok=False)
    helper = ROOT / 'Tools/Environment/owned-process-containment.py'
    raw = helper.read_bytes(); assert sha(raw) == HELPER_SHA
    module = types.ModuleType('canon_owned_containment'); module.__file__ = str(helper)
    sys.modules[module.__name__] = module
    exec(compile(raw, str(helper), 'exec', dont_inherit=True), module.__dict__)
    old = json.loads((ROOT / 'Tools/Autonomy/Drafts/Recovery06/GospelContainedSource/source-contract.json').read_bytes())
    controller = module.Controller({'helperPath': str(helper), 'helperSha256': HELPER_SHA,
        'evidenceRoot': str(CONTROL / 'ContainedNative' / args.game / args.attempt),
        'acceptance': old['processContainment']['acceptance']}, ROOT)
    lane = Lane(run, lease)
    lane.env.update(FIRSTGAME_VALIDATION_ROOT=str(run),
        FIRSTGAME_BUILD_ROOT=str(RELEASE.BUILD),
        FIRSTGAME_NATIVE_CAPTURE_ROOT=str(run / 'Screenshots'),
        FIRSTGAME_NATIVE_ATTEMPT=args.attempt, PYTHONDONTWRITEBYTECODE='1',
        PYTHONPYCACHEPREFIX=str(run / 'GeneratedPythonCache'))
    if RELEASE.REQUIRES_EXPLICIT_ENV:
        lane.env['FIRSTGAME_RELEASE_PROFILE'] = RELEASE.NAME
    if RELEASE.IS_GOSPEL_NPR:
        names = list(npr_names)
        expected = len(names)
    elif RELEASE.REQUIRES_EXPLICIT_ENV:
        from canon_release_evidence import REQUIRED_NATIVE_TESTS
        names = list(REQUIRED_NATIVE_TESTS[args.game])
        expected = len(names)
    elif args.game == 'PathOfDawn':
        names = gospel_names()
        # These two current suites add three book/conversation and two real
        # courtyard/composition checks without removing the retained sixteen.
        names += ['PathOfDawn.SmokeTests.GospelScriptureReadingSmokeTests',
                  'PathOfDawn.SmokeTests.GospelCourtyardCompositionSmokeTests']
        expected = 21
    else:
        native = ROOT / 'Docs/Autonomy/Evidence/C1-Native04-recovery/product-contract.executed.json'
        names = json.loads(native.read_bytes())['expectedFullNames']; expected = 8
    lane.env['FIRSTGAME_NATIVE_FILTER'] = ';'.join(names)
    shutil.copyfile(__file__, run / 'executed-driver.py')
    profile_copy = run / 'RecordedHarness/Tools/Validation/LivingRpgClassic/profile.py'
    profile_copy.parent.mkdir(parents=True, exist_ok=True)
    with profile_copy.open('xb') as stream:
        stream.write(RELEASE.SOURCE.read_bytes())
    extra_sources = []
    for source in RELEASE.EXTRA_SOURCE_FILES:
        raw = source.read_bytes()
        destination = run / 'RecordedHarness' / source.relative_to(ROOT)
        destination.parent.mkdir(parents=True, exist_ok=True)
        with destination.open('xb') as stream:
            stream.write(raw)
        extra_sources.append({'source': str(source.relative_to(ROOT)), 'recorded': str(destination.relative_to(ROOT)),
                              'sha256': sha(raw), 'bytes': len(raw)})
    write(run / 'invocation.json', {'scope': 'current-product-native-only', 'game': args.game,
        'attempt': args.attempt, 'filter': names, 'expectedCount': expected,
        'releaseProfile': RELEASE.NAME,
        **({'extraReleaseSources': extra_sources} if RELEASE.IS_GOSPEL_NPR else {}),
        'releaseConfiguration': {'source': str(RELEASE.SOURCE.relative_to(ROOT)),
            'recorded': str(profile_copy.relative_to(ROOT)), 'sha256': sha(profile_copy.read_bytes())},
        'lease': lease, 'supervisorSha256': HELPER_SHA, 'publishReady': False})
    guard = [sys.executable, str(ROOT / 'Tools/Autonomy/cycle_guard.py'), 'check', '--record', str(lease_path)]
    assert controller.child(lane, 'guard', guard, cap=45)['exitCode'] == 0
    if args.prepare:
        preparation = controller.child(lane, 'prepare', ['bash', 'Tools/Build/nativity-task.sh',
            args.game, 'prepare', args.attempt + 'Prepare'], cap=EXECUTION_LIMIT_SECONDS)
        if preparation['exitCode'] != 0:
            write(run / 'summary.json', {'status': 'prepare-failed', 'execution': preparation,
                'publishReady': False, 'scope': 'current-product-native-only'})
            return 1
    for pin in extra_sources:
        assert sha((ROOT / pin['source']).read_bytes()) == pin['sha256']
    result = controller.child(lane, 'native', [sys.executable, 'Tools/Validation/run-nativity-native.py',
        args.game, args.attempt], cap=EXECUTION_LIMIT_SECONDS)
    for pin in extra_sources:
        assert sha((ROOT / pin['source']).read_bytes()) == sha((ROOT / pin['recorded']).read_bytes()) == pin['sha256']
    summary = {'status': 'failed', 'game': args.game, 'attempt': args.attempt,
        'scope': 'current-product-native-only', 'publishReady': False,
        'execution': result, 'finishedUtc': datetime.now(timezone.utc).isoformat()}
    canonical = run / 'Native' / args.game
    if result['exitCode'] == 0 and (canonical / 'native.xml').exists():
        xml = ET.parse(canonical / 'native.xml').getroot()
        binding = json.loads((canonical / 'binding.json').read_bytes())
        assert xml.get('result') == 'Passed' and int(xml.get('passed')) == expected
        assert xml.get('failed') == xml.get('skipped') == '0'
        assert binding['status'] == 'passed' and not binding['sourceChangedDuringRun']
        summary.update(status='passed', binding=binding,
            originalPngCount=len(list((run / 'Screenshots').rglob('*.png'))))
    write(run / 'summary.json', summary)
    print(json.dumps(summary, ensure_ascii=False))
    return 0 if summary['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())

