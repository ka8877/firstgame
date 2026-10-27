# 판타지마스터즈 원작 음악 재조사·적용

확인일: 2026-10-27 UTC. 현재 개정 `fm-fidelity-audio-2026-10-27`.

이전 판의 창작곡 네 곡은 원작이 아니었다. 사용자가 원작 로비·전투 음악을 요청해 이번에는 실제 제작자가 판타지마스터즈용으로 공개한 녹음을 확인하고 적용한다. [SOUND-I 공식 포트폴리오](https://soundcloud.com/sound-i2/sets/eomgh4mkqbec)에서 Login/Lobby/Deck/Game1/Game2 등의 원래 제목을 확인했다. [S.I.D-Sound의 전투곡](https://soundcloud.com/sid_sound/fantasy-masters-battle-bgm)은 설명에 `Online TCG game, Fantasy Masters battle BGM`, `Composer - Ye eun-su (S.I.D-Sound)`를 명시한다.

## 반복 음성의 실제 근거

SOUND-I 공개 로비 MP3 원본 SHA-256은 `b178fb80ea4fef730d0cee1a180b15336beae7c147cec172076703a500b61460`이다. 원본을 변경하지 않고 ffmpeg로 디코딩한 뒤 로컬 Whisper-small을 실행하자 `Welcome to Sound Interactive!`가 검출됐다. 8초 구간 분석에서는 약 4.58–6.66초, 24–26.42초, 34.56–36.62초, 44.14–46.06초, 64–66.66초 등에 제작사 ident 계열 말소리가 검출됐다. 녹음 자체에 이 문구가 있는 근거이며 외부 광고로 단정하지 않는다. 자동 인식 결과는 `Tools/Audio/FmFidelity/Research`에 원본 해시와 함께 남겼다. 이것은 물리 스피커 청취 검수와 다르다.

이 공개 녹음을 다시 외부 플레이어로 연결하면 이전 신고를 반복할 수 있다. 그래서 게임의 네 고정 오디오 요소·설정·첫 입력·볼륨·음소거·장면 전환은 유지하고, 로컬 파일을 실제 원작 녹음에 근거한 아래 편집본으로 바꾼다.

| 장면 | 실제 원곡/제작자 | 런타임 길이 | 적용 방식 |
| --- | --- | ---: | --- |
| 로그인 | `2 BGM Login` / SOUND-I | 18.530초 | 말소리 구간을 포함하지 않는 네 연주 구간 편집 |
| 로비 | `3 BGM Lobby` / SOUND-I | 30.000초 | 같은 원곡의 다섯 연주 구간을 음악적 박자 경계에서 연결 |
| 덱 편집 | `1 BGM Deck` / SOUND-I | 19.551초 | 같은 원곡의 네 연주 구간 편집 |
| 전투 | `Fantasy Masters - battle BGM` / S.I.D-Sound, Ye eun-su | 60.421초 | 공개 제공 파일의 전체 디코딩 음원 |

로비의 정확한 원본 구간은 17.27565–23.28961, 27.79429–33.78503, 37.54667–43.53741, 47.29905–53.28980, 57.42295–63.43692초다. 연결에는 다음 구간 앞의 40ms를 사용해 짧게 교차 전환하며 각 음악 구간의 길이는 유지한다. 원본 전체를 복원하거나 원래 말소리가 없던 파일을 확보했다고 표시하지 않는다. 전투 원본의 60.479초 메타데이터에는 MP3 padding이 있고 디코딩 결과는 60.421초다. 어느 패치 시점의 정확한 전장 배정인지까지 확정하지 않는다.

모든 곡은 −19 LUFS 목표/−1.8dB true-peak 목표로 정규화한 뒤 Ogg와 MP3로 변환했다. 디코딩한 네 Ogg 모두 clipping 0이며 최종 파일의 Whisper-small/VAD 분석에서 말소리 문장은 검출되지 않았다. 이는 자동 검출의 범위이며 인간의 미적 평가나 실물 스피커 검수가 아니다. 최종 브라우저의 실제 재생·음량·음소거는 이번 개정의 별도 실행 보고서에서 확인한다.

## 재현·원본 보존

- 원본 여덟 MP3, 제목·제작자·공개 페이지·바이트·SHA-256은 `Tools/Audio/FmFidelity/Originals`와 `source-files.json`에 있다. 공개 재생 경로를 정상 접근했으며 인증·DRM을 우회하지 않았다.
- 편집 구간·방법은 `loop-spec.json`, 실제 네이티브/웹 음원 수치·해시는 `runtime-manifest.json`, 자동 음성 검출 결과는 `runtime-speech-screen.json`에 있다.
- `python3 Tools/Audio/FmFidelity/generate-loops.py`는 원본 해시를 확인하고 같은 입력·명시된 편집/정규화·코덱으로 파일을 다시 생성한다. ffmpeg 코덱 버전이 다르면 인코딩 바이트가 달라질 수 있다.
- 이전 창작곡의 MIDI/JSON과 저작권 기록은 보존했고 당시 런타임 파일도 `Tools/Audio/ModernCards/History/before-fm-fidelity-2026-10-27`에 남겼다.

## 추가 조사와 확인하지 못한 범위

[2012-06-21 대격변 업데이트 기사](https://www.inven.co.kr/webzine/news/?news=45447)는 게임 BGM 변경을 명시하므로 모든 시기의 음악을 하나로 단정하지 않는다. 2003년 공식 보존 `fmtest.exe`의 정적 문자열에는 `resource/sound/bgm-login.mp3`, `bgm-lobby.mp3`, `bgm-deck.mp3`, `bgm-battle.mp3`, `bgm-ready.mp3`, `bgm-result.mp3`가 있지만 실행 파일 자체에 그 음악은 들어 있지 않았다. 자세한 정적 근거는 `Tools/Research/FmFidelity/archived-launcher-inspection.json`이다.

Wayback의 실제 `resource/sound` 및 `AutoPatch/resource/sound` prefix와 도메인 내 `bgm*.mp3` 파일명 필터에서는 보존 음원 응답을 확보하지 못했다. Internet Archive 소프트웨어 제목 검색에서도 확인 가능한 전체 클라이언트를 찾지 못했다. 팬마의 BGM 목록은 정상 비로그인 요청에 401이어서 우회하지 않았다. 이 조사 결과를 어디에도 원본이 없다는 주장으로 확대하지 않는다.

원작 저작권은 원 권리자에게 있으며 공개 재생·서비스 종료·개인 이용이 새로운 재배포 허가를 뜻하지 않는다. 사용자 요청에 따라 원작 자료를 활용하고 출처·변환·미확인 권리 범위를 기록한다. 새 창작곡이나 공식 서비스·전체 OST 복구로 소개하지 않는다.
