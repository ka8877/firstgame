# C1 grade-guide browser additions — 미실행 제안

이 디렉터리에만 작성했다. 저장소·제품·Tools·Docs·Git·Unity·Native·브라우저·네트워크·실제 source import를 실행하거나 변경하지 않았다. 아래 수치와 원장은 JSON/소스의 정적 비교이며 실제 입력·가독성·배포 통과가 아니다.

## 최소 정상 입력 경로

기존 두 C1 계획의 `capability-catalog-entry` 직후에만5단계/필수2원본을 넣었다. 실제 출전 카드 도감에는 이미 `등급 / 랭크 안내`가 있어 게임방법/대기실을 왕복할 필요가 없다.

1. 도감의 안내 버튼 click `(.6185,.374)`.
2. `capability-grade-guide-all-thirteen` 현재 원본: 기존13분류의 명칭·원작코드·수록수, 마지막 미분류 항목과 상단설명, 두 footer의 가독성/분리를 확인.
3. `review_input`에서 현재 화면을 보며 일반 입력으로만 확인. 해당 acknowledgement는 Root 최종 시각 승인이 아니다.
4. footer 돌아가기 click `(.7475,.9435)`.
5. `capability-grade-guide-return-catalog` 현재 원본: 검색 비움/전체2646·능력전체/같은카드 도감 복귀 확인 후 원래 source-ID 검색 단계 재개.

`FantasyCardsCollection.cs:75`의 안내 Unity rect는x.509~.728/y.598~.654이고, `FmFidelityUi.cs:203`의 돌아가기 rect는x.55~.945/y.018~.095이다. 상단원점 정규화 변환 `[centerX,1-centerY]`로 좌표를 계산했다. `ShowCardGradeGuide(edit)`에 custom back이 없으므로 footer는 같은 `ShowCollection(edit)`로 돌아간다. 두 프로필 모두 같은 canvas 내부 정규화 좌표다. 실제 hit-test는 수행하지 않았으며 owner의 현재 screenshot-guided 정상입력 확인이 필요하다.

## 보존 결과

- PC: 기존157steps/46required → 제안162/48.
- Touch: 기존158steps/45required → 제안163/47. 현재 mobile-landscape 계획을 실제 공개터치에도 사용하는 경우 이 같은 명시계획을 사용하고, 별도 미래 배포 정체성을 꾸미지 않는다.
- 추가5steps/required2를 제거하면 기존 C1 객체 전체와 **원문 바이트까지** 정확히 복원된다. 기존 scope/모든step값·순서/required prefix는 바꾸지 않았다.
- 기존25필수원본과 그 위에 추가된 capability21/20원본을 모두 유지했다.
- 음악PCM4개 전체 객체(`nativity-title`, `starter-lobby`, `continued-sealed-lobby`, `continued-battle`)와 실제reload2개·경제/전투/저장 흐름을 그대로 유지했다. 새로운 audio/reload/재화입력은 추가하지 않았다.

## 산출물과 소유자 적용 경계

- `base/Tools/Autonomy/`: 실제 기존 두 계획의 원문 사본.
- `proposed/Tools/Autonomy/`: 제안 후계 계획. 제품/실제 실행계획에 설치하지 않았다.
- `plan-additions.json`: 추가5steps/required2.
- `comparison-ledger.json`: base/proposed SHA·bytes·counts와 exact byte 역복원, 좌표근거·관측 source SHA·미검증범위.
- `observed-source/`: 읽은 실제 UI/adapter/contract 사본. owner의 이후 UI 보정과는 별개 관측 시점이다.
- `contract-plan-updates.json`: 실제 `Tools/Autonomy/c1-contract.json`의 `plans.desktop`/`plans.mobile-landscape`에 반영할 SHA·steps·required만을 제안한다. 기존 나머지 source/Native/G2/PCM/hash/public 필드를 이 임시 사본으로 덮지 않는다.

현재 `c1_web_smoke.py`는 계획 SHA/count/required를 계약에서 읽고 모든 required checkpoint 수집을 요구한다. 따라서 이 추가를 위해 wrapper 판정 코드를 바꿀 필요는 찾지 못했다. owner가 실제계획을 검토·채택할 때 두 계약 entry만 현재source기준으로 갱신하고, final closure/Root시각승인에는 **새2개원본을 모두 필수**로 요구해야 한다. 실행한 정확한 계획·계약·harness 사본/SHA를 보존한다.

`checkpoint.expected`는 자동 텍스트/겹침 검사가 아니다. Root가 PC/터치 각각 새 실제 원본을 직접 열고13항목과 마지막 미분류의 이름/수록수 및footer가 읽히는지 확인해야 한다. 현재13개 분류는 이미 있는 current enum이며, 원작에13등급이 있었다는 새 주장이나 지원카드 추가가 아니다. Native01의8pass와 잘린등급 원본/시각반려는 그대로 남긴다. 새source Native02/Web/오류/음악/저장/실제공개·전체SHA/985MB/Root closure는 release owner의 별도 실행 범위다.
