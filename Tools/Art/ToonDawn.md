# 새벽의 길: 애니메이션풍 캐릭터·물가 조형 개정

`generate_toon_dawn.py`는 이 저장소를 위해 직접 작성한 Blender 메시 생성기다. 외부 모델이나 텍스처를 가져오지 않았다. 최신 캐릭터 3종과 갈릴리 환경은 기존 FBX 파일을 교체하며 Unity `.meta`는 수정하지 않는다. 첫 프록시 생성기인 `generate_proxies.py`의 공통 함수를 재사용하지만 해당 파일 전체를 실행하지는 않는다. **현재 새벽의 길 모델을 재생성할 때는 아래 새 명령을 사용한다.** 예전 생성기를 단독 실행하면 구형 프록시로 돌아간다.

```sh
blender --background --threads 4 --python Tools/Art/generate_toon_dawn.py -- --root . --render
blender --background --threads 4 --python Tools/Art/verify_proxies.py -- --root .
blender --background --threads 4 --python Tools/Art/verify_toon_dawn.py -- --root .
```

`storybook-dawn-02`는 따뜻하고 청량한 갈릴리 애니메이션 판타지 방향이다. 인물과 풍경을 함께 바꾸되, 기존 위치·충돌·상호작용 계약은 유지한다.

실제 조형 변경:

- 성인 체형과 구별되는 얼굴·복장 실루엣. 얼굴과 연결된 머리 장식을 함께 넓혀 애니메이션 비율을 조정하고, 콧날 돌출은 줄였다. 넓고 낮은 아몬드형 눈과 눈꺼풀·홍채·동공·작은 반짝임을 별도 형상으로 구성했다. 짧은 수염의 뾰족한 볼 경계를 둥근 턱 곡선으로 바꿨다.
- 곡선 보간과 연속 프레임을 적용한 머리카락, 얼굴 곡면에 밀착한 수염. 원형 머리 덩어리와 빗살 같은 수염 막대를 제거했다.
- 목·어깨 단면을 Hermite 곡선으로 보간하고 소매 시작점과 반경을 낮춰 솟은 어깨를 완화했다. 몸통·소매를 7mm voxel 표면으로 연결하고 완화·감면·한 단계 subdivision을 적용했다. 가중치는 작은 값을 제거한 뒤 합계 1로 정규화한다. 각 캐릭터는 SkinnedMesh 1개이며 static duplicate는 없다. 액세서리 병합 때 섞인 custom split normals는 제거한 뒤 매끄러운 정점을 사용한다. FBX는 FACE smoothing, Unity는 Import Normals로 그 표면을 유지한다. Unity 모델 인덱스는 UInt32를 명시한다. 기존 UInt16 설정은 65,535개가 넘는 정점을 여러 renderer로 나눠 단일 몸체 검증에서 실패했으므로 유지할 수 없다.
- 올리브 나무는 가는 가지 중심에서 넓고 둥근 수관 덩어리와 보조 잎으로 바뀌었다. 돌과 석조 모서리를 둥글게 다듬고, 풀과 꽃이 섞인 일곱 초지와 먼 언덕을 추가했다. 꽃은 각 화단 단위로 합쳐 생성 비용을 줄인다. 석조 줄눈·곡면 선체·항아리·어망도 유지하며 환경은 논리 그룹별 9개 메시로 병합한다.
- Unity에는 부드러운 3단계 셀 명암, 얇은 가장자리 색, 구름이 흐르는 하늘과 해안·물결 색이 움직이는 물 셰이더를 적용한다. 물은 불투명 색·프레넬 효과이며 실제 반사/굴절 시뮬레이션은 아니다.
- 모델의 이름, FBX 경로, 10본 Generic 리그와 4초 `BasicIdle`, 환경의 원본 좌표 및 런타임 Y180 회전 계약을 유지한다. 새 `POD_*` 재질 기본색은 `ToonDawnManifest.json`과 공용 `AssetManifest.json`에 기록한다.

편집 원본은 `Source/ToonDawnCharactersAndShore.blend`, 실제 Blender 렌더는 `Previews/ToonDawn_CharacterLineup.png`, `ToonDawn_FaceDetail.png`, `ToonDawn_GalileeShore.png`다. 이 렌더는 Unity 실행 화면이 아니며 Unity 셀 셰이더와 조명은 게임의 실제 캡처로 별도 검수한다. 첫 프록시 원본과 미리보기는 비교 자료로 남긴다.

Blender 4.3.2의 실제 FBX 재가져오기는 공통 자산 5/5, 새 계약 4/4 통과했다. `toon-verification.json`은 캐릭터별 SkinnedMesh 1개/static duplicate 0개, Root 원점, 발바닥 높이 0, 좌우 중심, 가중치 합계, 10본 리그와 기존 `BasicIdle` take 이름을 확인한다. 최종 삼각형 수는 예수님 173,936, 미리암 174,240, 여행자 119,920, 환경 405,908이다. 세 인물 합계는 468,096이며 전경 모두가 보일 때 환경과 합쳐 874,004다. 각 FBX의 `.meta`는 변경하지 않았다. 이번 생성·렌더·계약 검증 로그와 중간 실패 증거는 커밋 대상이 아닌 `Artifacts/Validation/Subculture/Blender/`에 보관한다. 자동 계약 결과는 `toon-verification.json`과 `verification.json`에 기록한다. 삼각형 수만으로 모바일 성능을 보장하지 않으며 WebGL 실행 흐름과 실기기 성능은 별도 검증이 필요하다.

현재 결과는 절차적 조형을 애니메이션풍으로 다듬은 개발용 개정이다. 생성 원화와 같은 완성도의 3D, 최종 초상 승인, 정밀한 역사적 외모 재현을 주장하지 않는다. 수작업 리토폴로지/UV·텍스처, 표정·입모양·시선 리그, 걷기·상호작용 애니메이션, 헤어·직물의 더 자연스러운 형태, 플랫폼별 LOD와 실측은 후속 작업이다. `BasicIdle`은 유지했지만 새 걷기 클립은 아직 만들지 않았다.
