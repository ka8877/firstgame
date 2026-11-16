#!/usr/bin/env python3
"""Safer review-branch-only raw documentation/evidence save. No main update or merge."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import uuid

ROOT = Path('/workspace/firstgame')
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/ClassicNprResume-2026-11-16'
HELPER = ROOT / 'Artifacts/Validation/Autonomy/ClassicNpr-2026-11-16/indexed-save-plan.py'
SUBJECT = ROOT / 'Artifacts/Validation/Drafts/ClassicNpr20261116Preservation/indexed-save-plan-proposal.py'
BRANCH = 'codex/classic-npr-proof-2026-11-16'
DOCS = ('README.md', 'AGENTS.md', 'Docs/Autonomy/STATE.json', 'Docs/Autonomy/REPORT.md',
        'Docs/Autonomy/ROADMAP.md', 'Docs/Research/ClassicLibraryUi20261116.md')


def need(value, message):
    if not value:
        raise ValueError(message)


def run(argv, timeout=180):
    result = subprocess.run(argv, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=timeout)
    need(result.returncode == 0, 'Command failed: ' + repr(argv) + '\n' + result.stderr.decode(errors='replace')[-5000:])
    return result.stdout


def new_bytes(path, data):
    with path.open('xb') as stream:
        stream.write(data)


def ref(path):
    b = path.read_bytes()
    return dict(file=str(path.relative_to(ROOT)), bytes=len(b), sha256=hashlib.sha256(b).hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--phase', choices=('checkpoint', 'closure'), required=True)
    parser.add_argument('--expected-head', required=True)
    args = parser.parse_args()
    prefix = args.phase + '-save01'
    branch = run(['git', 'branch', '--show-current']).decode().strip()
    need(branch == BRANCH or (args.phase == 'checkpoint' and branch == 'main'), 'Expected isolated review checkout required')
    need(run(['git', 'rev-parse', 'HEAD']).decode().strip() == args.expected_head, 'Expected main HEAD changed')
    need(not run(['git', 'diff', '--cached', '--name-only']), 'No existing staged changes may be included')
    destination = json.loads((CONTROL / 'CommandRuns/ReviewProofDestination01/command.stdout.log').read_bytes())
    need(destination['nameWithOwner'] == 'ka8877/firstgame' and destination['isPrivate'] is False and destination['viewerPermission'] == 'ADMIN', 'Actual public user-owned destination must be verified')
    screen = json.loads((CONTROL / 'Release/checkpoint-known-credential-screen01.json').read_bytes())
    need(screen['status'] == 'known-signature-screen-completed' and not screen['findings'] and screen['files'] == 10384, 'Full known-credential screen required')
    if branch == 'main':
        run(['git', 'switch', '-c', BRANCH])
        state_path = ROOT / 'Docs/Autonomy/STATE.json'
        state = json.loads(state_path.read_bytes())
        state['activeWork']['branch'] = BRANCH
        state['activeWork']['branchScope'] = 'Actual isolated documentation/evidence review checkout; main update was rejected, no merge performed.'
        state['mainDocumentationIntegration']['destination'] = BRANCH
        state['mainDocumentationIntegration']['mainUpdate'] = 'Not performed. Automatic approval rejected direct main evidence update; isolated review PR prepared.'
        state_path.write_text(json.dumps(state, ensure_ascii=False, indent=2) + '\n')
    need(hashlib.sha256(HELPER.read_bytes()).hexdigest() == 'bf2e4708742b51712132ba34eeb74d14f04d07db74daf7d26d5183e0c4641a6d'
         and HELPER.read_bytes() == SUBJECT.read_bytes(), 'Reviewed indexed save helper changed')
    identity = uuid.uuid4().hex
    plan = Path('/tmp/firstgame-classic-npr-' + prefix + '-' + identity + '.json')
    paths = Path('/tmp/firstgame-classic-npr-' + prefix + '-' + identity + '.nul')
    base = ['python3', str(HELPER), '--reviewed-source', str(SUBJECT)]
    argv = base + ['plan', '--phase', args.phase]
    for name in DOCS:
        argv += ['--include', name]
    argv += ['--output', str(plan)]
    plan_stdout = run(argv)
    value = json.loads(plan.read_bytes())
    new_bytes(CONTROL / ('Release/' + prefix + '-plan.json'), plan.read_bytes())
    path_bytes = run(base + ['paths', '--plan', str(plan)])
    need(path_bytes == b''.join(row['file'].encode() + b'\0' for row in value['files']), 'Literal NUL inventory differs')
    new_bytes(paths, path_bytes)
    run(['git', '--literal-pathspecs', 'add', '-f', '--pathspec-from-file=' + str(paths), '--pathspec-file-nul'], timeout=600)
    staged = run(['git', 'diff', '--cached', '--name-only', '-z']).split(b'\0')
    staged_set = {item.decode() for item in staged if item}
    expected_set = {row['file'] for row in value['files']}
    # Closure plans include already committed checkpoint files; they need not be changed.
    need(staged_set and staged_set <= expected_set, 'Staged changes escaped exact frozen save inventory')
    for row in value['files']:
        b = run(['git', 'cat-file', 'blob', ':' + row['file']], timeout=60)
        need(len(b) == row['bytes'] and hashlib.sha256(b).hexdigest() == row['sha256'], 'Staged raw bytes differ: ' + row['file'])
    title = ('docs: preserve ClassicNpr public release evidence and current status' if args.phase == 'checkpoint'
             else 'docs: preserve ClassicNpr normal closure and final release status')
    commit_stdout = run(['git', '-c', 'gc.auto=0', '-c', 'maintenance.auto=false', 'commit', '-m', title], timeout=300)
    commit = run(['git', 'rev-parse', 'HEAD']).decode().strip()
    need(run(['git', 'rev-parse', 'HEAD^']).decode().strip() == args.expected_head, 'Normal single-parent isolated review save required')
    verified_stdout = run(base + ['verify', '--plan', str(plan), '--commit', commit], timeout=600)
    verified = json.loads(verified_stdout)
    need(verified['fileCount'] == value['fileCount'] and verified['commit'] == commit, 'Actual Git blob readback incomplete')
    new_bytes(CONTROL / ('Release/' + prefix + '-git-blob-readback.json'), verified_stdout)
    new_bytes(CONTROL / ('Release/' + prefix + '-commit.stdout.log'), commit_stdout)
    before = dict(schema=1, status='actual-normal-local-commit-raw-blobs-verified-push-not-yet-run', phase=args.phase,
                  parent=args.expected_head, commit=commit, verifiedRawFiles=value['fileCount'],
                  plan=ref(CONTROL / ('Release/' + prefix + '-plan.json')),
                  blobReadback=ref(CONTROL / ('Release/' + prefix + '-git-blob-readback.json')))
    new_bytes(CONTROL / ('Release/' + prefix + '-local.json'), (json.dumps(before, indent=2) + '\n').encode())
    push_stdout = run(['git', 'push', 'origin', 'HEAD:refs/heads/' + BRANCH], timeout=600)
    new_bytes(CONTROL / ('Release/' + prefix + '-push.stdout.log'), push_stdout)
    result = dict(before, reviewBranch=BRANCH, mainUpdated=False, status='actual-isolated-review-branch-push-completed-raw-Git-blobs-verified-paired-CI-pending')
    new_bytes(CONTROL / ('Release/' + prefix + '-pushed.json'), (json.dumps(result, indent=2) + '\n').encode())
    print(json.dumps(result))


if __name__ == '__main__':
    main()
