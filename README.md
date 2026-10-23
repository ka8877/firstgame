# FirstGame · 첫 플레이 구간

두 독립 Unity 프로젝트의 개발 저장소입니다.

**[설치 없이 두 게임 플레이하기](https://ka8877.github.io/firstgame/)** · PC 및 모바일 가로 화면. 업데이트 후에도 같은 주소를 사용합니다.

현재 소스는 플레이 피드백을 반영한 **애니메이션·서브컬처풍 아트와 BGM 개정판**입니다. 이 개정판의 새 빌드·브라우저 검증·공개 배포는 진행 중이며, 위 주소의 직전 공개판 통과 결과와 구분합니다. 최종 배포 상태는 [검증 기록](Docs/Validation.md)을 확인하세요.

| 게임 | 프로젝트 | 현재 범위 |
|---|---|---|
| **별빛의 서약** | `Games/FantasyCards` | 16종 카드, 24장 덱, 지형·유닛·공방·코인·AI, 저장/재도전 |
| **새벽의 길** | `Games/PathOfDawn` | 갈릴리 공간, 3인칭 이동·조사·대화, 선택 두 번, 결과 세 가지, 기억과 회귀 |

Unity **6000.3.26f1 LTS (de8167e7440c)**를 사용합니다. 두 폴더를 각각 Unity Hub에서 여세요. 첫 import가 끝나면 `FirstGame > Prepare First Slice`로 씬·URP·한국어 글꼴·제작 데이터·프리팹을 준비하고 `Assets/Scenes/Boot.unity`에서 Play합니다. 현재 실행 검증 여부와 제한은 반드시 [검증 기록](Docs/Validation.md)을 확인하세요.

- [개발·실행·빌드](Docs/Development.md)
- [첫 플레이 안내와 조작법](Docs/PlaytestGuide.md)
- [웹 체험판 배포와 업데이트](Docs/WebPlaytest.md)
- [환경 및 공식 도구 확인](Docs/Environment.md)
- [카드 규칙: 원작 근거와 v0.1 설계](Docs/FantasyCardsRules.md)
- [서사·상태 소유권·회귀](Docs/PathOfDawnDesign.md)
- [자동 테스트 결과](Docs/CoreValidation.md)
- [아트 방향](Docs/ArtDirection.md), [에셋 출처](Docs/AssetRegister.md)
- [새 애니메이션 원화](Docs/Illustration.md), [오리지널 BGM과 사용 권리](Docs/Audio.md)
- [원래 개발 요청](Docs/Brief.md)

이번 개정은 카드 원화 16종·타이틀 2종·대화 초상 3종을 현대 애니메이션풍으로 교체하고, 꽃과 구름 궁전의 전장 배경·둥근 카드와 버튼을 더합니다. 새벽의 길은 부드러운 캐릭터 실루엣과 둥근 돌·나무·꽃이 어우러진 따뜻한 스토리북 분위기로 다듬습니다. 원화 10개 PNG와 3D·UI의 실제 적용 범위는 각 제작 기록을 따릅니다.

두 게임에는 서로 다른 오리지널 오케스트라 BGM이 포함됩니다. Web에서는 로딩 뒤 게임 화면을 한 번 클릭하거나 탭하면 음악이 시작됩니다. 카드는 **음악**에서 켜기/끄기·음량을, 서사는 **설정**에서 배경 음악 켜기/끄기·전체 음량을 조절합니다. 자세한 조작은 [첫 플레이 안내](Docs/PlaytestGuide.md#배경-음악과-음량)를 참고하세요.

PC 마우스/키보드와 모바일 가로 화면의 터치 입력을 유지합니다. 3D 표정·걷기·음향의 최종 완성도와 실제 휴대폰 성능은 별도 검수 범위입니다. 실제 측정 전에는 목표 플레이 시간·60fps·최종 비주얼을 달성한 것으로 간주하지 않습니다. Git LFS를 설치하고 클론해야 모델·그림·글꼴·음원을 받을 수 있습니다.
