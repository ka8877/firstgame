#!/usr/bin/env python3
"""Apply frozen reviewed documentation copies and only observed Checkpoint facts."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path('/workspace/firstgame')
DRAFT = ROOT / 'Artifacts/Validation/Drafts/ClassicNpr20261116Execution/DocsAfterPc'
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/ClassicNprResume-2026-11-16'
INDEX = ROOT / 'Docs/Autonomy/Evidence/ClassicNpr20261116/Checkpoint01/index.json'
M2 = '1af1205ea447c4bb627d436e6b17b14177f91c51'
P = '9e7c049b79d7991bc60e0d4bf06dfe4ec4333b70'
TARGETS = {
    'README.md': ('README.md', '8dd1767400317eefdd79ac7e53362d85c67ead09201a7609d54ec17640dc07d4'),
    'AGENTS.md': ('DraftAGENTS.md', '1352d1cfa128b66b10bf89a6d3fe5e9cb828c098344e56cda7369b2172ff89b0'),
    'Docs/Autonomy/STATE.json': ('Docs/Autonomy/STATE.json', '76ca27ca5b3deb0849c414c1ccc265c8ccba0024ebad96c8b8246193118bdd7f'),
    'Docs/Autonomy/REPORT.md': ('Docs/Autonomy/REPORT.md', 'c42a12527c71f76dd2cfc26ba76684079326fedff62b9383c68b44c36feca9a0'),
    'Docs/Autonomy/ROADMAP.md': ('Docs/Autonomy/ROADMAP.md', '5ed45724971f17fb388bc933057d0bafc731df4078eeac1865a4c70d901e61eb'),
    'Docs/Research/ClassicLibraryUi20261116.md': ('Docs/Research/ClassicLibraryUi20261116.md', '9fd0b0748da27aacb5d08219b20903f9b1b98e563c2cc739b11792db8a7ca495'),
}


def need(value, message):
    if not value:
        raise ValueError(message)


def ref(path):
    b = path.read_bytes()
    return dict(file=str(path.relative_to(ROOT)), bytes=len(b), sha256=hashlib.sha256(b).hexdigest())


def main():
    def git(*args):
        return subprocess.check_output(['git', *args], cwd=ROOT, timeout=30).decode().strip()
    need(git('rev-parse', 'HEAD') == M2 and git('branch', '--show-current') == 'main', 'Exact clean main checkout required')
    need(not git('diff', '--name-only') and not git('diff', '--cached', '--name-only'), 'No tracked edits may be overwritten')
    index_ref = ref(INDEX)
    need(index_ref['sha256'] == '28decc424fda18af253013652ac07ca5632f5e7540a06bc689af95b460a86961', 'Completed Checkpoint identity required')
    index = json.loads(INDEX.read_bytes())
    wrapper_path = CONTROL / 'CommandRuns/Checkpoint01/summary.json'
    wrapper = json.loads(wrapper_path.read_bytes())
    need(all(wrapper[k]['event']['exitCode'] == 0 and wrapper[k]['event']['containmentProvedEmpty'] for k in ('guardBefore', 'idleBefore', 'command', 'guardAfter')), 'Completed archive cleanup required')
    original_agents = (ROOT / 'AGENTS.md').read_text()
    updates = {}
    for destination, (source, sha) in TARGETS.items():
        path = DRAFT / source
        need(ref(path)['sha256'] == sha, 'Frozen reviewed Draft changed: ' + source)
        updates[destination] = path.read_text()
    need(updates['AGENTS.md'].endswith(original_agents), 'Entire original AGENTS history must remain exact')
    agents = updates['AGENTS.md']
    first, rest = agents.split('\n\n', 1)
    _, rest = rest.split('\n\n', 1)
    updates['AGENTS.md'] = '## 현재 ClassicNpr 새 공개·검증·Checkpoint 상태 — 2026-11-16\n\n' + (
        '아래 최신 기록은 Root가 실제 공개판·최종 검증·Checkpoint 원문과 대조해 적용했다. '
        '원래 AGENTS 본문 전체는 아래에 보존하며 과거 ‘현재’·owner·마감·대기는 당시 이력이다. '
        '최신 사용자 요청과 실제 소유권을 우선하고 기존 원본 보존·검증 규칙을 유지한다.\n\n') + rest
    replacements = {
        '새 currentDeployment는 배포/포털 완료와 아직 남은 실제 공개 gameplay/봉인/종료를 구분합니다.': '새 currentDeployment는 배포·전체 공개 전달·6개 gameplay·최종 봉인 완료와 남은 문서 저장/종료를 구분합니다.',
        '이 Draft는 아직 tracked Docs에 적용하지 않았으며 최종 공개 proof 뒤 갱신합니다.': 'Root가 실제 공개 proof와 완료된 Checkpoint를 대조한 뒤 이 tracked 문서에 적용했습니다.',
        '공개 gameplay는 별도 필요하며 목도리': '공개110/필수56Root+실제 성경 스크롤1도 완료했으며 목도리',
        'Checkpoint·문서 저장 CI·최종 유휴/자기해제·Closure는 아직 완료하지 않았습니다.': 'Checkpoint 원본 보존을 완료했고, 이번 문서/Checkpoint의 Git 저장·CI와 최종 유휴/자기해제·Closure는 작성 시점에 대기 중입니다.',
        '실제 Checkpoint/archive·문서 저장CI·독립 최종 idle/정상 자기해제·Closure는 아직 완료하지 않았다. 이 Draft는 archive 전 동결하며 Root는 이후 실제 보존/종료 사실을 tracked 문서에만 적용하고 동결 Draft를 수정하지 않는다.': '실제 Checkpoint 원본 보존을 완료했다. 문서 저장CI·독립 최종 idle/정상 자기해제·Closure는 작성 시점에 대기 중이다. 동결 Draft 원문은 바꾸지 않고 후속 실제 사실은 tracked 문서에만 반영한다.',
        '이 초안은 archive 전에 동결하고 Root가 이후 tracked 문서에만 실제 보존/종료 결과를 반영합니다.': 'archive 전 동결 Draft는 보존하며 Root가 이 tracked 문서에 실제 보존/종료 결과를 반영합니다.',
        '실제 Checkpoint·문서CI·정상 최종 해제/Closure는 아직이며 이 Draft는 archive 전 동결하고 이후 수정하지 않습니다.': '실제 Checkpoint 원본 보존은 완료했고 문서CI·정상 최종 해제/Closure는 작성 시점에 대기 중입니다. 동결 Draft를 유지하며 실제 후속 사실은 이 tracked 문서에 반영합니다.',
        '완료했지만 Checkpoint·최종 Resume 해제·Closure·그 미래 저장/CI 결과는 아직 없습니다.': '완료했고 Checkpoint 원본 보존도 마쳤지만 최종 Resume 해제·Closure·그 미래 저장/CI 결과는 작성 시점에 아직 없습니다.',
    }
    for name in updates:
        for a, b in replacements.items():
            # Preserved AGENTS historical body is left byte-exact.
            if name == 'AGENTS.md':
                prefix = updates[name][:-len(original_agents)]
                updates[name] = prefix.replace(a, b) + original_agents
            else:
                updates[name] = updates[name].replace(a, b)
    paragraph = ('Root가 실제 Checkpoint01의10,384원본·18조각·결합565,882,862B를 독립 재독해 보존했습니다. '
                 'index4,604,790B/SHA256 `28decc424fda18af253013652ac07ca5632f5e7540a06bc689af95b460a86961`, '
                 '결합 SHA256 `f5adb3e95b8c25a679e423129be7924e68f6cf6cde9adf4bb97734801e3d8178`입니다. '
                 '완료 wrapper의4개 event exit0·빈 후손을 확인했습니다. 이 시점은 로컬 원본 보존 완료이며 '
                 '이번 문서·모든 조각/index/raw 속성의 실제 Git blob 저장·정상 push/paired CI는 후속 확인합니다. '
                 '최종 독립 유휴·정상 자기해제·Closure도 아직 미래 결과입니다.\n\n')
    for name in updates:
        if name.endswith('.md'):
            first, rest = updates[name].split('\n\n', 1)
            updates[name] = first + '\n\n' + paragraph + rest
    s = json.loads(updates['Docs/Autonomy/STATE.json'])
    s['updatedUtc'] = datetime.now(timezone.utc).isoformat()
    work = s['activeWork']
    work['status'] = 'M2-P9e7-all-six-suites-FinalSeal01-Checkpoint01-completed-Git-save-CI-closing-pending'
    work['branch'] = 'main'
    work['branchScope'] = 'Actual Root checkout main at M2 when applying reviewed documentation; C3 source branch is historical integration.'
    durable = work['validation']['durablePreservation']
    durable.update(status='actual-local-Checkpoint01-completed-raw-Git-save-and-CI-pending', checkpoint=index_ref,
                   fileCount=index['fileCount'], partCount=len(index['parts']), joinedBytes=index['joinedBytes'],
                   joinedArchiveSha256=index['joinedArchiveSha256'], archiveWrapper=ref(wrapper_path),
                   scope='Actual local archive/readback completed. Frozen Drafts and originals unchanged. Raw Git blobs/push/paired CI and independent idle/release/Closure are subsequent boundaries.')
    work['next'] = ['Save every indexed part/index/raw attribute and these six documents using the reviewed literal NUL inventory; verify actual Git blobs and normal push paired Core/helper CI.',
                    'Complete separate final idle, normal current-owner release/status, standalone immutable Closure; save all indexed nested late originals and observe its future CI separately.',
                    'Retain remaining original UI/deck-slot/reference and Gospel cloth/face/expression/environment/Bible-scroll-affordance gaps as future product work, without claiming user aesthetic approval or continuous automation.']
    batch = s['execution']['currentBatchAtReport']
    batch.update(status=work['status'], checkpoint=index_ref)
    s['nextRun'].update(status=work['status'], tasks=work['next'])
    previous = s['mainDocumentationIntegration']
    s['mainDocumentationIntegration'] = dict(status='tracked-reviewed-documentation-applied-local-Checkpoint-complete-save-CI-pending',
        scope='Current product/public/gameplay/seal/Checkpoint facts are observed; this first documentation commit and paired CI are not invented.',
        baseMainCommit=M2, currentlyPublishedProductCommit=M2, currentlyPublishedCommit=P,
        checkpoint=index_ref, actualCommittedRawBlobs=None, documentationCi=[], priorIntegrationRecord=previous)
    updates['Docs/Autonomy/STATE.json'] = json.dumps(s, ensure_ascii=False, indent=2) + '\n'
    need(updates['AGENTS.md'].endswith(original_agents), 'Preserved AGENTS history changed')
    for name, text in updates.items():
        (ROOT / name).write_text(text)
    receipt = dict(schema=1, status='reviewed-docs-applied-with-actual-checkpoint-only', checkpoint=index_ref,
                   archiveWrapper=ref(wrapper_path), files=[ref(ROOT / name) for name in updates],
                   scope='Only six tracked documents updated. Frozen archived Drafts unchanged; no future Git/CI/release approval supplied.')
    with (CONTROL / 'Release/docs-checkpoint-application01.json').open('x') as f:
        json.dump(receipt, f, ensure_ascii=False, indent=2); f.write('\n')
    print(json.dumps(receipt, ensure_ascii=False))


if __name__ == '__main__':
    main()
