#!/usr/bin/env python3
"""Preserve one actual GitHub push-CI observation without retries or approvals."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path('/workspace/firstgame')
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/ClassicNprResume-2026-11-16'
WORKFLOWS = {'core': 'Deterministic core and saves', 'helper': 'Pages deployment completion helper'}


def run(argv):
    result = subprocess.run(argv, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=120)
    if result.returncode:
        raise ValueError(result.stderr.decode(errors='replace')[-3000:])
    return result.stdout


def write(path, data):
    with path.open('xb') as stream:
        stream.write(data)


def ref(path):
    b = path.read_bytes()
    return dict(file=str(path.relative_to(ROOT)), bytes=len(b), sha256=hashlib.sha256(b).hexdigest())


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--commit', required=True)
    p.add_argument('--label', required=True)
    args = p.parse_args()
    assert re.fullmatch('[0-9a-f]{40}', args.commit) and re.fullmatch('[A-Za-z0-9_-]{1,64}', args.label)
    output = CONTROL / 'Release' / args.label
    output.mkdir(exist_ok=False)
    raw = run(['gh', 'run', 'list', '--repo', 'ka8877/firstgame', '--commit', args.commit,
               '--event', 'push', '--limit', '20', '--json', 'databaseId,workflowName,headSha,event,status,conclusion,url,headBranch'])
    write(output / 'list.json', raw)
    values, docs, pending = json.loads(raw), [], []
    for role, workflow in WORKFLOWS.items():
        matches = [v for v in values if v['workflowName'] == workflow and v['event'] == 'push' and v['headSha'] == args.commit]
        assert len(matches) <= 1, 'Repeated workflow attempts require explicit separate review'
        if not matches:
            pending.append(dict(workflowKind=role, status='not-observed'))
            continue
        item = matches[0]
        view = run(['gh', 'run', 'view', str(item['databaseId']), '--repo', 'ka8877/firstgame', '--json',
                    'headSha,event,status,conclusion,url,workflowName,jobs'])
        path = output / (role + '.json')
        write(path, view)
        result = json.loads(view)
        assert result['headSha'] == args.commit and result['event'] == 'push' and result['workflowName'] == workflow
        if result['status'] != 'completed':
            pending.append(dict(workflowKind=role, runId=item['databaseId'], status=result['status']))
            continue
        assert result['conclusion'] == 'success', 'Completed workflow failed; preserve and investigate'
        assert result['jobs'] and all(job['status'] == 'completed' and job['conclusion'] == 'success'
            and job['steps'] and all(step['status'] == 'completed' and step['conclusion'] == 'success' for step in job['steps'])
            for job in result['jobs']), 'Every actual job/step must succeed'
        docs.append(dict(commit=args.commit, workflowKind=role, runId=item['databaseId'], receipt=ref(path)))
    result = dict(schema=1, status='actual-paired-push-CI-passed' if len(docs) == 2 else 'actual-push-CI-pending',
                  commit=args.commit, documentationCi=docs, pending=pending, rawList=ref(output / 'list.json'))
    write(output / 'result.json', (json.dumps(result, indent=2) + '\n').encode())
    print(json.dumps(result))


if __name__ == '__main__':
    main()
