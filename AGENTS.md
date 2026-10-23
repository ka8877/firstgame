# 두 독립 Unity 게임

`Games/FantasyCards`(별빛의 서약)와 `Games/PathOfDawn`(새벽의 길)은 독립 실행·배포하는 Unity 프로젝트다. 요청 전문은 `Docs/Brief.md`, 검증 결과는 `Docs/Validation.md`를 읽는다. 현재 범위는 첫 전투 및 선택 두 번/결말 세 가지의 서사 구간이다. 2026-10-23 실제 플레이 피드백을 반영해 서양 반실사 원화를 현대 애니메이션·서브컬처풍으로 교체하고, 둥근 카드·UI와 부드러운 스토리북 3D, 게임별 오리지널 BGM을 추가했다. 최신 시각 방향은 `Docs/ArtDirection.md`, 음악은 `Docs/Audio.md`, 웹 배포는 `Docs/WebPlaytest.md`를 따른다. 개발 중 비주얼을 최종 품질이나 사용자 승인으로 보고하지 않는다.

## 작업과 소유권
- `Games/*/Assets/Game/Core`: 엔진과 화면에서 독립된 규칙/현재 상태. 실행 상태는 ScriptableObject에 저장하지 않는다.
- `Games/*/Assets/Game/Presentation`: uGUI/TMP, Input System, 카메라, 연출.
- `Games/*/Assets/Shared`: 두 프로젝트에 동일하게 포함하는 저장·전송·BGM 지원. 변경 시 양쪽 복사본을 동기화한다.
- `Games/*/Assets/Editor`: 씬/데이터 생성, 검증, Windows/Web 빌드.
- `Tools`: 재현 가능한 생성·검증 도구. `Docs`: 설계, 근거, 한계, 결과. `ReferencePrototype`: 보존하는 참고 자료.
- 병렬 작업은 경로 소유권을 정하고 같은 씬/프리팹을 동시에 수정하지 않는다.
- 이 클라우드 작업은 이미 격리되어 있다. 사용자가 요청하지 않는 한 Git worktree를 만들지 않는다.

## 기술과 검증
Unity **6000.3.26f1 (de8167e7440c)**, URP, C#, uGUI/TMP, Input System, Cinemachine, Timeline. 정확한 패키지 버전은 각 `Packages/manifest.json`과 잠금 파일, `Docs/Environment.md`를 따른다. PathOfDawn은 공식 Ink Unity Integration **2.0.0**을 변경 없이 임베드한다. Ink v2의 `.ink`는 `InkFile` 에셋이며 `storyJson`으로 실행한다.

`Docs/Development.md`의 실행 및 빌드 명령을 사용한다. 규칙·저장·회귀 테스트를 먼저 실행하고 Unity EditMode/PlayMode, 실제 Windows/Web 조작 검증을 구분한다. 실행하지 못한 검증이나 측정하지 않은 성능을 통과했다고 쓰지 않는다. 한국어 글꼴 포함 여부, 에셋 참조, URP 활성화, Input System 이벤트 처리를 확인한다. 직전 공개판의 보고서는 이력으로 보존하고 새 원화·3D·UI·BGM 플레이어의 통과 근거로 대신하지 않는다.

## 파일·아트·저장
- 에셋은 `.meta`와 함께 관리한다. 캐시/빌드 결과는 Git에서 제외한다. 바이너리 원화·모델·글꼴은 `.gitattributes`의 LFS 정책을 따른다.
- 기존 사용자 파일을 보존한다. 비밀키·라이선스·인증 토큰을 저장소/로그에 넣지 않는다.
- `Docs/ArtDirection.md`와 `Docs/AssetRegister.md`를 따른다. 새벽의 길 모델은 `Tools/Art/ToonDawn.md`의 생성기를 사용한다. 이전 `generate_proxies.py`를 단독 실행하면 새 모델을 구형으로 덮어쓴다. 새 원화와 3D는 개발 자산이며 사용자 승인으로 표현하지 않는다.
- 새 원화는 `Docs/Illustration.md`의 2×2 atlas 슬롯과 기존 GUID를 유지한다. 이전 반실사 그림을 필터 처리한 결과로 대체하지 않는다. 카드의 둥근 마스크·장식은 UI에서 그리며 원화에 글자나 수치를 합성하지 않는다.
- 음악은 `Tools/Audio`의 독자적 악보·렌더 경로와 샘플 라이선스 기록을 따른다. 배포에는 완성 음원과 저작권 안내를 포함하며 외부 음악 API를 요구하지 않는다. Web BGM은 실제 첫 사용자 입력으로 시작하고, 기존 `BgmPlayer`와 브라우저 AudioContext 동작을 유지한다. 타이틀/전투의 음악 설정과 서사 설정의 음악 켜기/끄기·전체 음량을 실제 UI에서 확인한다. 파일 파형 검사·브라우저 출력 검증·실제 청취를 구분한다.
- 콘텐츠 안정 ID와 저장 스키마/콘텐츠 버전을 유지한다. 손상되거나 호환되지 않는 저장은 원본을 보존하고 오류를 표시한다.
- 다음 단계 완료 조건: 실제 Unity 씬/빌드에서 두 게임의 시작→플레이→결과→저장/복귀 확인, 자동 테스트 통과, 실제 캡처와 측정 결과 기록.
