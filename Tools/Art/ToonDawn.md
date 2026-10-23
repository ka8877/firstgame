# 새벽의 길: 캐릭터·환경·걷기 제작

`generate_toon_dawn.py`는 이 저장소를 위해 작성한 Blender 메시·애니메이션 생성기다. 외부 모델·모션을 복사하지 않는다. 기존 FBX 네 개의 경로와 Unity GUID를 유지한다. 예전 `generate_proxies.py` 전체를 실행하면 구형 프록시로 돌아가므로 새벽의 길 재생성에는 아래 명령을 사용한다.

```sh
blender --background --threads 4 --python Tools/Art/generate_toon_dawn.py -- --root . --render
blender --background --threads 2 --python Tools/Art/verify_proxies.py -- --root .
blender --background --threads 2 --python Tools/Art/verify_toon_dawn.py -- --root .
blender --background --threads 4 --python Tools/Art/render_toon_walk.py -- --root .
```

현재 개정 `storybook-dawn-03-walk`는 더 큰 홍채·간결한 입선·얇게 흐르는 머리카락과 좁힌 어깨 실루엣을 사용한다. 목·어깨·소매는 연속 표면이며 열린 칼라와 가는 봉제선을 추가했다. 몸통에 정적 몸체를 겹치지 않고 각 인물은 SkinnedMeshRenderer 한 개를 유지한다. 매끄러운 정점 법선과 정규화 가중치를 사용하며 Unity 인덱스는 UInt32다. 이는 상용 대작과 같은 최종 캐릭터 품질이나 사용자 승인을 뜻하지 않는다.

환경은 블록을 전면에 쌓은 집에서 낮은 석조 기초·연속 회벽·둥근 문틀이 있는 작은 쉼터로 바꿨다. 맨 갈색 땅 대신 초지와 곡선 흙길을 구성했다. 실제 Unity 카메라에서 원경 언덕이 호수 위에 떠 있는 타원으로 보이는 문제를 확인하여 해당 재질 면을 제거했다. 원경은 별도 회화풍 파노라마가 맡는다. 물·나무·꽃밭과 기존 길·문·NPC 좌표 계약은 유지한다. 환경은 논리 그룹별 9개 메시다.

## 리그와 애니메이션 계약

기존 Root/Pelvis/Spine/Head, 좌우 UpperArm/Forearm/Leg에 **Shin_L/R 및 Foot_L/R**을 추가한 **14본 Generic 리그**다. 바지·종아리·샌들과 발의 가중치를 실제 관절에 나눴다. 여행자의 튜닉 앞면과 금색 밑단도 다리 움직임을 따라가게 하여 걷기 중 허벅지가 의상 밖으로 관통하던 문제를 고쳤다. 무릎은 2절 링크 해석해로 굽히며 지지 구간의 발을 평평하게 놓고 회수 구간에는 발을 들어 옮긴다. 팔 스윙, 상체 반대 회전과 작은 골반 상하 이동도 같은 클립에 기록한다.

- `BasicIdle`: 30fps, 1–121프레임, **4초** 반복.
- `Walk`: 30fps, 1–28프레임, **0.9초** 반복. 한 걸음 0.65m/지지 0.45초로 **1.45m/s** 이동에 맞춘다.
- 두 take는 해당 인물의 NLA strip만 FBX에 내보낸다. 다른 인물의 action은 포함하지 않는다.
- Root는 원점에 고정한다. 실제 월드 이동·회전·충돌은 게임 controller가 맡으며 Animator의 `applyRootMotion=false`다.
- `ProjectSetup`이 원본 take를 `*Idle.anim`, `*Walk.anim`으로 갱신하고 기존 controller GUID를 유지하며 float **Speed 0–1**의 1D BlendTree를 만든다.

`verify_toon_dawn.py`는 실제 FBX를 새로 가져와 리그·두 take 길이·발 가중치·루프·발 들기·전후 보폭·지지 높이·Root 이동 부재를 검사한다. Blender의 FBX importer는 Root의 유일한 자식 위치로 tail을 추측해 Pelvis에 원본에 없던 `connected` 제약을 붙인다. 이 제약은 파일에 있는 골반 translation 곡선을 억제한다. 체커는 그 importer 제약만 해제한 뒤 **내보낸 곡선을 그대로** 평가하고 조치 사실을 JSON에 기록한다. 키나 FBX 바이트를 고치거나 기대 보행 조건을 생략하지 않는다. Unity의 실제 Generic 애니메이션 평가는 native/Web 검사와 구분한다.

## Unity 재생 준비

`ToonPalette.json`은 생성기의 실제 POD 재질 RGB를 담는다. Unity의 외부 FBX 재질은 reimport 뒤 이전 색을 유지할 수 있으므로 `ProjectSetup`은 이 파일을 기준으로 셀 재질 색을 갱신한다. Web와 Desktop 기존 URP 자산에도 **renderScale 1.0 / 4x MSAA**를 적용한다. 새 셰이더·카메라·이동 표현과 실제 플레이 캡처는 게임 구현 및 `Docs/Validation.md`에서 확인한다.

편집 원본은 `Source/ToonDawnCharactersAndShore.blend`, Blender 렌더는 `Previews/ToonDawn_CharacterLineup.png`, `ToonDawn_FaceDetail.png`, `ToonDawn_GalileeShore.png`, `ToonDawn_WalkCycle.png`다. 걷기 프리뷰는 실제 클립의 네 위상을 배치한 Blender 렌더이며 Unity 화면을 대신하지 않는다. 이전 원본·미리보기·체커 실패는 `Artifacts/ValidationHistory/before-demo-walk-2026-10-23` 및 `Artifacts/Validation/Demo/Blender`에 보존한다.

삼각형은 예수님 175,041, 미리암 174,757, 여행자 127,177, 환경 342,996개다. 숫자는 `ToonDawnManifest.json`의 실제 내보내기 집계다. 표정·입 모양·시선·손가락·천/머리카락 물리와 LOD, UV 기반 수작업 페인팅은 아직 별도 제작 범위다. 해상도와 삼각형 수만으로 실기 모바일 성능을 주장하지 않는다.
