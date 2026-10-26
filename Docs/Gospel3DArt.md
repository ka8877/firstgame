# 복음 여정 — 인물 제작 계약

이번 개정은 그림을 위치에 따라 움직이던 장면을 실제 관절과 입체 인물이 있는 3D 성전 탐색으로 바꾼다. 가장 중요한 대상은 예수님 주인공이다. 인체는 Quaternius의 제작된 연속 스킨 메시와 눈·손·발을 사용하고, 의상과 관절 동작을 맞춘다. 원기둥/구/상자를 조립한 이전 인체 프록시는 새 주인공의 대체 모델이 아니다.

## 에셋과 재현

- `Assets/Resources/Art/Gospel3D/YoungJesus.fbx`: 소년 구간 플레이어. 목표 키1.45m, 발 기준 원점, Unity +Z 정면.
- `Assets/Resources/Art/Gospel3D/AdultJesus.fbx`: 같은 제작 계약의 성인 인물. 목표 키1.78m. 미래 공생애 구간과 성인 인물 기반이며 해당 이야기 전체가 구현됐다는 뜻은 아니다.
- `material-spec.json`: URP Lit용 실제 재질 이름, baseColor, baseMap, smoothness. 기본 FBX Standard 재질의 색으로 대신하지 않는다.
- `Tools/Art/Gospel3D/build-actors.py`: Blender4.3 생성/리타깃/FBX/검토 렌더 경로. `blender -b -t 4 --python Tools/Art/Gospel3D/build-actors.py -- --root . --render`.
- `Tools/Art/Gospel3D/GospelActors.blend`: 선택 동작과 텍스처를 보존한 편집 원본. 미사용 원본 동작과 끊어진 원 제작자 로컬 텍스처 경로는 정리한다.
- `Tools/Art/Gospel3D/manifest.json`: 실제 메시·본·삼각형·클립 길이. 원본 및 변형 출처는 `SOURCE-NOTICE.txt`와 `Source/*License*.txt`에 있다.

본체·눈·눈썹·긴 머리·성인 수염은 Quaternius **Universal Base Characters Standard FREE**의 CC0 자료다. `Universal Animation Library Standard`의 **Idle_Loop / Walk_Loop / Jog_Fwd_Loop**를 배우의 제자리 관절 동작으로 리타깃했다. 이 동작이 모션캡처로 제작됐는지는 확인하지 않았으며, 전부 직접 캡처했다고 주장하지 않는다. 유료 Source/Pro 파일은 사용하지 않는다.

## 인물과 변형

소년은 성인 몸을 단순히 작게 보이는 데 그치지 않고 어깨·팔의 폭과 본 위치를 함께 줄이고 머리 비례를 조절한다. 정수리는 원 인체 두상에서 추출한 머리카락 표면으로 완전히 덮고, 앞/옆/뒤 머리와 가는 빗질 선을 결합한다. 눈·눈꺼풀·눈썹·코·입·귓바퀴가 있는 조형과 UV 피부/눈 텍스처를 유지한다. 실제 역사적 얼굴의 복원이라는 주장은 하지 않는다.

린넨 상의는 원 인체의 연결된 어깨·몸통·윗팔 토폴로지와 스킨 가중치를 이용한다. 몸통은 의복에 여유를 주고 원 인체 근육의 커스텀 노멀을 의상에 그대로 남기지 않는다. 별도로 부푼 소매를 붙이는 방식 대신 어깨에서 소매가 이어진다. 속 인체의 보이지 않는 부분은 제거해 피부가 의상을 뚫지 않게 한다. 허리의 상의/치마는 넓게 겹치고 직물 띠가 경계를 덮는다. 손가락은 원 locomotion의 쥔 손을 완화한 자세이며, 샌들 위에는 실제 조형된 발이 남는다.

## 애니메이션 계약

Generic rig, 제자리 `Idle`, `Walk`, `Run` 세 take만 게임 FBX에 내보낸다. 동작은 각각 30fps의 75/40/28프레임 간격(2.5초/1.333초/0.933초)이다. 관절의 로컬 회전 변화를 목표 bind pose로 옮겨 원본/목표 인체의 골격 길이 차이가 팔을 늘리지 않게 한다. `Animator.applyRootMotion=false`이며 실제 위치·충돌·가속은 CharacterController가, 시각 동작은 속도에 따른 Animator blend가 처리한다.

## 검토 범위

`Previews/*-front.png`, `*-three-quarter.png`, `*-face.png`, `*-walk-contact.png`는 실제 변형된 메시의 Blender 렌더다. 이것을 Unity 인게임 캡처나 사용자가 승인한 최종 인물로 소개하지 않는다. Unity에서 재질, 방향, 초기 정지, 걷기/달리기, 선회, 벽 앞 정지, 발 접지, UI가 얼굴을 가리지 않는지 따로 확인해야 한다.

이번 인물은 첫 조작 모델이다. 얼굴 표정/립싱크, 별도의 아기 배우/성인 이야기 전체, 천 시뮬레이션, 모든 NPC의 고유 조형, 물리 기기의 성능과 AAA 수준의 최종 아트는 구현/검수 범위를 구분한다. 향후 전투 동작은 같은 전문 골격을 기반으로 추가할 수 있으나 현재 전투 시스템이 존재한다고 주장하지 않는다.

## 2026-10-26 리마스터 추가

현재 개정의 보행·점프·착지·연습을 합친7개 클립과 소년8/성인10개 스킨드 렌더러 구성, 변환/기하 보존 기록은 `Docs/GospelRemaster.md` 및 `Tools/Art/Gospel3D/action-upgrade.json`을 따른다. 소년의 정점14565개·삼각형28250개와65개 뼈는 보존한다. 낮은 프레임에서도 이동 시간을 모두 적용하도록 작은 충돌 이동 단계로 나눈다.
