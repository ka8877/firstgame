#!/usr/bin/env python3
"""Build fresh post-release facts exclusively from completed originals."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path('/workspace/firstgame')
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/ClassicNprResume-2026-11-16'


def ref(path):
    b = path.read_bytes()
    return dict(file=str(path.relative_to(ROOT)), bytes=len(b), sha256=hashlib.sha256(b).hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ci-result', required=True)
    args = parser.parse_args()
    original_facts = json.loads((CONTROL / 'Release/checkpoint-facts01.json').read_bytes())
    lease = json.loads((CONTROL / 'lease.json').read_bytes())
    assert lease['state'] == 'released' and lease['lease']['owner'] == '555529fe-af82-4e24-85a4-e7627ae70ccb'
    assert json.loads((CONTROL / 'Release/release-resume01.stdout.json').read_bytes()) == lease
    assert json.loads((CONTROL / 'Release/status-resume01.stdout.json').read_bytes()) == {
        'status': 'available', 'repository': 'ka8877/firstgame', 'ref': 'refs/heads/codex/autonomy-lock'}
    ci_path = Path(args.ci_result)
    ci = json.loads(ci_path.read_bytes())
    assert ci['status'] == 'actual-paired-push-CI-passed' and ci['commit'] == '42ebeb91f0a112a62796333944242c37c6e918e3'
    assert {row['workflowKind'] for row in ci['documentationCi']} == {'core', 'helper'}
    wrappers = {}
    for role, label in (('archiveWrapper', 'Checkpoint01'), ('finalIdleWrapper', 'FinalIdleResume01')):
        path = CONTROL / 'CommandRuns' / label / 'summary.json'
        value = json.loads(path.read_bytes())
        assert value['label'] == label and all(value[key]['event']['exitCode'] == 0
            and value[key]['event']['containmentProvedEmpty'] for key in ('guardBefore', 'idleBefore', 'command', 'guardAfter'))
        wrappers[role] = ref(path)
    facts = dict(schema=1, releaseProfile='classic-npr', leaseChain=original_facts['leaseChain'],
        checkpoint=ref(ROOT / 'Docs/Autonomy/Evidence/ClassicNpr20261116/Checkpoint01/index.json'),
        releasedLease=ref(CONTROL / 'lease.json'), releaseStdout=ref(CONTROL / 'Release/release-resume01.stdout.json'),
        statusStdout=ref(CONTROL / 'Release/status-resume01.stdout.json'), **wrappers,
        documentationCi=ci['documentationCi'], lateOriginals=[])
    path = CONTROL / 'Release/closure-facts01.json'
    with path.open('x') as stream:
        json.dump(facts, stream, ensure_ascii=False, indent=2); stream.write('\n')
    print(json.dumps(dict(status='actual-post-release-facts-prepared-not-Closure-execution', facts=ref(path))))


if __name__ == '__main__':
    main()
