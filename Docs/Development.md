# 개발과 실행

## 고정 도구

- Unity 6000.3.26f1 (de8167e7440c), C#.
- URP 17.3.0, uGUI 2.0.0(TMP 포함), Input System 1.20.1, Cinemachine 3.1.7, Timeline 1.8.13.
- Unity Test Framework 1.6.0. PathOfDawn의 공식 Ink Unity Integration 2.0.0은 tag commit `a0b734d906a6224050af7c4f39392fd9ba5988fe`의 패키지를 변경 없이 임베드.
- 선택적 연결: Unity CLI 1.0.0-beta.13, 공식 Codex unity 플러그인 0.1.8-beta, Pipeline 0.8.0-exp.1. CLI/Pipeline 실패 시 지원하는 에디터 메뉴와 배치 실행을 사용한다.
- Blender 4.3.2, 엔진 독립 테스트용 .NET SDK 8.0.426.

## Windows 개발 PC

1. Git과 Git LFS를 설치하고 저장소를 받는다. 이미 클론했으면 저장소 안에서 `git lfs install --local`, `git lfs pull`, `git lfs checkout`을 실행한다. 모델·원화가 작은 텍스트 포인터로 남아 있으면 Unity를 열기 전에 이 명령으로 실제 파일을 받는다.
2. Unity Hub에서 정확한 에디터 버전과 Windows/WebGL 모듈을 설치하고 본인 계정의 적합한 라이선스를 활성화한다.
3. Hub의 Add project로 `Games/FantasyCards`, `Games/PathOfDawn`을 각각 등록한다.
4. 패키지 가져오기가 끝나면 `FirstGame > Prepare First Slice`를 실행한다. 최초 실행 시 자동 초기화도 등록되어 있다. 이 단계가 한국어 TMP atlas, URP renderer, 게임별 표현 자산과 카드/서사 ScriptableObject를 준비한다. 새벽의 길은 그림 가져오기·보행 UV와 이전 모델 프리팹 보존을 포함한다. 생성한 에셋과 `.meta`를 함께 커밋한다.
5. `Assets/Scenes/Boot.unity`에서 Play. 원본 `.ink`는 공식 v2 importer가 `InkFile`로 가져온다. TextAsset JSON 경로를 별도 추측해 만들지 않는다.
6. `FirstGame > Validate First Slice`, Test Runner의 EditMode 및 UI smoke 테스트를 실행한다.
7. `FirstGame > Build Windows` / `Build Web`은 각각 `Artifacts/Builds/<Game>/Windows|Web`에 결과를 쓴다. Windows/WebGL Build Profile은 `Assets/Settings/Build Profiles`에 포함돼 있다. 아래 공식 CLI 명령으로 다시 생성할 수 있다. 위 메뉴와 배치 스크립트가 플랫폼별 URP 품질 설정을 적용한다.

## 현재 Linux 클라우드

```bash
cd /workspace/firstgame
source Tools/Environment/activate-shell.sh
# 새 환경에서 필요한 경우. 인증 상태 출력은 자격 증명 값을 포함하지 않는다.
bash Tools/Environment/install.sh
bash Tools/Validation/run-core-tests.sh

bash Tools/Build/unity-task.sh FantasyCards prepare
bash Tools/Build/unity-task.sh PathOfDawn prepare
bash Tools/Build/unity-task.sh FantasyCards edit-tests
bash Tools/Build/unity-task.sh PathOfDawn edit-tests
# UI smoke fixtures enter actual PlayMode from the Editor test runner.
bash Tools/Build/unity-task.sh FantasyCards play-tests
bash Tools/Build/unity-task.sh PathOfDawn play-tests

bash Tools/Build/unity-task.sh FantasyCards profiles
bash Tools/Build/unity-task.sh PathOfDawn profiles
bash Tools/Build/unity-task.sh FantasyCards windows
bash Tools/Build/unity-task.sh FantasyCards web
bash Tools/Build/unity-task.sh PathOfDawn windows
bash Tools/Build/unity-task.sh PathOfDawn web
```

`UNITY_EDITOR`로 다른 운영체제의 정확한 에디터 실행 파일을 지정할 수 있다. 공식 CLI의 프로필 생성은 `unity build <project> --create-profile StandaloneWindows64`와 `--create-profile WebGL`이며, 생성 후 `--list-profiles`로 확인한다. 생성하지 못한 프로필을 이미 존재한다고 보고하지 않는다.

각 클라우드 작업은 이미 격리되어 있으므로 기존 체크아웃을 사용한다. 사용자가 명시적으로 요청하지 않으면 Git worktree를 만들지 않는다. 스냅샷에는 파일과 설치 도구가 남지만 에디터·Xvfb·서버 프로세스와 계정/라이선스 세션이 유지된다고 가정하지 않는다.

## 입력과 저장

- 별빛의 서약: 카드를 클릭해 선택 → 지형 전개 또는 전장 칸 → 공격 단계에서 공격자와 대상 → 방어 단계에서 방어 유닛 또는 가로막지 않기. 상세 보기, 덱 구성, 저장/이어하기, 재도전은 화면 버튼으로 진행한다.
- 새벽의 길: WASD/화살표로 화면 기준 이동, 오른쪽 마우스 드래그로 제한된 장면 이동, E 대화·조사·다음 대사, M 기억·회귀, Esc 설정. 모바일 왼쪽 패드와 오른쪽 장면 드래그도 같은 원칙을 따른다. 게임패드 입력도 바인딩돼 있으며 실제 검증 여부는 별도 기록한다.
- Windows 저장: `Application.persistentDataPath/Saves`. UTF-8 JSON envelope와 SHA-256, 버전, 안정 콘텐츠 ID를 사용하고 이전 파일은 `.backup`에 보존한다.
- Web 저장: Unity PlayerPrefs/IndexedDB. 브라우저 origin마다 별도 저장이고 브라우저 데이터 삭제 시 사라질 수 있으므로 내보내기를 제공한다.
- 게임 내 저장 내보내기/가져오기에서 같은 portable JSON을 Web→Windows로 전송한다. 가져오기는 게임 ID/버전/무결성 및 실제 게임 상태를 검증한 뒤 저장을 바꾼다. 손상되거나 미래 버전인 저장은 자동 초기화하지 않는다.
- 저장 데이터에는 자격 증명을 포함하지 않는다. 불러오기 실패 원본은 유지하고 명시적 가져오기를 통해 복구한다.

## 품질과 산출물

새벽의 길의 현재 시각 경로는 [PaintedDawn.md](PaintedDawn.md)다. `ProjectSetup`은 `Resources/Art/Painted`의 일곱 PNG를 원본 크기·sRGB·무압축·mipmap 없이 가져오고, 입력 알파와 GUID를 보존한다. 투명 경계와 발 위치를 측정한 `layout.json`(전신 영역 4개·보행 프레임 8개)으로 원화를 읽으며 원본 PNG 픽셀은 수정하지 않는다. 이전 3D 프리팹은 `Art/Archive/LegacyModels`에 보존하고 Boot의 모델 필드에서 분리한다. 원화 생성 기록과 이전 FBX 제작 이력을 현재 Unity 화면 검증으로 대신하지 않는다.

Windows는 URP renderScale 1.0, HDR, shadow2048/40m; Web은 renderScale0.8, LDR, shadow1024/20m, texture mip limit1로 별도 빌드한다. Web은 gzip+decompression fallback, 단일 스레드 기본을 사용한다. 핵심 에셋과 한국어 글꼴은 로컬 포함이며 게임 중 생성형 AI API를 호출하지 않는다. 현재 개발 자산·음향과 향후 교체 사양은 ArtDirection을 따른다.

Windows 1080p60은 목표이며 실제 Windows 하드웨어에서 측정해야 한다. Linux의 llvmpipe는 소프트웨어 렌더러이므로 실제 성능 목표의 근거로 사용하지 않는다. Blender 렌더와 Unity 실행 캡처는 별도 분류한다.

Web 플레이어는 `file://`로 열지 않고 HTTP 서버에서 실행한다. 포함된 `FirstGame` WebGL 템플릿은 16:9 화면을 창 크기에 맞추고 실제 로딩 진행과 오류를 표시한다. 내부 브라우저 검증은 `python3 Tools/Validation/web-smoke.py --tessdata /workspace/tools/tesseract-best`로 실행하며, 브라우저·한글 OCR 도구가 설치된 이 클라우드에서 사용한다. 검증 스크립트가 임시 로컬 서버를 시작하고 종료한다.

두 대상 빌드가 모두 성공한 뒤 `python3 Tools/Build/package-builds.py`를 실행하면 게임별 Windows/Web ZIP과 SHA-256 목록을 `Artifacts`에 만든다. 각 플레이어 ZIP에는 첫 플레이 안내와 검증 기록도 포함한다. `python3 Tools/Build/export-source.py`는 캐시·빌드 결과를 제외하고 실제 LFS 에셋을 포함한 Unity 소스 ZIP을 만든다. 저장소에 인증 파일을 넣지 않는다. 플랫폼 빌드와 원본 소스 ZIP은 서로 다른 산출물이다.

해당 개정의 native 테스트와 필수 로컬 브라우저 검증 결과 파일이 준비되면 `python3 Tools/Build/package-evidence.py`로 테스트 XML/TRX, 실제 화면, 빌드 결과, 검증 문서의 묶음을 만든다. 명시한 파일만 포함하며 원시 CLI 로그와 인증 정보는 수집하지 않는다. 결과 파일이 있다는 이유로 검사를 통과했다고 판단하지 않고 각 보고서의 실제 결과를 확인한다.
