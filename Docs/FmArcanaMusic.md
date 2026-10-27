# 판타지마스터즈 장면별 음악 전환

현재 `fm-arcana-2026-10-27` 음악 개정은 기존 네 원작 음원의 내용·출처·편집 구간을 유지하고 재생 수명 주기를 바꾼다. 기존판은 로비·덱을 나갈 때 녹음을 일시 정지하고 돌아오면 이전 위치에서 재개해, 새 화면의 시작이 곡 중간에 이어졌다. Web에서는 대상 재생이 확인되는 순간 이전 곡의 gain을 바로0으로 만들어 경계가 급했다.

## 재생 정책

- 로그인·로비·덱·전투 중 **실제 음악 역할이 바뀌면** 새 대상 녹음을0초에서 시작한다. 로비→덱→로비도 로비 녹음의 처음으로 돌아온다.
- 대상 로컬 녹음이 준비될 때까지 기존 곡을 유지한다. 준비된 대상은 약0.42초 동안 올라오고, 이전 곡은 약0.18초 동안 내려간 뒤 일시 정지한다. WebAudio gain 예약은 렌더 프레임과 독립적이며, 직접 HTML 미디어 대체 경로도 같은 짧은 볼륨 전환을 사용한다. Native 가중치는 Unity의 unscaledDeltaTime으로 갱신한다.
- 같은 역할의 UI 재생성·정렬·상세 복귀는 녹음을 다시 감지 않는다. 설정·음량 변경·음소거도 새로운 장면으로 취급하지 않는다.
- 음소거는 현재 위치에서 일시 정지한다. 같은 역할에서 다시 켜면 그 위치를 이어간다. 음소거 중 다른 역할로 갔다면 새 역할은 켤 때0초에서 시작한다. 볼륨0은 음소거와 별개이며 재생 위치를 임의로 초기화하지 않는다.
- 빠른 A→B→A에서도 이전 fade 타이머나 늦게 완료된 play promise가 새 A를 끄거나 잘못된 B를 들리게 하지 않는다. 같은 요소가 아직 들릴 때는 짧은 tail을 내린 뒤 되감는다.
- 기존 사용자 제스처·음량/음소거 저장 키, 네 고정 HTML audio 요소·한 WebAudio graph, Native 네 AudioSource를 유지한다. 일반 플레이에 외부 플레이어나 SDK를 추가하지 않는다. 예수님 게임의 BGM/Shared BgmPlayer는 변경하지 않았다.

출처와 원곡 발췌의 한계는 [원작 음악 조사](Research/FmFidelityMusic.md)를 따른다. 안내 음성을 제외한 짧은 발췌를 전체 OST 복구라고 표시하지 않는다.

## 재현 가능한 수명 주기 검사

```sh
python3 Tools/Audio/Arcana/verify-web-lifecycle.py
source Tools/Environment/activate-shell.sh
python3 Tools/Audio/Arcana/verify-native-lifecycle.py
```

Web18개 검사는 실제 HTML 안의 컨트롤러를 실행한다. A→B→A의0초 시작, A→A의 위치 보존, 음소거 재개, 빠른 역할 변경, 이전 promise/fade cleanup 취소, 네 요소·한 graph, MP3 대체와 WebAudio가 없는 경우의 페이드·변경 중 음량 반영을 결정론적 미디어 시계로 검사한다. Native29개 검증 조건은 **실제 FmLocalMusicHost.cs**를 AudioSource 시계 대역과 함께 컴파일해 같은 전환·mute·준비 지연·한 루프 복귀·읽기 전용 기록을 확인한다. 이 결과는 Unity 장치 출력·실제 디코딩·브라우저 PCM·사람의 청취 검수가 아니다.

결과는 `Artifacts/Validation/Arcana/MusicWeb/report.json`과 `MusicNative/report.json`에 소스 SHA와 함께 기록한다. 실제 Unity·Web 빌드의 시간과 음원 출력은 릴리스 담당이 별도로 확인한다.

## 실제 실행에서 확인할 값

Web의 `window.FirstGameLocalMusic.getState()`는 `sceneTransitionCount`, `sameRoleMessages`, 현재 역할·실제 소스/위치·context 상태를 반환한다. `getDiagnostics()`는 네 고정 요소의 `restartCount`, `lastStartPosition`, `lastStartAtMs`, `lastStartRoleSerial`, `lastPromotionPosition`, 현재 위치·pause·gain 목표를 반환한다. `getTransitions()`는 최대24개의 복사된 역할 요청/재생 요청/실제 승격 기록이다. `restartCount`는0초 seek를 성공시킨 요청 수이며, 실제 재생 증거는 `promoted` 기록·시계 진행·별도 PCM으로 확인한다.

Native `FmLocalMusicHost`의 `RequestedScene`, `ActiveScene`, `PlaybackPosition`, `SceneTransitionCount`, `SameRoleApplies`와 최대24개의 복사된 `PlaybackStarts`를 읽는다. 시작 기록에는 역할·clip 이름·역할 전환 번호·실제 AudioSource.time·AudioSettings.dspTime이 있다. 기록은 재생 상태나 게임 판정을 조작하지 않는다.

권장 실제 조작은 로비에서2초 이상 재생→도감/덱 화면→로비 복귀다. 복귀한 로비의 재시작 수가 증가하고 현재 시작 기록이0초, 첫 관찰 시간이1초 미만인지 확인한다. 같은 역할에서 상세/필터/설정 왕복으로 시간이 계속 진행하고 시작 기록이 늘지 않는지도 확인한다. 전환 후0.6초 이상 지난 시점에는 한 곡만 재생해야 하며, 실제 PCM이 존재해야 한다. 빠른 이동 시 순간적인 짧은 crossfade와 지속적인 중첩 재생을 구분한다. 실제 사용자의 미적 자연스러움·물리 스피커 청취·실기기 성능은 이 자동 검사만으로 보장하지 않는다.
