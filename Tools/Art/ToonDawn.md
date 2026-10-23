# 새벽의 길: 캐릭터·물가 조형 개정

`generate_toon_dawn.py`는 이 저장소를 위해 직접 작성한 Blender 메시 생성기다. 외부 모델이나 텍스처를 가져오지 않았다. 최신 캐릭터 3종과 갈릴리 환경은 기존 FBX 파일을 교체하며 Unity `.meta`는 수정하지 않는다. 첫 프록시 생성기인 `generate_proxies.py`의 공통 함수를 재사용하지만 해당 파일 전체를 실행하지는 않는다. **현재 새벽의 길 모델을 재생성할 때는 아래 새 명령을 사용한다.** 예전 생성기를 단독 실행하면 구형 프록시로 돌아간다.

```sh
blender --background --threads 4 --python Tools/Art/generate_toon_dawn.py -- --root . --render
blender --background --threads 4 --python Tools/Art/verify_proxies.py -- --root .
blender --background --threads 4 --python Tools/Art/verify_toon_dawn.py -- --root .
```

실제 조형 변경:

- 성인 체형과 구별되는 얼굴·복장 실루엣. 얼굴은 턱·광대·이마·코가 연결된 메시이며 눈흰자/홍채/동공/눈꺼풀/눈썹/입술을 별도 형상으로 구성했다.
- 곡선 보간과 연속 프레임을 적용한 머리카락, 얼굴 곡면에 밀착한 수염. 원형 머리 덩어리와 빗살 같은 수염 막대를 제거했다.
- 몸통·소매를 5mm voxel 표면으로 연결하고 스킨 가중치를 다시 부여한 의복, 허리띠·직조 장식·연속 주름, 손가락·샌들·여행 가방. 캐릭터마다 SkinnedMesh 1개이며 같은 몸체의 static duplicate는 내보내지 않는다. 초기 Boolean 방식은 실제 렌더에서 의복 소실이 발견되어 사용하지 않는다.
- 석조 줄눈·지붕 목재/직물·곡면 선체·항아리·처진 어망·개별 올리브 잎과 가지. 환경은 논리 그룹별 9개 메시로 병합한다.
- 모델의 이름, FBX 경로, 10본 Generic 리그와 4초 `BasicIdle`, 환경의 원본 좌표 및 런타임 Y180 회전 계약을 유지한다. 새 `POD_*` 재질 기본색은 `ToonDawnManifest.json`과 공용 `AssetManifest.json`에 기록한다.

편집 원본은 `Source/ToonDawnCharactersAndShore.blend`, 실제 Blender 렌더는 `Previews/ToonDawn_CharacterLineup.png`, `ToonDawn_FaceDetail.png`, `ToonDawn_GalileeShore.png`다. 이 렌더는 Unity 실행 화면이 아니며 Unity 셀 셰이더와 조명은 게임의 실제 캡처로 별도 검수한다. 첫 프록시 원본과 미리보기는 비교 자료로 남긴다.

Blender 4.3.2의 실제 FBX 재가져오기는 공통 자산 5/5, 새 계약 4/4 통과했다. `toon-verification.json`은 캐릭터별 SkinnedMesh 1개/static duplicate 0개, Root 원점, 발바닥 높이 0, 좌우 중심, 가중치 합계, 10본 리그와 기존 `BasicIdle` take 이름을 확인한다. 최종 삼각형 수는 예수님 67,624, 미리암 66,814, 여행자 53,080, 환경 215,908이다. 각 FBX의 `.meta`는 변경하지 않았다. 실제 생성·렌더 로그는 `toon-generation-log.txt`, 검증 로그는 `toon-contract-log.txt`와 `toon-verification-log.txt`다.

현재 결과는 단순한 찰흙형 프록시를 벗어나기 위한 조형 개정이다. 생성 원화와 같은 완성도의 3D, 최종 초상 승인, 정밀한 역사적 외모 재현을 주장하지 않는다. 수작업 리토폴로지/UV·텍스처, 표정·입모양·시선 리그, 걷기·상호작용 애니메이션, 헤어·직물의 더 자연스러운 형태, 플랫폼별 LOD와 실측은 후속 작업이다. `BasicIdle`은 유지했지만 새 걷기 클립은 아직 만들지 않았다.
