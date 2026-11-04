# 2026-11-04 실제 작업 재개 근거

새 Core285·Native16과47PNG/sidecar 전체, 첫 중단·실패, 원본 복구·소유 잠금·제한 Root 검토의 실제 원문을 보존했다. 생성 Python 캐시는 실행 증거가 아니므로 제외했다. 전체 C1/Web/공개 승인은 아니다.

복원 시 archive SHA를 먼저 확인하고 모든 member의 경로·크기·SHA를 index와 대조한다. 대상이 없을 때만 저장소 루트 아래의 해당 상대 경로로 원문을 복원한다. 존재하는 원문은 덮어쓰지 않는다.

Web 원문은 web-index.json의 parts 순서로 결합하고 joinedArchiveSha256를 확인한 뒤 각 member를 검증한다. 실제 compiled runtime5개도 포함한다. 두 archive는 각각209개·140개의 고정 원문 스냅샷이며, 새 메타데이터 파일이 추가되더라도 과거 스냅샷의 파일 수를 현재 전체 폴더 수로 해석하지 않는다.
