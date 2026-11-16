#!/usr/bin/env python3
"""Apply only actual observed closing facts; preserve all prior document history."""
import argparse
from datetime import datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path('/workspace/firstgame')
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/ClassicNprResume-2026-11-16'
EVIDENCE = ROOT / 'Docs/Autonomy/Evidence/ClassicNpr20261116'
D1 = '42ebeb91f0a112a62796333944242c37c6e918e3'
BRANCH = 'codex/classic-npr-proof-2026-11-16'
PR = 'https://github.com/ka8877/firstgame/pull/11'
MARKDOWN = ('README.md', 'AGENTS.md', 'Docs/Autonomy/REPORT.md', 'Docs/Autonomy/ROADMAP.md', 'Docs/Research/ClassicLibraryUi20261116.md')


def ref(path):
    b = path.read_bytes()
    return dict(file=str(path.relative_to(ROOT)), bytes=len(b), sha256=hashlib.sha256(b).hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ci-result', required=True)
    args = parser.parse_args()
    def git(*argv):
        return subprocess.check_output(['git', *argv], cwd=ROOT, timeout=60).decode().strip()
    assert git('rev-parse', 'HEAD') == D1 and git('branch', '--show-current') == BRANCH
    assert not git('diff', '--name-only') and not git('diff', '--cached', '--name-only')
    checkpoint_path, closure_path = EVIDENCE / 'Checkpoint01/index.json', EVIDENCE / 'Closure01/index.json'
    checkpoint, closure = (json.loads(path.read_bytes()) for path in (checkpoint_path, closure_path))
    checkpoint_ref, closure_ref = ref(checkpoint_path), ref(closure_path)
    assert checkpoint_ref['sha256'] == '28decc424fda18af253013652ac07ca5632f5e7540a06bc689af95b460a86961'
    assert closure['actualPreconditions']['checkpoint'] == checkpoint_ref
    lease = json.loads((CONTROL / 'lease.json').read_bytes())
    assert lease['state'] == 'released' and lease['lease']['owner'] == '555529fe-af82-4e24-85a4-e7627ae70ccb'
    assert json.loads((CONTROL / 'Release/status-resume01.stdout.json').read_bytes()) == {
        'status': 'available', 'repository': 'ka8877/firstgame', 'ref': 'refs/heads/codex/autonomy-lock'}
    ci_path = Path(args.ci_result)
    ci = json.loads(ci_path.read_bytes())
    assert ci['status'] == 'actual-paired-push-CI-passed' and ci['commit'] == D1
    assert {row['workflowKind'] for row in ci['documentationCi']} == {'core', 'helper'}
    assert ci['documentationCi'] == closure['actualPreconditions']['documentationCi']
    saved = json.loads((CONTROL / 'Release/checkpoint-save01-pushed.json').read_bytes())
    assert saved['commit'] == D1 and saved['verifiedRawFiles'] == 26 and saved['mainUpdated'] is False
    released_seoul = datetime.fromisoformat(lease['releasedUtc']).astimezone(timezone(timedelta(hours=9))).isoformat()
    run_ids = {row['workflowKind']: row['runId'] for row in ci['documentationCi']}
    prefix = ('## ClassicNpr 새 공개·검증·원본 보존·정상 종료 — 2026-11-16\n\n'
        '판마 덱·도감과 예수님의 길 인물·대화 개선본을 공개했습니다. 제품 main `1af1205e…`/공개 `9e7c049b…`이며 '
        '[판마 실행](https://ka8877.github.io/firstgame/games/fantasy-cards/?v=9e7c049b)·'
        '[예수님의 길 실행](https://ka8877.github.io/firstgame/games/path-of-dawn/?v=9e7c049b)에서 테스트할 수 있습니다. '
        'PC·모바일·공개6회와 최종 봉인, 전체15,439공개파일 전달을 검증했습니다.\n\n'
        f'Checkpoint01의10,384원본/18조각/565,882,862B와 문서를 포함한26 raw Git blob을 실제 검증하고 별도 검토 브랜치에 정상 저장했습니다. '
        f'D1 `{D1}`, push Core{run_ids["core"]}/helper{run_ids["helper"]}의 모든 실제 작업·단계가 성공했습니다. '
        f'독립 최종 유휴와 소유 후손 정리 뒤 `{released_seoul}` KST에 자기 소유권을 정상 해제했고 available을 관측했습니다. '
        f'Closure01은 실제 후기 원본{closure["fileCount"]}개, index{closure_ref["bytes"]}B/SHA256 `{closure_ref["sha256"]}`로 완료했습니다. '
        '현재 소유 실행이나24시간 백그라운드 개발을 주장하지 않습니다.\n\n'
        f'현재 문서·검증자료의 검토 대상은 [PR #11]({PR})입니다. 약566MB 원본 보존을 포함한 main 직접 반영은 자동 승인 검토가 거절해 수행하지 않았습니다. '
        '명시적인 이 PR 반영 승인 전에는 main에 합치지 않습니다. 이번 종료 문서·Closure를 처음 저장할 후속 D2 커밋/CI는 '
        '작성 뒤 실제 관측하며 이미 동결한 Closure에 소급 넣지 않습니다.\n\n'
        '판마의 실제3덱 슬롯·정확한 후기 도감·작은 control과 EP/시장P 근거, 예수님 인물의 옷/얼굴/표정·단순 환경·숨은 성경 스크롤 조작은 다음 개선 대상입니다. '
        '원작 전체 재현·원신급 아트·사용자 미적 승인은 아직 완료하지 않았습니다.\n\n'
        '아래는 이 회차의 진행 중 작성 기록입니다. 그 안의 현재/소유자/대기 표기는 각 작성 시점의 이력이며 위 정상 종료와 실제 새 사용자 지시를 우선합니다.\n\n---\n\n')
    for name in MARKDOWN:
        path = ROOT / name
        path.write_text(prefix + path.read_text())
    path = ROOT / 'Docs/Autonomy/STATE.json'
    s = json.loads(path.read_bytes())
    s['updatedUtc'] = datetime.now(timezone.utc).isoformat()
    work = s['activeWork']
    work.update(status='technical-public-release-proof-checkpoint-D1-push-CI-normal-release-Closure-complete-review-PR-main-approval-pending',
        executionState='normally-released-no-current-owned-runtime', leaseState='released', releasedUtc=lease['releasedUtc'],
        releasedSeoul=released_seoul, branch=BRANCH,
        branchScope='Actual isolated documentation/evidence review branch; main merge has not been performed.')
    scope = 'Actual publication/all six suites/FinalSeal/Checkpoint/raw D1 Git blobs/paired push CI/separate FinalIdle/normal owner release/available/Closure completed. Review PR11 main integration and the later D2 own save/CI are separate pending boundaries. No aesthetic/AAA/original parity or background automation completion claim.'
    work['sourceProgress']['Validation'] = scope
    work['validation']['publicStage']['finalPreservation'] = scope
    work['validation']['qualityScope'] = scope
    durable = work['validation']['durablePreservation']
    durable.update(status='Checkpoint-D1-raw-Git-blobs-push-CI-and-actual-Closure-complete-D2-save-pending',
        checkpoint=checkpoint_ref, closure=closure_ref, closureFileCount=closure['fileCount'],
        documentationSaveD1=saved, documentationCi=ci['documentationCi'], finalRelease=ref(CONTROL / 'lease.json'), scope=scope)
    work['publication'].update(status='public-product-and-delivery-gameplay-seal-technical-closing-complete', scope=scope)
    work['publication']['stageUtilityFix']['scope'] = 'Bounded missing-license-parent stager fix normally merged as PR10/M2; product/proof identities unchanged; actual Checkpoint/D1/CI/normal closing now completed.'
    work['next'] = ['Review PR11 exact documentation/evidence payload before any main integration; automatic direct-main update was rejected.',
        'Observe later D2 own save/raw Git blobs/paired push CI separately; never retrofit that future result into frozen Closure.',
        'For next actual runtime work, recheck environment/remote/processes and obtain a new independent bounded ownership. Retain future real deck slots/original references and Gospel cloth/face/expression/environment/Bible-scroll UX candidates.']
    closing = dict(checkpoint=checkpoint_ref, closure=closure_ref, finalIdle=closure['actualPreconditions']['wrappers']['finalIdleWrapper'],
        releasedLease=ref(CONTROL / 'lease.json'), releasedUtc=lease['releasedUtc'], releasedSeoul=released_seoul,
        statusObservation=ref(CONTROL / 'Release/status-resume01.stdout.json'), currentOwnership=False,
        documentationCi=ci['documentationCi'], scope=scope)
    work['finalReleaseEvidence'] = closing
    s['execution']['lockStatus'] = 'normally-released-available-observed-no-current-owned-runtime'
    s['execution']['currentBatchAtReport'].update(status=work['status'], currentOwnership=False,
        releasedUtc=lease['releasedUtc'], closure=closure_ref)
    s['execution']['lastCompletedBatch'] = dict(id=work['currentRunId'], **closing)
    s['lastCompletedWorkBeforeClassicNpr'] = s['lastCompletedWork']
    s['lastCompletedWork'] = dict(id=work['id'], status=work['status'], **closing)
    s['nextRun'].update(status='future-runtime-requires-fresh-independent-checks-and-ownership', tasks=work['next'])
    s['baselineScope'] = scope
    s['environment']['currentConnectionStatus'] = scope
    s['environment']['currentRecovery'] = 'Current product and proof retained; prior storage/cache failures preserved. Execution normally closed. Review PR11 and next runtime require their own observed steps.'
    s['capacity']['previousScopeAtD1'] = s['capacity']['scope']
    s['capacity']['scope'] = 'Historical observed storage facts retained. All original Bee/cache/source/raw proof bytes kept; only strict same-byte derived copies shared. Checkpoint/D1 Git objects now saved; closing bookkeeping continues on review branch.'
    s['knownIssuesScope'] = 'Current remaining product quality gaps and review PR/main integration; local Checkpoint, D1 CI and normal runtime closure completed.'
    s['knownIssues'] = [scope if 'Checkpoint/DocsCI/closing not yet passed' in str(item) else item for item in s['knownIssues']]
    s['currentDeployment'].update(status='public-delivery-six-suites-final-seal-and-technical-closure-complete',
        closureState='actual-checkpoint-D1-raw-Git-push-CI-final-idle-normal-release-Closure-complete',
        qualityScope=scope, closingEvidence=closing)
    automation_path = CONTROL / 'Release/automation-peek-observation01.json'
    automation = json.loads(automation_path.read_bytes())
    assert automation['actualResponse']['automations'] == []
    s['automation']['currentReadOnlyObservation'] = dict(receipt=ref(automation_path), scheduledAutomations=0,
        currentGoalActivation='unverified-no-read-tool-available',
        scope='Actual automations.peek returned zero. Existing Codex goal-card activation is separate and unverified; no goal or schedule was changed.')
    s['mainDocumentationIntegration'].update(status='D1-review-branch-raw-save-and-paired-CI-complete-Closure-observed-D2-save-pending',
        scope=scope, reviewPr=PR, destination=BRANCH, mainUpdate='Not performed; explicit approval for concrete PR11 required.',
        actualCommittedRawBlobs=26, latestCompletedD1=saved, documentationCi=ci['documentationCi'], closure=closure_ref)
    path.write_text(json.dumps(s, ensure_ascii=False, indent=2) + '\n')
    receipt = dict(status='actual-closing-facts-applied-to-six-tracked-documents', checkpoint=checkpoint_ref,
        closure=closure_ref, documentationCi=ci['documentationCi'],
        files=[ref(ROOT / name) for name in (*MARKDOWN, 'Docs/Autonomy/STATE.json')])
    with (CONTROL / 'Release/docs-closure-application01.json').open('x') as f:
        json.dump(receipt, f, ensure_ascii=False, indent=2); f.write('\n')
    print(json.dumps(receipt, ensure_ascii=False))


if __name__ == '__main__':
    main()
