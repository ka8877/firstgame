# 자산 출처와 교체 장부

## 2026-10-27 타이틀·백필드·자연스러운 인물 개정

현재 개정은 `fm-detail-2026-10-27`과 `gospel-natural-2026-10-27`이며 두 게임을 모두 변경한다. 기존178종 원작 카드·픽셀·GUID를 보존하고 원본 데이터와 인쇄 그림으로 검증한72종을 더해250종·수록9등급을 사용한다. 새72종은 지형14·유닛44·아이템14이며 레전드 `제압의길`을 포함한다. 원본·변환·전체 필드 감사는 `Tools/Data/Detail`, `Tools/Research/FmDetail`, `Docs/Research/FmDetailCards.md`를 따른다. 새 타이틀의 성채·숲 파노라마는 이번 개정을 위한 생성 원화이며 원작의 HD 복원으로 소개하지 않는다. 원본·프롬프트·최적화·해시는 `Tools/Art/FmDetail`에 있다.

예수님 게임은 MakeHuman의 명시적CC0 인체 두상 데이터와 기존 Quaternius 골격·일곱 동작을 사용해 새로운 얼굴·머리·체형을 제작한다. 소년/성인/마리아를 구분하며 원본·라이선스·변형·직접 검토는 `Tools/Art/GospelNatural`과 `Docs/GospelNaturalActors.md`를 따른다. 성전은 기존 입체 공간·텍스처의 원본을 보존하고 기둥·아치·석재·수목·조명과 면 방향을 정돈한다. `Docs/GospelNaturalWorld.md`를 따른다. 인물의 미적 승인이나 역사적 얼굴 복원·상용 최종 품질을 주장하지 않는다. 아래의 '현재'는 각 이전 제작 시점의 기록이다.

## 2026-10-27 판타지마스터즈 원작 충실성 개정

현재 카드게임은 `fm-fidelity-2026-10-27`이다. 기존161종의 원본·픽셀·메타 GUID를 보존하고, 공개 원작 DB로 검증한17종을 추가해178종을 사용한다. 신규17장은 업그레이드3·유니크1·이벤트13이며 개별 인쇄 원본도 직접 대조했다. 원본 WebP와 픽셀을 보존한 Unity PNG, 출처·해시는 `Tools/Art/ClassicCards`, 생성·데이터 감사는 `Tools/Data/FmFidelity`, 원본 직접 검토는 `Tools/Research/FmFidelity/card-print-review.json`을 따른다. 원작12등급 중 실제 수록은8등급이다. 지원하지 않는 기술을 삭제해 나머지 등급을 채우지 않는다.

타이틀의 원작 웹 인물 조각과 카드 인쇄 그림은 보존한 공개 원작 자료이며 새로 생성한 HD 원화로 소개하지 않는다. 전장의 녹색 석재와 청동 테두리, 코인·베기·마법·피해·파괴 이펙트는 원작 자료와 실제 플레이 영상을 참고해 그린 새 런타임 UI다. 효과음은 별도 합성한 리마스터 효과음이며 원작 녹음 파일을 복구한 것으로 표시하지 않는다. `Docs/FmFidelityBattle.md`, `Tools/Audio/FmFidelitySfx/manifest.json`을 따른다.

음악은 SOUND-I의 원작 로그인·로비·덱 공개 데모에서 안내 음성이 없는 구간을 편집한 루프와 S.I.D-Sound의 공개 원작 전투곡이다. 공개 데모 원본에는 반복 안내가 있어 원본과 사용 발췌를 구분한다. 출처·원본·사용구간·변환·해시는 [음악 조사](Research/FmFidelityMusic.md)와 `Tools/Audio/FmFidelity`에 기록한다. 기존 창작곡도 제작 이력에 보존한다. 예수님 게임의 자산과 공개 바이트는 이번에 변경하지 않는다. 아래 이전 판의 '현재' 표기는 해당 제작 시점의 기록이다.

## 2026-10-26 판타지마스터즈 카드 개정

현재 카드96종과 뒷면1장은 공개 원작 카드 이미지다. `Tools/Art/ClassicCards/Originals`에 원본 WebP, `Resources/Art/ClassicCards`에 픽셀이 같은 Unity용 PNG를 보관한다. 출처·변환·해시는 [ClassicCardArt.md](ClassicCardArt.md)와 매니페스트를 따른다. 원본의 낮은 해상도를 고해상도 재제작으로 소개하지 않는다. 기존40종 atlas는 제작 이력으로 보존하며 네 창작 아바타와 배경은 현재에도 사용한다. 새 카드 BGM은 [ClassicAudio.md](ClassicAudio.md)의 독자적 오케스트라 곡이다. 아래 과거 카드 제작 기록과 현재 원작 이미지의 출처를 구분한다.


## 2026-10-24 새벽의 길 일러스트 2.5D 개정

사용자가 얼굴·인물·배경을 크게 개선하고 필요하면 3D 이외의 방법을 사용하도록 요청해 `painted-dawn-2026-10-24`를 제작한다. 현재 인게임은 새 `Resources/Art/Painted` PNG 일곱 개를 사용한다. `shore`·`room`은 인물 없는 1672×941 전체 장면, `jesus`·`miriam`·`traveler`는 1024×1536 투명 전신, `traveler-back`은 같은 크기의 양발을 바닥에 둔 뒷모습 정지 원화, `traveler-walk`는 1448×1086의 4×2 투명 보행 atlas다. 그림·크기·UV·가져오기 계약은 [PaintedDawn.md](PaintedDawn.md)에 모았다.

모두 `OpenAI image_gen.imagegen`으로 제작한 독자적 원화이며 생성 PNG의 바이트를 그대로 보존한다. 배경은 새로운 묘사로, 전신 인물은 이 프로젝트의 기존 초상을 참고해 생성했다. 원본·프롬프트·참조·SHA-256·파일 검수는 [배경 생성 기록](../Tools/Art/Painted/backgrounds-generation.json)과 [인물 생성 기록](../Tools/Art/Painted/characters-generation.json)을 따른다. 런타임 UV 선택과 가져오기 설정은 원본 그림의 필터 처리나 재합성이 아니다. 정확한 모델 버전·seed는 도구에서 제공하지 않았다.

기존 3D FBX·Blender 원본·ToonDawn 기록은 보존하며, 네 런타임 프리팹은 GUID를 유지해 `Assets/Art/Archive/LegacyModels`로 옮긴다. 현재 화면에는 이전 절차적 3D 인물·환경을 렌더링하지 않는다. 실제 이동·충돌·상호작용·Ink·저장·회귀·BGM·초반 음성은 유지한다. 아래 이전 자산 수치와 검증 이력은 삭제하거나 새 그림의 증거로 바꾸지 않는다. 새 Unity·브라우저·공개 배포의 실제 통과 범위는 [검증 기록](Validation.md)을 따른다.

## 2026-10-23 카드 체험판·보행·음성 개정

카드 40종은 독자적인 만화 선화·셀 채색 원화로 만든 2×2 아틀라스 10장에 연결한다. 기존 네 아틀라스의 이름·첫 16개 ID 순서는 보존하고 `cards-05-sanctuaries`부터 `cards-10-armaments`까지 확장한다. 리더 리리아·카엘·세린·노아는 별도 투명 초상 `portrait-{id}`를 사용하며 타이틀도 네 리더의 디자인에 맞춘다. 두 게임의 타이틀·서사 초상·전장 배경과 새 `galilee-painted-sky`를 포함한 실제 이미지 목록·출처·해상도·슬롯은 [일러스트](Illustration.md)와 해당 생성 매니페스트가 기준이다. 기존 게임 캐릭터·의상·원화를 복제하거나 이전 반실사 그림에 필터만 적용한 자산으로 대체하지 않는다.

직전 새벽의 길 3D 개정은 `Tools/Art`의 ToonDawn 생성 경로로 얼굴·의복·머리카락·소품과 환경을 개선했다. 세 인물은 정강이·발을 포함한 14본 Generic 리그와 Idle/Walk를 갖고, 당시 Unity의 `Speed` BlendTree가 실제 이동에 맞춰 동작을 전환했다. Walk는 제자리 클립이고 월드 이동은 컨트롤러가 처리했다. 기존 `*Proxy.fbx` 파일명과 meta GUID는 보존한다. 원본 `.blend`·FBX·보행 프리뷰·매니페스트와 재가져오기는 [ToonDawn 제작 계약](../Tools/Art/ToonDawn.md)을 따르며, 아래 초기 기록을 후속 모델의 수치로 인용하지 않는다. 이 3D 제작 경로는 현재 일러스트 2.5D 화면의 필수 경로가 아니다.

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

아래는 첫 프록시의 역사적 생성 명령이다. **현재 체크아웃에서 실행하면 보존된 후속 모델을 구형으로 덮어쓸 수 있다.** 보존된 ToonDawn 모델의 재생성과 검증은 `Tools/Art/ToonDawn.md`의 `generate_toon_dawn.py` 경로를 사용한다. 현재 일러스트 2.5D 제작에는 이 명령이 필요하지 않다. 초기 결과가 필요하면 별도의 보존 복사본에서만 아래 명령을 실행한다.

```sh
blender --version
blender --background --python Tools/Art/generate_proxies.py -- --root . --render
blender --background --python Tools/Art/verify_proxies.py -- --root .
```

`--render`를 빼면 FBX·소스·매니페스트만 재생성한다. 모든 임시 메시와 재질을 스크립트로 다시 만들며, 기존 `.meta`는 덮어쓰지 않는다. 메시 생성 난수는 `10719`로 고정했다. Blender가 저장하는 타임스탬프 등의 이유로 바이너리 바이트가 매번 같다는 보장은 하지 않는다. 에셋을 수동 편집하여 승인 후보로 바꾸기 전, 다른 경로/이름으로 복사하고 생성기를 실행해도 덮어써지지 않게 한다.

생성 로그는 `Tools/Art/generation-log.txt`, FBX 재가져오기 검증은 `Tools/Art/verification.json`에 기록한다. Blender 설치의 OpenImageDenoise 미포함으로 최초 미리보기 렌더가 실패했으며, denoising만 끄고 Cycles CPU에서 재실행한다. 패키지 서명·TLS 등 설치 검증을 우회하지 않는다.

## 보존된 3D 자산의 Unity 가져오기와 교체 계약

아래는 과거 3D 표현의 좌표·리그·재질 계약이다. 현재 일러스트 장면의 배치와 입력은 [PaintedDawn.md](PaintedDawn.md)를 따른다.

- FBX 형식, 미터 단위, `forward=-Z, up=Y`로 내보낸다. Blender의 -Y 전면이 Unity +Z로 변환되며 Unity의 좌표계 변환으로 Blender X와 원본 FBX의 Unity X 부호가 반대인 것을 실제 네이티브 prefab 좌표에서 확인했다. 초기 Blender 재가져오기 검증만으로 Unity 좌표를 확정했던 설명을 정정한다.
- `GalileeShore`만 런타임 Y축 180° 회전하여 물가를 +X, 석조 공간을 (−4,−3)으로 맞춘다. 음수 스케일로 미러링하지 않는다. 최종 석조 공간 z 범위는 대략 −4.7…−1.3이며 출입구는 z≈−4.7이다. 등장인물 모델은 이 환경 회전의 자식이 아니며 개별 배치한다.
- 모델의 `POD_*` / `FC_*` 재질 이름과 기본색을 유지하고 모든 MeshRenderer/SkinnedMeshRenderer 슬롯에 재질을 연결한다. 새벽의 길 인물은 `PathOfDawn/Cel Lit`, 환경의 물과 하늘은 전용 셰이더를 사용한다. 구 초기 URP/Lit 설정으로 덮어쓰지 않는다.
- 보존된 ToonDawn 인물 리그는 Root/Pelvis/Spine/Head와 좌우 UpperArm/Forearm/Leg/Shin/Foot, 총 14본 Generic이다. 단일 SkinnedMeshRenderer에 Idle/Walk를 적용한다. 최종 Humanoid 리타게팅을 보장하지 않으며 대화 시선 IK·입모양·손가락·세밀한 발 접지는 별도 3D 제작 범위다.
- 천과 머리카락은 단순 스킨 또는 단색 메시다. 실제 천 시뮬레이션, 헤어카드, 피부 셰이더가 아니다.
- 환경에는 카메라·광원·충돌체·오디오를 내보내지 않는다. Unity의 카메라, 광원, 상호작용 앵커, 충돌 영역은 별도 책임이다.
- 게임 로직의 안정 ID/컴포넌트는 부모에 두고 시각 자식 프리팹만 바꾼다. 생성된 메시 이름을 저장 데이터의 콘텐츠 ID로 사용하지 않는다.

## 남은 제작

최종 원화와 역사/문화 검토, 그림 보행의 자세 연결·발 접지, 표정·손짓·대화 반응·립싱크, 장면별 가림과 깊이감, 전투·환경 효과음, 음성의 사람 청취 및 실제 PC/웹·휴대폰 실측이 남아 있다. 3D 제작을 후속으로 재개한다면 얼굴·손 토폴로지, UV·스키닝·천·머리카락·LOD 등도 별도 작업이다. 현재 그림·보행·BGM·초반 음성의 존재를 최종 품질 작업의 완료로 해석하지 않는다.

## 새벽의 길 움직임 데이터

`traveler-motion.bytes`는 기존 여행자 원화와 보행 atlas를 읽어 계산한 양방향 수치 대응장이다. 새 그림이나 외부 캐릭터를 포함하지 않는다. 재현 도구는 `Tools/Art/Painted/build-motion-fields.py`, 원화/UV/출력 해시와 데이터 형식은 `Tools/Art/Painted/motion-fields.json`에 기록한다. 생성 도구의 OpenCV·NumPy·Pillow는 클라우드 제작 의존성이며 게임 플레이어에 그 라이브러리를 포함하지 않는다. 일곱 PNG 원본 바이트는 그래픽 개편판과 같다.

## 2026-10-26 리마스터 추가

- 원작 카드161종의 원본·변환·이전96종 보존 검증은 `Docs/CardRemaster.md` 및 `Tools/Data/ClassicCards/remaster-verification.json`. 원작 리소스를 새 창작/허가 자산으로 표시하지 않는다.
- 예수님·마리아·요셉·선생 대화 초상화는 이번에 생성한 창작 재현이다. 원본 atlas와 해시는 `Tools/Art/GospelRemaster/manifest.json`. UI UV로 영역을 선택하며 원본 PNG는 변경하지 않는다.
- 기존 CC0 인물의 관절 동작 추가와 mesh 결합은 `Tools/Art/Gospel3D/action-upgrade.json`; 기존 기하·보행 보존 검증은 같은 폴더의 `mesh-preservation-verification.json`.
