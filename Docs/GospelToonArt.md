# 복음 여정 — 카툰 인물 개정

`gospel-toon-2026-10-27`은 이전의 사실적 얼굴을 작은 수정으로 유지하는 대신, 소년 예수·성인 예수·마리아·성전 교사·요셉의 얼굴과 눈·머리를 새로 조형한다. 원신·궤적 리메이크에서 참고한 방향은 읽기 쉬운 눈, 단순한 명암, 머리카락 덩어리와 부드러운 실루엣이다. 해당 게임의 메시·텍스처·실행 파일·소스를 추출한 자산은 사용하지 않는다. 같은 수준의 완성도나 역사적 외모 복원을 주장하지 않는다.

## 실제 변경

- 머리는 둥근 이마와 가늘어지는 턱, 낮은 코, 읽히는 입선을 가진 새 폐곡면 메시다. 이전 MakeHuman 얼굴 메시와 피부 사진을 새 머리에 사용하지 않는다.
- 눈은 얼굴 곡면 위의 불투명 아몬드형 공막, 호박색 홍채·동공·하이라이트, 부드러운 윗눈꺼풀이다. 눈과 눈썹이 머리카락 위로 나오는 초기 시안은 폐기했다. 앞머리·정수리·앞쪽 측면 머리의 깊이를 함께 수정해 실제 3D 가림이 성립하도록 했다.
- 머리카락은 정수리를 덮는 닫힌 상부와 수십 개의 굽고 끝이 좁아지는 입체 머릿단이다. 머릿단 UV를 사용하는 얕은 명암을 더하며, 이전 투명 헤어 카드를 재사용하지 않는다. 성인은 짧은 수염, 교사는 회색 머리, 마리아는 기존 베일로 구분한다.
- 어깨와 소매의 부피를 줄이고 직물 주름을 추가한다. 별도 평면 망토가 가슴을 뚫던 시안을 버리고, **튜닉과 동일한 정점·노멀·스킨을 쓰는 직조 색상 패널**로 바꿨다. 가슴이 부푼 별도 형상과 뜬 겹침 면은 없다.
- `GospelToonCharacter.shader`는 URP용 두 명암 그룹·부드러운 전환·작은 림·그림자·안개를 지원한다. `material-spec.json`이 셰이더와 실제 색을 지정한다. 나무·석재·도자기 등 환경의 PBR 재질은 이 인물 셰이더로 강제로 바꾸지 않는다.

## 보존과 재현

65개 본의 이름과 경로, `Idle / Walk / Run / JumpStart / JumpAir / JumpLand / Practice` 7개 클립, 기존 모델 리소스 경로와 `.meta` GUID를 보존한다. 이동·충돌·달리기·점프·성경 진행·저장 버전은 이 아트 도구가 바꾸지 않는다. 자세한 원본 라이선스는 [Gospel3DArt](Gospel3DArt.md)와 `Tools/Art/Gospel3D/SOURCE-NOTICE.txt`에 있다. 손·발·기존 보행 골격/클립은 Quaternius CC0 자료를 이어 사용하며 의상 제작 이력은 [GospelAuthoredArt](GospelAuthoredArt.md)에 남긴다.

```bash
# 기본 출력은 저장소 밖 /tmp. 먼저 그림과 FBX를 검토한다.
MESA_SHADER_CACHE_DIR=/tmp/firstgame-toon-mesa-cache blender -b -t 4 \
  --python Tools/Art/GospelToon/build-toon-actors.py -- --root . --render
blender -b -t 4 --python-exit-code 1 \
  --python Tools/Art/GospelToon/verify-toon-actors.py -- --root .
# 검토 뒤 최종 모델을 현재 리소스에 복사할 때만 --publish를 사용한다.
```

생성기는 보존된 `Tools/Art/GospelAuthored/GospelActorsAuthored.blend`를 골격·의상 입력으로 사용한다. 새 머리·눈·머리는 코드로 만든 원본 조형이다. 생성 중간 `.blend`와 반복 렌더는 `/tmp`에 두며, 재현 코드·최종 모델·선택한 미리보기·해시 기록을 소스에 남긴다.

## 검토 범위

`Tools/Art/GospelToon/Previews`는 Blender 자산 렌더다. 루트의 여덟 번째 소년 시안 검토는 내부 프로토타입 방향 검토이며 사용자의 미적 승인이나 Unity/Web 통과가 아니다. 실제 엔진 재질·조명·카메라·조작과 NPC 대화는 현재 [RestorationValidation](RestorationValidation.md)의 실행 결과로 따로 판단한다.

`export-verification.json`은 실제 FBX 5개를 다시 가져와 공유 65본 경로·7클립·105개 자세의 유한 좌표/범위·정수리 방향과 유효한 스킨 가중치를 검사한다. 기존 팔다리의 원시 가중치 합이 모두 1이라는 초기 검증 가정은 맞지 않았다. 해당 실패 로그는 보존하고, 0이 아닌 유한 가중치와 실제 변형을 검사하며 합의 범위를 그대로 기록한다. 새 머리·눈·머리카락·직물은 정규화된 가중치를 가진다.

실제 Native의 역광 정면 검토 뒤, 마지막으로 옷·머리 그림자색을 `(0.80, 0.78, 0.85)`로 높이고 투사 그림자 최소 밝기를 `0.78 → 0.88`로 조정했다. 피부의 따뜻한 그림자색과 모델·골격·애니메이션은 유지한다. 별도 점광원 계산을 추가하지 않으며, 최종 재질 준비와 Native/Web 재검사는 릴리스 검증을 따른다. 기존 Blender 미리보기는 이 마지막 명암 보정 전의 동일 형상이다. 정확한 변경 해시는 `Tools/Art/GospelToon/final-shadow-lift.json`에 있다.
