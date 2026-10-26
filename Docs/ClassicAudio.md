# 개인용 카드 재현판의 음악

2026-10-26 개정 `fm-classic-2026-10-26`. **별빛의 회상 / Recollections of Starlight**는 차분한 2000년대 판타지 카드게임 분위기를 위한 독자곡이다. 판타지마스터즈의 원곡·MIDI·녹음을 가져오거나 채보한 곡이 아니다.

88 BPM, 4/4박자 40마디, 109.090907초의 E단조 루프다. 플루트와 오보에가 같은 8마디 주제를 교대하고, 현악·첼로·하프·피아노가 받친다. 중간 8마디는 관계장조로 전환하고 후반에는 작은 합창 패드와 팀파니를 더한 뒤 첫 분위기로 돌아온다. 첼레스타는 몇 마디의 짧은 응답에만 사용한다. 이전 112 BPM 호른 중심 곡보다 느린 박자와 여백을 늘렸으며, 효과음·카드 설명 읽기를 위한 배경 음악으로 구성했다.

## 재현과 보존

```sh
python3 Tools/Audio/compose-classic.py
python3 Tools/Audio/verify-soundtracks.py
```

카드 전용 생성기는 기존 `compose-soundtracks.py`의 FluidSynth 렌더 함수를 사용하지만, 기존의 두 곡 생성 진입점을 실행하지 않는다. `Scores/starlight-overture.mid`와 `.json`에 고정 시드 20261026의 876개 음표를 기록한다. FluidR3 GM 샘플은 기존 [MIT 고지](../Tools/Audio/MUSIC-NOTICE.txt)를 따르며 SoundFont 전체는 게임에 포함하지 않는다. 정규화는 55Hz high-pass와 −19 LUFS 목표, −1.5dB true-peak ceiling을 사용한다. 두 번째 연주 주기를 추출해 잔향을 이어받고 마지막 B7 화음에서 첫 E단조로 돌아간다. 앞뒤에 무음을 삽입하지 않고 3ms 경계 램프를 둔다.

원래 카드곡은 `Tools/Audio/history/2026-10-26-before-classic`의 manifest와 함께 음원·MIDI·JSON 악보·제작 도구·검증·고지를 보존했다. 새 런타임 음원은 기존 `Games/FantasyCards/Assets/Resources/Audio/starlight-overture.ogg` 경로와 GUID를 그대로 쓴다. 공용 `BgmPlayer`, 첫 사용자 입력 후 재생, 음소거·음량 설정과 소스 정체성은 변경하지 않았다.

PathOfDawn 음원 SHA-256은 `d1e9b23fbf8b1acace95f183bbda898376d07fdf3c5c21e632253ee0703d1b73`로 이전과 같다. 카드 교체 전후의 meta 바이트도 일치한다.

## 결과와 범위

- 파일: 44.1kHz stereo Vorbis, 1,990,779 bytes.
- SHA-256: `6153a23211c3e9d321dc2316d24cf59af1ca3cac88aaa7be0380513aac1945cd`.
- 실제 Ogg 디코딩: peak −5.666dBFS, RMS −21.394dBFS, clipping 0 samples.
- 100ms 창의 최소 RMS −27.813dBFS, 앞/뒤 50ms RMS −25.284/−28.620dBFS.
- 루프 경계 샘플 차이 0.00148494; 생성기·렌더러·MIDI·JSON 악보 해시 일치.

현재 측정 근거는 [production.json](../Tools/Audio/production.json), [verification.json](../Tools/Audio/verification.json), [classic-verification.json](../Tools/Audio/classic-verification.json)이다. 20초 미리 듣기는 `/tmp/firstgame-classic-music/recollections-of-starlight-preview.ogg`에 있다. 위 결과는 디코딩·신호 측정이며 원곡과의 청취 비교, 사람의 음악 승인, 실제 스피커/휴대폰 음량 검수 또는 새 플레이어의 브라우저 재생 통과를 대신하지 않는다.
