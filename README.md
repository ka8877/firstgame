# FirstGame · 첫 플레이 구간

두 독립 Unity 프로젝트의 개발 저장소입니다.

| 게임 | 프로젝트 | 현재 범위 |
|---|---|---|
| **별빛의 서약** | `Games/FantasyCards` | 16종 카드, 24장 덱, 지형·유닛·공방·코인·AI, 저장/재도전 |
| **새벽의 길** | `Games/PathOfDawn` | 갈릴리 공간, 3인칭 이동·조사·대화, 선택 두 번, 결과 세 가지, 기억과 회귀 |

Unity **6000.3.26f1 LTS (de8167e7440c)**를 사용합니다. 두 폴더를 각각 Unity Hub에서 여세요. 첫 import가 끝나면 `FirstGame > Prepare First Slice`로 씬·URP·한국어 글꼴·제작 데이터·프리팹을 준비하고 `Assets/Scenes/Boot.unity`에서 Play합니다. 현재 실행 검증 여부와 제한은 반드시 [검증 기록](Docs/Validation.md)을 확인하세요.

- [개발·실행·빌드](Docs/Development.md)
- [첫 플레이 안내와 조작법](Docs/PlaytestGuide.md)
- [환경 및 공식 도구 확인](Docs/Environment.md)
- [카드 규칙: 원작 근거와 v0.1 설계](Docs/FantasyCardsRules.md)
- [서사·상태 소유권·회귀](Docs/PathOfDawnDesign.md)
- [자동 테스트 결과](Docs/CoreValidation.md)
- [아트 방향](Docs/ArtDirection.md), [에셋 출처](Docs/AssetRegister.md)
- [원래 개발 요청](Docs/Brief.md)

현재 3D 캐릭터와 공간은 Blender로 만든 임시 프록시이며, 카드 원화는 승인 전 생성 이미지 후보입니다. 실제 측정 전에는 목표 플레이 시간·60fps·최종 비주얼을 달성한 것으로 간주하지 않습니다. Git LFS를 설치하고 클론해야 모델·그림·글꼴을 받을 수 있습니다.
