# 카드게임의 말소리 제거와 로컬 배경 음악

이 문서는 `modern-card-audio-2026-10-26` 판의 제작 이력이다. 최신 사용자는 원작 음악을 요청했고, 현재 파일과 출처·편집 범위는 [원작 음악 재조사](Research/FmFidelityMusic.md)를 따른다. 아래 창작곡은 이력으로 보존하며 현재 원작곡이라고 표시하지 않는다.

현재 개정은 `modern-card-audio-2026-10-26`이다. 정상 플레이에서 외부 음악 플레이어를 제거하고, 게임에 포함된 **가사·말소리 없는 독자적 연주곡 네 곡**을 사용한다. 이전 SOUND-I 공식 SoundCloud 페이지는 사용자가 직접 여는 새 탭 링크로만 남긴다. 새 곡을 판타지마스터즈 원곡이라고 소개하지 않는다.

이전 웹판은 SoundCloud iframe 네 개와 공식 SDK를 사용했고, 화면 전환을 위해 다른 곡도 음소거 상태로 재생 준비했다. 게임 소스에 사용자가 신고한 “Welcome … Sound …” 대사는 없으며, 외부 스트림의 인트로·안내·광고를 게임이 제어할 수 없었다. 신고된 음성 문장을 직접 녹음해 판별하지 않았으므로 광고인지 곡의 인트로인지 단정하지 않는다. 이번 변경은 그 외부 재생 경로 자체를 없애 두 경우 모두 정상 플레이에 들어오지 못하게 한다.

| 화면 | 연주곡 | 길이 | 편성 |
| --- | --- | --- | --- |
| 타이틀 | 별문이 열릴 때 | 73.85초 | 호른·현악·하프·팀파니 |
| 대기실 | 여행자의 정원 | 81.70초 | 플루트·현악·하프·피아노 |
| 덱 | 빛을 엮는 서고 | 85.33초 | 첼레스타·피아노·피치카토 |
| 전투 | 별의 결투 | 62.95초 | 브라스·현악·팀파니·절제된 타악 |

악보는 `Tools/Audio/ModernCards/Scores`의 MIDI와 JSON, 생성기는 `Tools/Audio/compose-modern-cards.py`다. FluidR3 GM의 MIT 악기 샘플을 오프라인 FluidSynth로 연주했다. 보컬·합창·음성·효과음 GM 프로그램은 사용하지 않는다. 기존 두 게임의 음원과 제작 기록은 보존했다. 표기한 길이는 실제 Ogg 디코딩 길이다.

웹은 `StreamingAssets/Audio/ModernCard`의 같은 출처 Ogg를 사용하고, Vorbis 미지원 브라우저에는 포함된 MP3를 사용한다. 첫 실제 클릭·터치·키 입력으로 단일 WebAudio 그래프를 생성하고 재생을 허용한다. 시작 로딩 화면과 이어하기 버튼 등 페이지 전체 입력이 대상이다. 선택되지 않은 곡을 미리 재생하지 않으며, 현재 곡은 다음 곡의 실제 재생이 확인될 때까지 유지한다. 화면을 다시 그려도 같은 곡은 재시작하지 않는다. 음소거·음량은 기존 설정 키를 유지하고 게임 설정과 하단 버튼이 공유한다.

Windows는 같은 원본 Ogg를 Unity AudioSource로 재생하고 0.35초의 볼륨 전환을 사용한다. 저장 내용과 카드 규칙에는 영향을 주지 않는다. 웹의 `FirstGameLocalMusic.getState/getDiagnostics`는 실제 미디어 요소의 경로·시간·일시정지 상태와 AudioContext 상태만 읽는다. 게임 진행이나 RNG를 조작하는 인터페이스가 아니다.

검증은 `node --test Tools/Validation/test-fm-local-music.cjs`의 10개 수명주기 회귀 검사와 `Tools/Audio/ModernCards/manifest.json`의 실제 디코딩 수치로 구분한다. 네 Ogg 모두 clipping 0이고 100ms 최소 RMS는 약 −26.3~−27.4dBFS이며, 배포 사본의 SHA-256이 Unity 원본과 일치한다. 이 기록은 브라우저 PCM·최종 빌드 재생·사람의 스피커 청취 검수나 사용자 음악 취향 승인을 대신하지 않는다. 최종 브라우저 결과는 별도 최신 실행 보고서에 기록한다.

```sh
python3 Tools/Audio/compose-modern-cards.py
node --test Tools/Validation/test-fm-local-music.cjs
```
