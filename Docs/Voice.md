# 새벽의 길 — 초반 한국어 음성

체험판의 **도입 9줄과 첫 호숫가 선택 전 11줄**, 총 20줄에 한국어 AI 낭독을 추가한다. 원문의 내레이션과 미리암·여행자의 첫 대사를 포함하며 한 명의 기본 한국어 화자가 읽는다. 인물별 성우 연기나 특정 성우의 목소리 복제는 아니다. 음높이를 인위적으로 바꿔 여러 성우로 표시하지 않는다. 플레이 중 외부 음성 API, 로그인, 추가 다운로드는 필요하지 않다.

음성은 기존 `galilee.ink` 문장을 그대로 읽으며 화면에 붙는 `미리암:`·`당신:` 화자 표지만 음원에서 제외한다. `Resources/Voice/opening-voice.json`에는 화면 전체 문구, 발화 문구, 안정적인 line ID, text SHA-256, Resources 경로와 길이가 있다. 현재 문구와 정확히 일치할 때만 재생하므로 이야기 원문을 바꾸고 음원을 갱신하지 않으면 다른 문장을 읽지 않는다. 대사 자동 진행이나 Ink 변수 변경은 음성 재생의 책임이 아니다.

## 제작과 재현

공식 **MeloTTS Korean**의 기본 `KR` 화자 0을 CPU에서 오프라인 합성했다. 코드와 모델의 공식 문서는 MIT 및 상업·비상업 이용 허용을 명시한다. 특정 인물의 녹음을 입력하거나 목소리를 학습하지 않았다. 모델 가중치·형태소 사전·추론 라이브러리는 개발 도구이며 플레이어에는 완성 Ogg와 manifest만 포함한다.

- MeloTTS 코드: `209145371cff8fc3bd60d7be902ea69cbdb7965a`.
- Korean 모델: `0207e5adfc90129a51b6b03d89be6d84360ed323`, MIT.
- 한국어 언어 특징: `kykim/bert-kor-base`의 고정 revision `1779cc0982ada0216dd6de0dd4e86fb78201926d`. 모델 카드가 연결하는 저자 LMkor 저장소는 Apache-2.0이며, 모델 파일은 배포하지 않는다.
- 합성 설정: speed 1.0, sdp ratio 0.2, noise 0.6/0.8, seed 90210 + 문장 인덱스. 같은 한국어 화자를 전 구간에 사용한다.
- 최종 음원: 44.1kHz mono Vorbis Ogg, quality 5. FFmpeg loudnorm 목표 −18 LUFS·true peak −2dB. 측정값과 정규화 목표를 구분한다.

출처·원문 라이선스는 `Tools/Voice/Licenses`, URL과 SHA는 `license-sources.json`, 개발 모델 파일의 크기·SHA는 `models.json`에 기록했다. 배포용 고지는 `Tools/Voice/VOICE-NOTICE.txt`다. 각 최종 Ogg와 원본 WAV의 SHA 및 정확한 문구는 `production.json`을 따른다. 원본 WAV는 `Artifacts/Voice/Raw`에 보존한다.

```sh
python3 -m venv /workspace/tools/voice-venv
/workspace/tools/voice-venv/bin/python -m pip install --no-cache-dir torch==2.5.1 torchaudio==2.5.1 --index-url https://download.pytorch.org/whl/cpu
/workspace/tools/voice-venv/bin/python -m pip install --no-cache-dir -r Tools/Voice/requirements.txt
python3 Tools/Voice/prepare-runtime.py
/workspace/tools/voice-venv/bin/python Tools/Voice/generate-opening-voice.py
/workspace/tools/voice-venv/bin/python Tools/Voice/verify-opening-voice.py --skip-asr
```

Python 3.12에서 실행했다. 상위 MeloTTS의 Transformers 4.27.4는 이 Python용 tokenizer wheel이 없어 빌드에 실패했고, 4.44.2와 동일한 고정 모델로 추론했다. `prepare-runtime.py`는 MeloTTS 작업 복사본의 한국어 전처리/BERT만 가져오도록 import 네 곳을 제한하고 로컬 BERT 경로를 사용하며, TTS checkpoint는 `weights_only=True`로 읽는다. 추론 네트워크·한국어 G2P·모델 파라미터는 변경하지 않는다. 예상하지 못한 로컬 소스 변경은 덮어쓰지 않는다. NLTK 사전은 공식 고정 해시 파일을 먼저 설치하며 네트워크 보호 옵션을 끄지 않는다. 전체 실제 Python 패키지는 `environment-freeze.txt`에 기록한다.

## 검증된 범위

`Tools/Voice/verification.json`에서 20개 전부 원문·hash·길이·sample rate·mono·유효 신호·클리핑 검사에 통과했다. 파일 합계는 **1,543,350 bytes**, 실제 디코딩 PCM 합계는 **168.231초**다. Ogg 컨테이너 길이 합계는 168.973초이며 파일별 디코딩 길이와 최대 0.096초 차이가 있어 따로 기록한다. 20개 모두 클리핑 샘플 0이며 개별 디코딩 길이는 약 3.37–12.67초, sample peak는 −5.97~−2.21dBFS, RMS는 −19.15~−17.70dBFS다.

별도 **Whisper small** 모델로 최종 Ogg를 다시 한국어로 전사했다. 기대 문구나 initial prompt를 넣지 않았고, Unicode NFC 정규화 뒤 공백·문장부호만 제외한 문자 편집 거리를 비교했다. 전체 문자 오류율 **4.073%**, 개별 최대 **11.63%**로 사전에 설정한 전체 10% 이하·각 문장 20% 이하 기준을 통과했다. `호숫가→호수카`, `길을→비를` 같은 ASR 오독도 원문 그대로 기록하며 철자 보정을 하지 않았다. 이는 문장 식별에 대한 보조 근거이며, 오독을 모두 합성 발음 오류나 모두 ASR 오류로 단정하지 않는다. 실제 사람이 듣고 자연스러운 연기를 승인했다는 뜻은 아니다.

```sh
/workspace/tools/voice-venv/bin/python -m pip install --no-cache-dir -r Tools/Voice/asr-requirements.txt
python3 Tools/Voice/prepare-runtime.py --with-asr
/workspace/tools/voice-venv/bin/python Tools/Voice/verify-opening-voice.py
```

독립 ASR도 모델 revision·SHA를 `asr-model.json`에 고정한다. CTranslate2 4.4.0의 로더 호환 문제는 4.6.0 wheel로 해결했다. FFmpeg로 16kHz PCM을 디코딩해 ASR에 전달하여 PyAV 버전별 파일 로더 차이에 의존하지 않는다. 음원 검사는 Unity나 Web의 재생 통과를 대신하지 않으며, 실제 런타임 결과는 [검증 기록](Validation.md)과 [브라우저 검사](VisualValidation.md)를 따른다.

## 재생 컴포넌트

`DialogueVoicePlayer.Attach(owner)`는 앱에 전용 2D·non-loop AudioSource 하나를 추가한다. BGM 소스와 별도로 관리하며 전체 음량은 기존 `AudioListener.volume`을 따른다. 현재 line ID, clip, 지원 문장 수와 재생 상태를 검사할 수 있다.

- `PlayLine(fullLine)`은 이전 소리를 중단한 뒤 정확히 일치하는 음원을 재생한다. 없는 문구는 이전 참조도 지워 다른 대사를 잘못 반복하지 않는다.
- `Stop()`은 출력만 멈추고 현재 문장을 남겨 다시 듣기를 허용한다. `Clear()`는 출력과 현재 문장을 모두 없앤다.
- `Replay()`는 현재 음원을 처음부터 재생한다. clip이 로드 중이면 완료를 기다린다. 음소거 상태나 현재 음원이 없을 때는 재생하지 않는다.
- 음소거·음량은 진행 저장과 별개인 `POD.voice.muted`, `POD.voice.volume`에 보존한다. 음소거 해제만으로 이미 끝난 문장을 갑자기 다시 읽지 않는다.
- 앱은 대사 교체·회귀·타이틀 이동에서 음성을 정리하고, 음성 중 BGM의 실제 출력만 잠시 낮춘다. 사용자가 지정한 음악 음량 값을 바꾸지 않는다. Web은 기존 실제 입력 뒤 활성화된 Unity AudioContext를 사용한다.

초반 이후 문장은 기존 자막으로 계속 진행한다. 전 구간 성우 제작, 인물별 연기, 입 모양과 발화 동기화, 실제 기기 청취 평가는 후속 제작 범위다.
