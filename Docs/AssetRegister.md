# 자산 출처와 교체 장부

## 2026-10-23 카드 체험판·보행·음성 개정

카드 40종은 독자적인 만화 선화·셀 채색 원화로 만든 2×2 아틀라스 10장에 연결한다. 기존 네 아틀라스의 이름·첫 16개 ID 순서는 보존하고 `cards-05-sanctuaries`부터 `cards-10-armaments`까지 확장한다. 리더 리리아·카엘·세린·노아는 별도 투명 초상 `portrait-{id}`를 사용하며 타이틀도 네 리더의 디자인에 맞춘다. 두 게임의 타이틀·서사 초상·전장 배경과 새 `galilee-painted-sky`를 포함한 실제 이미지 목록·출처·해상도·슬롯은 [일러스트](Illustration.md)와 해당 생성 매니페스트가 기준이다. 기존 게임 캐릭터·의상·원화를 복제하거나 이전 반실사 그림에 필터만 적용한 자산으로 대체하지 않는다.

새벽의 길 3D는 `Tools/Art`의 ToonDawn 생성 경로로 얼굴·의복·머리카락·소품과 환경을 개선한다. 세 인물은 정강이·발을 포함한 14본 Generic 리그와 Idle/Walk를 갖고, Unity의 `Speed` BlendTree가 실제 이동에 맞춰 동작을 전환한다. Walk는 제자리 클립이고 월드 이동은 컨트롤러가 처리한다. 기존 `*Proxy.fbx` 파일명과 meta GUID는 연결 보존을 위해 유지한다. 원본 `.blend`·FBX·보행 프리뷰·매니페스트와 재가져오기는 [ToonDawn 제작 계약](../Tools/Art/ToonDawn.md)을 따르며, 아래 초기 기록을 새 모델의 수치로 인용하지 않는다.

두 게임의 오리지널 BGM은 MIDI 작곡과 MIT 라이선스 FluidR3 악기 샘플을 사용해 제작했다. 약 103초·104초의 stereo Ogg이며 [음악 제작 기록](Audio.md), `Tools/Audio/MUSIC-NOTICE.txt`, 생성/검증 JSON을 따른다. 샘플 bank 전체를 게임에 넣지 않으며 공개 웹과 실행파일에 음악 출처·라이선스를 함께 배포한다. 실제 재생 검증 범위는 최신 검증 기록을 따른다.

초반 서사 음성은 공식 MeloTTS의 일반 한국어 KR 음성으로 제작한 20개 Ogg(도입 9줄·호숫가 11줄)와 정확한 문장 매칭용 `Resources/Voice/opening-voice.json`이다. 단일 일반 음성이며 실존 배우의 목소리를 복제하지 않는다. 원문 Ink는 변경하지 않으며 발화에서만 화자 접두사를 뺀다. 제작 도구·모델 리비전·MIT/관련 모델 라이선스와 생성·문장·파일 검증은 [음성 제작 기록](Voice.md) 및 `Tools/Voice`를 따른다. 모델 가중치나 실행 중 생성 서비스는 Player에 포함하지 않는다.

이 장부는 제작 범위를 설명한다. 최신 Unity 가져오기·실제 화면·오디오 출력·공개 배포의 통과 여부는 [검증 기록](Validation.md)을 따른다. 파일 분석·Blender 프리뷰·이전 공개판 검사를 새 게임 실행 증거로 대신하지 않는다.

## 최초 프록시 제작 이력

아래 표는 최초 프록시 제작 이력이다. 최신 PNG와 모델 수치로 해석하지 않는다. 기존 승인 원화·모델·ZIP·웹 시제품은 제공되지 않았다. 기존 자료를 임의로 삭제하거나 가져오지 않았다. 아래 3D 자산은 이 저장소를 위해 Blender 4.3.2 Python API로 직접 만든 오리지널 임시 프록시다. 외부 메시·텍스처를 다운로드하거나 혼합하지 않았다. 정확한 재질 이름, 파일 크기, 메시·정점·삼각형 수는 `Tools/Art/AssetManifest.json`에서 확인한다.

| 자산 | 위치 | 상태 / 출처 | 크기·교체 계약 |
|---|---|---|---|
| 갈릴리 물가/석조 공간 | `Games/PathOfDawn/Assets/Art/Generated/GalileeShore.fbx` | 임시, 저장소용 절차 생성 | 약 24×19m. Unity 원점의 지면 높이 약 0m. 실제 Unity FBX 원본은 석조 공간 x=+4,z=+3, 출입구 +Z, 물가 x<−4.7. 런타임에서 환경만 Y축 180° 회전하여 석조 공간 x=−4,z=−3, 출입구 −Z, 물가 x>4.7로 정규화한다. 충돌체는 이 최종 좌표에 별도 배치. |
| 예수님 프록시 | `Games/PathOfDawn/Assets/Art/Generated/JesusProxy.fbx` | 임시, 저장소용 절차 생성 | 약 1.8m, 발 기준 원점, Unity +Z 전면. Generic 리그와 `JesusProxy_BasicIdle`. |
| 여행자 프록시 | `Games/PathOfDawn/Assets/Art/Generated/TravelerProxy.fbx` | 임시, 저장소용 절차 생성 | 약 1.75m, 동일 축/피벗 계약. Generic 리그와 `TravelerProxy_BasicIdle`. |
| 미리암 프록시 | `Games/PathOfDawn/Assets/Art/Generated/MiriamProxy.fbx` | 임시, 저장소용 절차 생성 | 약 1.7m, 동일 축/피벗 계약. Generic 리그와 `MiriamProxy_BasicIdle`. |
| 별빛 전장 | `Games/FantasyCards/Assets/Art/Generated/StarfallArena.fbx` | 임시, 저장소용 절차 생성 | 직경 16.4m. 표면 y≈0.15m, UI/슬롯은 y≥0.25m. 외곽 8개 기둥. 게임의 슬롯 수나 규칙과 독립. |
| Blender 편집 소스 | `Tools/Art/Source/FirstSliceProxies.blend` | 위 5종의 원본 장면 | Unity 프로젝트 외부 보관. FBX를 Unity에 가져온다. |
| Blender 미리보기 | `Tools/Art/Previews/*_BlenderPreview.png` | 실제 Blender Cycles 렌더 | 원화 승인·Unity 실행 증거 아님. |
| 초기 카드 원화 아틀라스 | `Games/FantasyCards/Assets/Art/Archive/card-atlas.png` | 이미지 생성 도구로 만든 구 4×4 원본, Resources 밖에 보존 | 현재 40종 원화에 사용하지 않는다. 현재 출처·슬롯·교체는 `Docs/Illustration.md`가 기준. |
| 한글 글꼴 | 각 프로젝트 `Assets/Resources/Fonts` | 별도 설치 단계에서 원본/라이선스 포함 | 실제 파일명과 라이선스·출처는 프로젝트 글꼴 폴더와 `Docs/Development.md`를 참조. 본 장부는 미검증 글꼴을 승인하지 않음. |

## 초기 프록시 재현 이력

아래는 첫 프록시의 역사적 생성 명령이다. **현재 체크아웃에서 실행하면 새 모델을 구형으로 덮어쓸 수 있다.** 현재 모델의 재생성과 검증은 `Tools/Art/ToonDawn.md`의 `generate_toon_dawn.py` 경로를 사용한다. 초기 결과가 필요하면 별도의 보존 복사본에서만 아래 명령을 실행한다.

```sh
blender --version
blender --background --python Tools/Art/generate_proxies.py -- --root . --render
blender --background --python Tools/Art/verify_proxies.py -- --root .
```

`--render`를 빼면 FBX·소스·매니페스트만 재생성한다. 모든 임시 메시와 재질을 스크립트로 다시 만들며, 기존 `.meta`는 덮어쓰지 않는다. 메시 생성 난수는 `10719`로 고정했다. Blender가 저장하는 타임스탬프 등의 이유로 바이너리 바이트가 매번 같다는 보장은 하지 않는다. 에셋을 수동 편집하여 승인 후보로 바꾸기 전, 다른 경로/이름으로 복사하고 생성기를 실행해도 덮어써지지 않게 한다.

생성 로그는 `Tools/Art/generation-log.txt`, FBX 재가져오기 검증은 `Tools/Art/verification.json`에 기록한다. Blender 설치의 OpenImageDenoise 미포함으로 최초 미리보기 렌더가 실패했으며, denoising만 끄고 Cycles CPU에서 재실행한다. 패키지 서명·TLS 등 설치 검증을 우회하지 않는다.

## Unity 가져오기와 교체

- FBX 형식, 미터 단위, `forward=-Z, up=Y`로 내보낸다. Blender의 -Y 전면이 Unity +Z로 변환되며 Unity의 좌표계 변환으로 Blender X와 원본 FBX의 Unity X 부호가 반대인 것을 실제 네이티브 prefab 좌표에서 확인했다. 초기 Blender 재가져오기 검증만으로 Unity 좌표를 확정했던 설명을 정정한다.
- `GalileeShore`만 런타임 Y축 180° 회전하여 물가를 +X, 석조 공간을 (−4,−3)으로 맞춘다. 음수 스케일로 미러링하지 않는다. 최종 석조 공간 z 범위는 대략 −4.7…−1.3이며 출입구는 z≈−4.7이다. 등장인물 모델은 이 환경 회전의 자식이 아니며 개별 배치한다.
- 모델의 `POD_*` / `FC_*` 재질 이름과 기본색을 유지하고 모든 MeshRenderer/SkinnedMeshRenderer 슬롯에 재질을 연결한다. 새벽의 길 인물은 `PathOfDawn/Cel Lit`, 환경의 물과 하늘은 전용 셰이더를 사용한다. 구 초기 URP/Lit 설정으로 덮어쓰지 않는다.
- 현재 인물 리그는 Root/Pelvis/Spine/Head와 좌우 UpperArm/Forearm/Leg/Shin/Foot, 총 14본 Generic이다. 단일 SkinnedMeshRenderer에 Idle/Walk를 적용한다. 최종 Humanoid 리타게팅을 보장하지 않으며 대화 시선 IK·입모양·손가락·세밀한 발 접지는 후속 작업이다.
- 천과 머리카락은 단순 스킨 또는 단색 메시다. 실제 천 시뮬레이션, 헤어카드, 피부 셰이더가 아니다.
- 환경에는 카메라·광원·충돌체·오디오를 내보내지 않는다. Unity의 카메라, 광원, 상호작용 앵커, 충돌 영역은 별도 책임이다.
- 게임 로직의 안정 ID/컴포넌트는 부모에 두고 시각 자식 프리팹만 바꾼다. 생성된 메시 이름을 저장 데이터의 콘텐츠 ID로 사용하지 않는다.

## 남은 제작

최종 원화 검토, 캐릭터 턴어라운드와 역사/문화 검수, 얼굴·손 토폴로지, UV·텍스처와 스키닝의 세밀한 조정, 보행의 지면 접촉·발 미끄러짐 보정, 자연스러운 대화 반응·립싱크, 머리카락과 천, 환경 LOD·충돌체·광원 베이크, 전투·환경 효과음, 음성의 사람 청취 및 실제 PC/웹·휴대폰 실측. Idle/Walk·BGM·초반 음성의 존재를 이러한 최종 품질 작업의 완료로 해석하지 않는다.
