# 개발 환경과 재현 기록

기준일: 2026-10-23, Asia/Seoul. 최초 설치 기록은 2026-10-20이며 로그인·라이선스와 배치 에디터 실행을 2026-10-23에 재확인했다. 게임 기능 테스트 결과는 별도 검증 기록을 따른다.

## 고정한 도구

| 도구 | 버전 / 위치 | 확인 방법 |
| --- | --- | --- |
| Unity Editor | **6000.3.26f1**, revision **de8167e7440c** | Unity 공식 Release API의 `LTS` / `Release`, 에디터 `-version` |
| Unity CLI | **1.0.0-beta.13**, `/workspace/tools/unity-cli/bin/unity` | 공식 설치 스크립트 SHA256 검증, `--version` |
| 공식 Unity Codex 플러그인 | **0.1.8-beta** | `codex plugin list`: installed, enabled |
| .NET SDK | **8.0.426**, `/workspace/tools/dotnet` | Microsoft 공식 release metadata SHA512 검증, `dotnet --info` |
| Blender | **4.3.2**, `/usr/bin/blender` | `blender --version` |
| Xvfb | **21.1.16**, `/workspace/tools/xvfb/usr/bin/Xvfb` | Debian 서명·패키지 SHA256, 1920×1080 디스플레이 실행 |
| 운영체제 | Debian 13, Linux x86_64 | `/etc/os-release`, `uname -a` |

설치 시작 시 CPU 5개, 가용 메모리 약 34GB, 가용 디스크 약 30GB였다. 에디터와 필요한 Windows Mono·WebGL 모듈만 선택했다. Windows IL2CPP 빌드와 Windows 실행 검증에는 별도의 지원되는 Windows 환경이 필요하다. Debian은 Unity의 Ubuntu 지원 플랫폼과 동일한 검증 범위라고 주장하지 않는다.

Unity 에디터 경로:

```text
/workspace/tools/unity-editors/6000.3.26f1/Editor/Unity
```

공식 CLI 설치 결과에서 에디터, `windows-mono`, `webgl` 세 항목 모두 `installed`, 전체 `success:true`를 확인했다. 모듈 목록에서도 Windows와 Web이 `Installed`였다. 설치 후 가용 디스크는 약 15GB였다.

게임 프로젝트 가져오기 전 추가 점검에서 가용 디스크 약 13GB, 가용 메모리 약 31GiB를 확인했다. Windows x64 Mono 개발·배포용 변형 파일이 모두 존재한다. WebGL에 포함된 Emscripten **3.1.39-git**, Node **15.14.0**, Binaryen **112**의 버전 실행을 확인했고 LLVM clang의 누락 공유 라이브러리는 없었다. 독립적인 `emcc` 호출은 번들 도구 경로를 담은 `EM_CONFIG`가 필요하며 Unity 빌드 도구는 자체 설정을 사용한다. 이 점검은 실제 게임 빌드 성공을 의미하지 않는다. 플레이어 빌드 시 cgroup `cpu.max=400000 100000`과 `memory.max=34359738368`을 확인했다. 실제 한도는 CPU 4개 분량과 32GiB이므로 WebGL 빌드는 순차 실행한다.

## 패키지 근거

| 패키지 | 고정 버전 | 확인 근거 |
| --- | --- | --- |
| URP | 17.3.0 | 설치한 6000.3.26f1의 BuiltInPackages/package.json |
| uGUI / TextMeshPro | 2.0.0 | 같은 에디터의 uGUI 패키지; TMP 포함 |
| Input System | 1.20.1 | Unity 공식 패키지 레지스트리, 최소 Unity 6000.0 |
| Cinemachine | 3.1.7 | 공식 패키지 레지스트리, 최소 Unity 2022.3 |
| Timeline | 1.8.13 | 공식 패키지 레지스트리, 최소 Unity 6000.0 |
| Unity Test Framework | 1.6.0 | 설치한 에디터에 포함된 버전 |
| Unity Pipeline | 0.8.0-exp.1 | 공식 CLI 설치 결과와 Unity 패키지 레지스트리 |

공식 API 확인에 사용한 실제 소스는 `/workspace/tools/packages/`에 있다. Cinemachine 3의 네임스페이스는 `Unity.Cinemachine`이며 `CinemachineCamera`, `Follow`, `LookAt`, `Lens`, `Priority`, `CinemachineFollow.FollowOffset`을 확인했다. Input System의 `InputSystemUIInputModule.AssignDefaultActions()`와 Timeline 1.8.13 소스도 확인했다. 별도 `com.unity.textmeshpro` 구버전을 중복 추가하지 않는다.

기계가 읽을 수 있는 버전·원본 URL·체크섬 근거는 [package-evidence.json](../Tools/Environment/package-evidence.json), [unity-release.json](../Tools/Environment/unity-release.json)에 있다. Ink Unity Integration의 정확한 고정 커밋은 PathOfDawn의 패키지 manifest와 콘텐츠 문서를 따른다.

## 설치와 활성화

현재 작업 공간은 이미 독립된 클라우드 환경이다. 기존 `/workspace/firstgame` 체크아웃을 사용하며 사용자 요청 없이 별도 Git worktree를 만들지 않는다.

```bash
cd /workspace/firstgame
bash Tools/Environment/install.sh
source Tools/Environment/activate-shell.sh
```

이 스크립트는 고정한 .NET, CLI, 에디터와 모듈을 재설치·보완한다. 다운로드의 TLS와 공식 체크섬을 검증하며 검증 우회 옵션을 쓰지 않는다. CLI의 사용자 설정 및 Codex 플러그인 설치는 사용자 홈에 쓰므로 이 클라우드 샌드박스에서는 승인된 권한 상승 실행이 필요했다. 첫 일반 네트워크 요청에서 발생한 403은 승인된 실행으로 해결되었고, 도메인 허용 목록 전체를 덮어쓰지 않았다.

스크립트의 실제 반복 실행으로 이미 설치된 모듈은 다시 설치하지 않는 것을 검증했다. 의존성 설치·검증은 **exit 0**으로 완료했다. Personal 활성화 후 별도 런타임 진단도 **exit 0**, `runtimeReady:true`를 확인했다. 새 스냅샷에서 로그인·라이선스가 없더라도 설치를 실패로 취급하지 않지만, 예상하지 못한 명령 실패나 JSON·상태 형식 오류는 설치를 실패시킨다. 설치 성공이 에디터 실행 준비 완료를 의미하지는 않는다. `activate-shell.sh`를 source한 뒤 `dotnet --version`과 `unity --version`도 실제 확인했다.

에디터를 실행하기 전에 `python3 Tools/Environment/check-runtime.py`를 실행한다. `authReady`는 CLI 계정 세션, `licenseReady`와 `runtimeEditorReady`는 에디터 라이선스 준비 상태이며 기존 `runtimeReady` 필드는 같은 라이선스 상태를 계속 제공한다. 정상적인 CLI 미로그인(`auth status`의 `exit 3`, `loggedIn:false`)은 유효한 에디터 라이선스를 막지 않는다. 라이선스가 없으면 기본 검사는 `exit 4`로 실패한다. 의존성 설치 스크립트만 `--allow-missing-license`를 사용해 이 상태를 진단으로 남긴다. 예상하지 못한 명령·JSON·상태 형식 오류는 계속 `exit 6`이며 계정 식별 정보나 인증 값은 출력하지 않는다.

공식 플러그인 설치 명령:

```bash
codex plugin marketplace add Unity-Technologies/unity-agent-plugin
codex plugin add unity@unity-agent-plugin
codex plugin list
```

플러그인은 실제 설치되었다. 목록 명령의 별도 원격 전체 카탈로그에서 451 경고가 나왔지만, 로컬 `unity@unity-agent-plugin`의 설치·활성 상태는 확인했다. 이 세션이 이미 로드한 도구 목록과 다음 세션에서 발견할 플러그인 스킬은 구분한다.

최초 Unity 로그인은 사용자가 브라우저에서 완료했고 당시 CLI의 `loggedIn:true`, `sessionState:fresh`를 확인했다. 2026-10-23에는 CLI 세션이 없는 `authReady:false` 상태였지만 기존 Personal 라이선스가 유효해 `licenseReady:true`, `runtimeEditorReady:true`, `runtimeReady:true`였고, 실제 배치 에디터도 **exit 0**으로 종료했다. 이번 작업에는 사용자 재로그인이나 약관 재동의가 필요하지 않았다. 계정 의존 명령을 실제로 수행해야 하고 세션이 없을 때만 공식 로그인 흐름을 사용한다. 비밀번호·토큰은 채팅이나 저장소에 복사하지 않는다.

일반 샌드박스의 `auth status`는 보호된 로컬 인증 저장소 때문에 `LOCAL_STORE_UNWRITABLE`/`exit 6`을 반환했다. 승인된 권한 실행에서는 정형 미로그인 상태를 읽었고, 수정한 진단기 전체가 **exit 0**이었다. 저장소 접근 오류를 로그인·라이선스 만료로 단정하거나 저장소 권한을 느슨하게 바꾸지 않는다. 진단기는 이런 오류 코드를 비밀 값 없이 보여 주며 라이선스 결과도 별도로 기록한다.

```bash
unity auth status --json
unity license status --json
# 계정 의존 작업에 로그인이 실제로 필요하고 세션이 없을 때만:
unity auth login
```

사용자가 Unity Personal 이용 자격과 소프트웨어 약관 동의를 명시한 뒤 `unity license activate --personal --accept-eula --non-interactive --json`을 실행했다. 활성화 응답은 `activated:true`와 제품 2개였으며, 이후 실제 상태 조회에서 **active:true / exit 0**, 에디터 실행에서도 **exit 0**을 확인했다. 무료 Personal 활성화에 결제나 유료 구독 신청은 없었다. 초기 기본 entitlement 활성화의 빈 제품 응답만으로 성공을 판단하지 않고 실제 상태·실행으로 확인했다. 필요한 비밀 값은 채팅이나 Git 파일에 입력하지 않는다.

## 실제 실행 검증

- `Unity -version`: **6000.3.26f1**, 성공.
- `ldd Unity`: 누락된 공유 라이브러리 없음.
- `python3 Tools/Environment/check-runtime.py`: 최초에는 로그인·라이선스 모두 유효. 2026-10-23 재검증은 CLI 미로그인과 유효한 라이선스를 구분해 **exit 0**, `authReady:false`, `licenseReady:true`, `runtimeEditorReady:true`, 기존 `runtimeReady:true`를 확인했다.
- 2026-10-23 기존 `/workspace/tools/unity-smoke`의 실제 `-batchmode -nographics -quit` 실행: **exit 0**, 정상 종료 로그 확인. 라이선스·스크립트 컴파일 오류 없음. `start-display.sh`도 **exit 0**으로 `:99`/Mesa llvmpipe/OpenGL 4.5를 확인했다.
- Personal 활성화 후 `Unity -batchmode -nographics -quit -createProject /workspace/tools/unity-smoke`: **exit 0**. 정확한 엔진 버전의 프로젝트 파일 생성, 스크립트 컴파일 약 18.63초, `Batchmode quit successfully invoked` 확인.
- 초기 라이선스 미활성 상태의 **exit 198**은 Personal 활성화 후 재검증으로 해결되었다.
- `unity pipeline install`: 두 프로젝트 manifest에 **0.8.0-exp.1** 추가 성공.
- FantasyCards의 실제 GUI 에디터에서 공식 Pipeline **연결 검증 통과**. 명령 목록에 `editor_status`가 있었고 `ready`, 컴파일·리로드 없음, `playMode:stopped`, 프로젝트 절대 경로와 엔진 버전 일치를 확인했다. 읽기 전용 eval도 **6000.3.26f1**을 반환했다. PathOfDawn의 별도 라이브 연결은 아직 이 환경 점검에서 확인하지 않았다.
- 첫 GUI 실행의 Unity Editor Software Terms 창은 이미 받은 사용자의 명시적 소프트웨어 약관 동의에 따라 처리했다. 이 대화상자가 프로젝트 로드를 막는 동안의 연결 실패를 설치 실패로 오인하지 않았다.
- 읽기 전용 진단 후 `unity close`로 점검 에디터를 정상 종료했다. 프로세스 **exit 0**, 해당 프로젝트의 `UnityLockfile` 해제 확인. 앱·씬·importer 설정은 이 진단에서 변경하지 않았다.
- 클라우드에 물리 오디오 출력 장치가 없어 ALSA/FMOD 출력 장치 초기화 경고가 발생했다. 기본 프로젝트 생성은 성공했으며 실제 음향 청취 검증은 별도다.

최초 활성화 후 성공 로그는 `/workspace/tools/downloads/unity-smoke-after-personal.log`, 2026-10-23 재확인 로그는 `unity-smoke-session-renewal.log`, 안전한 상태 요약은 `runtime-session-renewal-status.json`이다. 설치 로그는 `unity-install.log`, 초기 실패 로그는 `unity-smoke.log`, `unity-smoke-after-login.log`로 구분한다. 로그인 URL·계정 기록은 저장소에 넣지 않는다. 이 기본 프로젝트 실행 성공은 게임 프로젝트의 Play Mode나 Windows·웹 빌드 성공을 대신하지 않으며, 해당 결과는 각각의 실행 기록을 따른다.

라이브 Pipeline의 상태·버전 eval·atlas 형식 진단은 계정·토큰 정보를 제거한 `Artifacts/Environment/pipeline-fantasycards.json`에 기록했다. atlas 진단은 수정 전의 TextureCube/Cubemap 상태를 기록하며, 해당 콘텐츠 수정 이후의 결과는 게임 검증 기록에서 구분한다.

## 지원되는 실행·프로필 경로

실제 에디터 창을 띄우고 화면을 캡처할 가상 디스플레이도 준비했다.

```bash
cd /workspace/firstgame
bash Tools/Environment/start-display.sh
source Tools/Environment/activate-display.sh
source Tools/Environment/activate-shell.sh
"$UNITY_EDITOR" -projectPath Games/FantasyCards -force-glcore
# 별도 터미널에서도 activate-display.sh를 source한 뒤:
import -window root /workspace/tools/editor-capture.png
```

가상 화면은 `:99`, 1920×1080이다. `xdotool getdisplaygeometry`, `glxinfo -B`, 루트 화면 캡처를 실제 실행했다. 현재 GPU 대신 **Mesa llvmpipe 25.0.7 / OpenGL 4.5**로 렌더링한다. 이는 화면 기능 검증용이며 Windows PC의 60fps 성능 측정 근거가 아니다. 아직 빈 디스플레이 캡처를 게임 화면으로 제공하지 않는다.

Xvfb 패키지는 Debian trixie `InRelease`를 시스템 Debian 키링과 `sqv`로 서명 검증한 뒤 `Packages.xz`, 각 `.deb`의 SHA256까지 대조했다. 시스템 패키지를 덮어쓰지 않고 `/workspace/tools/xvfb`에 풀었다. 재현은 `install-display.py`와 `display-packages.json`에 기록했다. 라이브 Xvfb 프로세스는 새 클라우드 작업에서 자동 복원된다고 가정하지 않고 `start-display.sh`로 시작한다.

라이선스가 유효한 환경에서 각 프로젝트를 연 다음 연결을 확인한다.

```bash
unity open /workspace/firstgame/Games/FantasyCards
unity pipeline list --json
unity command --project-path /workspace/firstgame/Games/FantasyCards --json
```

다른 프로젝트에는 `Games/PathOfDawn` 경로를 사용한다. 동시에 에디터 둘을 열면 모든 연결 명령에 `--project-path`를 명시한다. Pipeline 설치 명령은 프로젝트 manifest를 변경하므로 고정 버전과 함께 실행하고 변경을 검토한다.

```bash
unity pipeline install --project-path Games/FantasyCards --package-version 0.8.0-exp.1
unity pipeline install --project-path Games/PathOfDawn --package-version 0.8.0-exp.1
```

현재 CLI의 `build --help`에서 확인한 실제 Build Profile 생성 경로:

```bash
unity build Games/FantasyCards --create-profile StandaloneWindows64
unity build Games/FantasyCards --create-profile WebGL
unity build Games/PathOfDawn --create-profile StandaloneWindows64
unity build Games/PathOfDawn --create-profile WebGL
unity build Games/FantasyCards --list-profiles
```

`--create-profile`은 에디터를 실행해 프로필만 생성하며 빌드는 수행하지 않는다. 생성한 실제 프로필 경로를 `--profile`에 전달하고 `--output-path`를 지정해 빌드한다. 프로젝트의 에디터 부트스트랩과 배치 빌드 명령은 루트 실행 문서를 따른다. 배치 경로는 실험적 CLI 연결 실패 시 사용할 수 있지만, 유효한 Unity 라이선스 자체를 대신할 수는 없다.

## 브라우저·OCR 전제 조건 재현

WebGL 검증에 사용한 도구는 다음 베이스 이미지에 이미 포함되어 있었다. 이번 작업에서 Chromium, Python 패키지 또는 Tesseract를 새로 설치하지 않았다.

| 도구 | 확인한 버전 | 위치 / Debian 패키지 버전 |
| --- | --- | --- |
| Python | 3.12.14 | `/opt/codex/runtimes/codex-primary-runtime/dependencies/python/bin/python3` |
| Chromium | 154.0.8037.57 | `/usr/bin/chromium`, `154.0.8037.57-1~deb13u1` |
| Playwright | 1.62.0 | 위 Python의 `playwright` 패키지 |
| Pillow | 12.3.0 | 위 Python의 `Pillow` 패키지 |
| Tesseract | 5.5.0 | `/usr/bin/tesseract`, `5.5.0-1+b1` |
| 시스템 영어 OCR 데이터 | 4.1.0 | `tesseract-ocr-eng`, `1:4.1.0-2` |

`install.sh`는 [install-browser.py](../Tools/Environment/install-browser.py)를 호출한다. 이 경량 헬퍼는 실행 파일 버전, Python 모듈 import, 한국어·영어 모델의 해시와 실제 OCR 모델 로드를 검사한다. 시스템/Python 패키지를 변경하거나 브라우저·프로필을 열지 않는다. 새 비주얼 검증에는 기본 `best` 모델과 `/workspace/tools/tesseract-best` 경로를 사용한다. `FIRSTGAME_TOOLS_ROOT`, `FIRSTGAME_TESSDATA` 또는 `--tessdata`로 경로를 바꿀 수 있다. 기존 fast 모델은 `--model fast`와 기본 `/workspace/tools/tesseract` 경로로 보존한다.

```bash
python3 Tools/Environment/install-browser.py
```

없는 데이터만 공식 저장소의 고정 커밋에서 검증된 HTTPS로 받아 SHA256을 대조한 후 설치한다. 기존 파일의 해시가 다르면 덮어쓰지 않고 실패한다. 모델별 원본 URL·byte 수·해시는 데이터 폴더의 `provenance.json`에도 기록한다.

현재 기본 모델은 [tesseract-ocr/tessdata_best 커밋 e12c65a915945e4c28e237a9b52bc4a8f39a0cec](https://github.com/tesseract-ocr/tessdata_best/tree/e12c65a915945e4c28e237a9b52bc4a8f39a0cec)이다.

| 파일 | SHA256 |
| --- | --- |
| `kor.traineddata` | `f888d4038348a0c3d25151e7f452bda0d74ca275b18cab146798bcbb94084fff` |
| `eng.traineddata` | `8280aed0782fe27257a68ea10fe7ef324ca0f8d85bd2fd145d1c2b560bcb66ba` |

2026-10-23 실제 새 빈 경로에서 best 모델 설치와 재사용을 각각 실행해 두 번 모두 **exit 0**을 확인했다. 첫 실행은 두 파일 `installed`, 두 번째는 `reused`였고 기준 버전 일치·두 언어 모델 로드도 통과했다. 현재 공용 경로에서도 `reused`와 **exit 0**을 확인했다. 근거는 `/workspace/tools/downloads/browser-prerequisites-best-{first,second,current}.json`이다. 새 원화 타이틀에서 기존 fast OCR이 ‘빛’을 ‘빚’으로 오독한 원본 실패는 보존하며, 새 검사는 제목 영역을 좁혀 best 모델과 필요한 OCR 전처리를 사용한다. 원본 캡처와 원시/전처리 OCR을 분리해 기록하고 기대 문구의 철자를 바꾸지 않는다.

기존 [tesseract-ocr/tessdata_fast 커밋 87416418657359cb625c412a48b6e1d6d41c29bd](https://github.com/tesseract-ocr/tessdata_fast/tree/87416418657359cb625c412a48b6e1d6d41c29bd)의 검증 기록은 다음과 같다. 최초 수동 다운로드한 한국어 파일과 시스템에서 복사한 영어 파일도 이 공식 커밋의 파일과 정확히 일치했다.

| 파일 | SHA256 |
| --- | --- |
| `kor.traineddata` | `6b85e11d9bbf07863b97b3523b1b112844c43e713df8b66418a081fd1060b3b2` |
| `eng.traineddata` | `7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2` |

기존 fast 모델도 새 빈 경로에서 헬퍼를 두 번 실행했다. 첫 실행은 두 파일 모두 `installed`, 두 번째는 모두 `reused`였으며 두 실행 모두 **exit 0**, `browserPrerequisitesReady:true`, `matchesVerifiedVersions:true`, `ocrModelsLoaded:[kor,eng]`였다. 실제 공용 데이터 경로에서도 변경 없이 `reused`와 **exit 0**을 확인했다. 결과는 `/workspace/tools/downloads/browser-prerequisites-{first,second,current}.json`에 있다. 브라우저를 띄우지 않는 이 검사 결과는 실제 WebGL 조작 검증과 구분한다.

다른 베이스 이미지에서 패키지가 없으면 실행 중인 검증을 끝낸 뒤 아래 복구 명령을 필요한 항목에만 사용한다. Debian 시스템 패키지 명령은 적절한 설치 권한으로 실행하며 저장소 서명 검증을 유지한다. 현재 이미지에서는 이 복구 설치를 실행하지 않았다.

```bash
apt-get update
apt-get install --no-install-recommends chromium tesseract-ocr tesseract-ocr-eng
python3 -m pip install --only-binary=:all: playwright==1.62.0 Pillow==12.3.0
python3 Tools/Environment/install-browser.py
```

시스템 패키지는 활성 Debian 저장소에서 제공하는 버전으로 복구된다. 위 표는 실제 검증한 기준 버전이며, 헬퍼는 버전이 달라도 실제 값을 출력하고 `matchesVerifiedVersions:false`로 구분한다. 해당 경우 실제 게임 브라우저 검증을 다시 실행한다. 시스템 Chromium을 명시적으로 쓰므로 별도의 `playwright install` 브라우저 다운로드는 필요 없다. OCR은 `OMP_THREAD_LIMIT=1`로 모델을 로드한다.

## 실제 Web 검사 실행과 캡처 경로

카드게임은 기본 headless SwiftShader 경로로 최종 통과했다.

```bash
python3 Tools/Validation/web-smoke.py --game FantasyCards --tessdata /workspace/tools/tesseract-best
```

서사게임은 SwiftShader에서 캡처 시간 초과가 발생했지만 당시 Unity ready·JS 응답·WebGL 컨텍스트는 유지됐다. 빈 WebGL2 캔버스에서 확인한 X11/ANGLE/Mesa 경로로 같은 빌드를 다시 실행해 최종 저장·이어하기 검사를 통과했다.

```bash
bash Tools/Environment/start-display.sh
source Tools/Environment/activate-display.sh
python3 Tools/Validation/web-smoke.py --game PathOfDawn --renderer x11-mesa \
  --interaction-resolution 1280x720 --tessdata /workspace/tools/tesseract-best
```

관측 렌더러는 `ANGLE (Mesa, llvmpipe (LLVM 19.1.7 256 bits), OpenGL 4.5)`다. 서사 조작은 720p, 타이틀과 저장 화면 캡처는 실제 720p·1080p에서 검사한다. 실패 원본은 `report-before-capture-fallback.json`, 최종 결과는 게임별 `report.json`에 있다. 소프트웨어 렌더러의 지원되지 않는 내부 셰이더 진단을 보존하며, 이 기능 검사를 실제 GPU의 화질·성능 보장으로 해석하지 않는다. 체크는 자체 브라우저·HTTP 서버를 시작하고 종료한다.

## 반복 실행 중 자원 점검

이 환경에서 여러 에디터 실행이 끝난 뒤 `VBCSCompiler.dll -shutdown` 종료 요청 헬퍼 28개가 부모 없는 상태로 남는 현상을 관찰했다. 이 작업이 설치한 정확한 Unity 도구 경로, 두 프로젝트 cwd, PPID 1, 자식 없음, 활성 Unity 빌드 트리에 속하지 않음을 확인한 대상에만 pidfd 기반 `SIGTERM`을 보냈다. 일반 dotnet/ILPP/Unity 프로세스를 일괄 종료하지 않았고 `SIGKILL`도 사용하지 않았다.

28개 대상은 모두 종료됐다. 정리 직후 cgroup 메모리는 약 30.61GB에서 28.79GB, 프로세스·스레드 수는 1031에서 551로 감소했다. 빌드와 다른 작업이 동시에 진행됐으므로 이 차이를 순수 회수량이나 연결 장애의 유일한 원인으로 단정하지 않는다. 이후 기본 샌드박스의 새 명령 실행은 실제 성공했다. `memory.events`의 `oom`, `oom_kill`은 모두 0이었으므로 이번 장애를 OOM으로 단정하지 않는다. 진단 기록은 `/workspace/tools/downloads/orphan-shutdown-cleanup.json`에 있으며 원문 인자·토큰을 포함하지 않는다.

반복 빌드 전에 디스크뿐 아니라 `/sys/fs/cgroup/memory.current`, `memory.max`, `memory.events`, `pids.current`도 확인한다. 무거운 WebGL 컴파일과 소프트웨어 렌더링 브라우저 검사는 순서대로 실행하는 편을 권장한다. 이미 종료한 에디터의 헬퍼라는 근거 없이 `pkill dotnet` 같은 광범위한 종료를 하지 않는다. 현재 빌드의 Unity/ILPP/컴파일 자식 트리는 보호하고, 기존 터미널 세션이 끊기면 먼저 새 명령으로 실제 프로세스와 로그·결과 파일 상태를 확인한다.

## 공식 출처

- [Unity 6000.3 공식 릴리스 API](https://services.api.unity.com/unity/editor/release/v1/releases?limit=5&version=6000.3)
- [공식 Unity Codex 플러그인](https://github.com/Unity-Technologies/unity-agent-plugin), 조사 커밋 `cf6b2da24e424b0a60d560a57f39f676cb6f79f3`
- [Unity CLI 공식 설치 스크립트](https://unity.com/install.sh)
- [Unity CLI 문서](https://docs.unity.com/en-us/unity-cli/use-unity-cli)
- [공식 Graphics 6000.3 소스](https://github.com/Unity-Technologies/Graphics/tree/6000.3/staging)
- [공식 uGUI 6000.3 소스](https://github.com/Unity-Technologies/uGUI/tree/6000.3)
- [Microsoft .NET 8 릴리스 메타데이터](https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json)
