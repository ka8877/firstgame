# 두 게임의 오리지널 배경 음악

2026-10-23 사용자 요청에 따라 두 게임에 서로 다른 오케스트라 BGM을 추가했다. 2026-10-26 카드 개인용 재현판은 목관·하프·현악 중심의 새 독자곡으로 교체했다. 기존 합성 차임·짧은 환경음만 있던 상태에서 실제 악기 샘플로 연주한 긴 음악을 재생한다. 게임 실행 중 MIDI 합성기나 외부 음악 API를 사용하지 않는다.

| 게임 | 곡 | 구성 | 최종 길이 / 파일 |
|---|---|---|---|
| 카드 개인용 재현판 | **별빛의 회상 / Recollections of Starlight** | E단조 계열, 88 BPM, 40마디. 플루트·오보에의 교대 선율, 현악·첼로·하프·피아노·합창 패드·첼레스타와 절제된 팀파니. 관계장조 중간부와 조용한 회귀. | 109.091초 / 1,990,779 bytes |
| 새벽의 길 | **갈릴리의 새벽 / Dawn on Galilee** | G장조 계열, 92 BPM, 40마디. 플루트/오보에의 서정 선율·피아노·하프·현악·첼로·낮은 호른. 8마디 주제와 연결부·변주. | 104.348초 / 1,903,871 bytes |

두 곡의 음표·화성·편곡은 이 프로젝트용으로 작성한 독자적 구성이다. 특정 게임의 곡·MIDI·기존 녹음이나 멜로디를 가져오지 않았다. AI 지원 개발로 만든 원본 음악이며 사람이 연주한 녹음이나 전문 작곡가의 납품으로 표현하지 않는다.

현재 카드곡의 세부 제작·비교 기록은 [ClassicAudio.md](ClassicAudio.md)에 있다. 직전 **별빛의 서곡** 음원·악보·메타·도구·검증은 `Tools/Audio/history/2026-10-26-before-classic`에 보존한다. 카드 재생 경로는 `starlight-overture.ogg`와 기존 GUID를 유지한다.

## 제작과 사용 권리

`Tools/Audio/compose-classic.py`(현재 카드곡)와 `compose-soundtracks.py`(기존 두 곡)의 명시적 악보를 `Scores/*.mid` 및 사람이 읽을 수 있는 `Scores/*.json`으로 저장한다. 새 카드 생성기는 기존 FluidSynth 렌더 함수만 재사용하고 PathOfDawn 파일을 변경하지 않는다. 악기 연주는 **FluidR3 GM** SoundFont를 설치된 FluidSynth로 오프라인 렌더링한다. FluidR3의 Frank Wen/Toby Smithe 샘플은 Debian 배포의 명시적 **MIT** 라이선스를 따른다. 실제 확인한 원문은 `Tools/Audio/FluidR3-LICENSE.txt`, 게임에 동봉하는 저작권·허락문은 `Tools/Audio/MUSIC-NOTICE.txt`다. Debian 패키징의 GPL 표기는 SoundFont 샘플의 MIT 라이선스와 구분하며 패키징 코드·SoundFont 전체를 플레이어에 포함하지 않는다.

샘플 은행은 빌드 도구로만 `/workspace/tools/audio`에 둔다. 다운로드한 Debian 패키지와 추출한 SF2의 SHA-256을 고정했다. `download-soundfont.py`는 패키지 해시가 다르면 추출하지 않는다. 실행 파일에는 44.1kHz stereo Vorbis Ogg만 포함한다. 실제 파일 해시·길이·용량·악보 해시는 `Tools/Audio/production.json`에 있다. 음표 생성은 고정 시드로 재현하며 인코더·합성기 버전이 달라졌을 때 압축 파일 바이트까지 같다고 가정하지 않는다.

```sh
python3 Tools/Audio/download-soundfont.py
python3 Tools/Audio/compose-classic.py
python3 Tools/Audio/verify-soundtracks.py
```

필요 도구는 `libfluidsynth.so.3`, Python NumPy/SciPy, FFmpeg, `dpkg-deb`다. 생성기는 두 번 연속 연주한 두 번째 음악 주기를 가져와 잔향을 이어받고, 마지막 화음에서 첫 화음으로 돌아오는 루프를 사용한다. 앞뒤에 별도 무음은 붙이지 않으며 3ms의 짧은 경계 램프로 불연속을 줄였다. 정규화 목표는 카드곡 −19 LUFS, 서사곡 −18 LUFS, true peak −1.5dB다. 실제 인코딩 후 측정값은 아래 근거를 따른다. 이전 두 곡을 다시 만들려는 경우 `compose-soundtracks.py`를 사용할 수 있으나 현재 카드곡도 이전 편곡으로 덮어쓰므로 일반 카드 재생성에는 위 명령을 사용한다.

## 재생과 조작

두 프로젝트의 `Assets/Shared/BgmPlayer.cs`는 바이트가 같은 공용 구현이다. 앱에 음악용 AudioSource를 한 번만 붙이며 2D·loop·고정 피치로 재생한다. `Attach`를 같은 앱에 반복 호출해도 소스를 더 만들지 않는다. 곡은 타이틀/플레이/설정/서사 회귀 사이에 유지되며 진행 저장 데이터의 일부가 아니다. 대화나 회귀가 취소하는 일회성 효과음과 구분한다.

브라우저에서는 실제 첫 포인터/키 입력 이후 `NotifyUserGesture()`로 시작한다. Unity 6000.3 WebGL의 기존 사용자 이벤트 기반 AudioContext 재개 처리를 사용하며, 브라우저 자동재생 정책을 우회하지 않는다. 시작은 0.45초 fade-in이다. 로딩 안내에도 첫 화면을 누르면 음악이 시작됨을 표시한다.

- 카드는 타이틀/전투의 **음악**에서 음소거와 음악 음량을 조절한다. `FC.music.volume`·`FC.music.muted`를 보존한다.
- 서사는 설정의 **배경 음악 켜기/끄기**와 기존 전체 음량을 사용한다. `POD.music.volume`·`POD.music.muted`와 `POD.volume`은 서로 다른 설정이다. 음악 기본 gain은 0.65이고 전체 음량을 추가로 따른다. 작은 환경음은 음악을 가리지 않게 0.03으로 낮췄다.
- 음악 음소거는 소스의 출력만 끄고 시간은 계속 진행한다. 음악을 켜도 곡이 매번 처음부터 시작하지 않는다. 저장/회귀의 스키마·콘텐츠 ID는 변경하지 않았다.

## 검증 범위

`Tools/Audio/verification.json`은 실제 플레이어용 Ogg를 다시 디코딩해 확인한 결과다. 두 곡 모두 유효한 44.1kHz stereo Vorbis이며 90–120초 범위, 클리핑 샘플 0, 100ms 단위 무음 구간 없음, 유효한 시작/끝 신호, 낮은 경계 샘플 변화량을 통과했다. 카드의 디코딩 peak/RMS는 −5.666/−21.394dBFS, 서사는 −5.879/−20.437dBFS다. 경계 샘플 변화량은 0.001485/0.001353이다. 생성기·렌더러·MIDI·JSON 악보 해시도 검증한다. 이 수치는 청취자의 최종 음악 취향이나 모든 기기의 무결점 루프를 증명하지 않는다.

현재 카드곡의 20초 preview는 `/tmp/firstgame-classic-music/recollections-of-starlight-preview.ogg`에 만든다. 이전 곡의 15초 preview는 `/tmp/firstgame-music/*-preview.ogg` 제작 이력이다. 실제 사람이 듣고 승인했다고 기록하지 않는다. 실제 WebAudio의 재생·루프 설정·음소거·음량·페이지 재로드 검사는 [VisualValidation.md](VisualValidation.md)와 해당 최신 브라우저 보고서를 기준으로 하며, 원본 파일의 파형 검사를 브라우저 재생 통과로 대신하지 않는다. 실제 스피커/이어폰·휴대폰 음량 균형은 별도 청취 범위다.
