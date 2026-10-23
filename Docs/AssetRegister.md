# 자산 출처와 교체 장부

## 2026-10-23 변경

새 일러스트 PNG 9개(카드 아틀라스 4·두 게임 타이틀 2·서사 인물 초상 3)를 생성했다. `Docs/Illustration.md`와 `Tools/Art/Illustration/generation-manifest.json`이 실제 파일·슬롯·출처의 최신 기준이다. 원본 이미지 픽셀을 유지하고 Unity Sprite 영역으로 나누어 사용한다.

새벽의 길 3D는 `Tools/Art`의 ToonDawn 생성 경로로 얼굴·의복·머리카락·소품과 환경을 개선한다. 기존 `*Proxy.fbx` 파일명과 meta GUID는 연결 보존을 위해 유지한다. 모델의 최신 실제 수치와 재가져오기 결과는 제작 담당자의 매니페스트·검증 기록을 따르며, 아래 초기 기록을 새 모델의 수치로 인용하지 않는다.

기존 승인 원화·모델·ZIP·웹 시제품은 제공되지 않았다. 기존 자료를 임의로 삭제하거나 가져오지 않았다. 아래 3D 자산은 이 저장소를 위해 Blender 4.3.2 Python API로 직접 만든 오리지널 임시 프록시다. 외부 메시·텍스처를 다운로드하거나 혼합하지 않았다. 정확한 재질 이름, 파일 크기, 메시·정점·삼각형 수는 `Tools/Art/AssetManifest.json`에서 확인한다.

| 자산 | 위치 | 상태 / 출처 | 크기·교체 계약 |
|---|---|---|---|
| 갈릴리 물가/석조 공간 | `Games/PathOfDawn/Assets/Art/Generated/GalileeShore.fbx` | 임시, 저장소용 절차 생성 | 약 24×19m. Unity 원점의 지면 높이 약 0m. 실제 Unity FBX 원본은 석조 공간 x=+4,z=+3, 출입구 +Z, 물가 x<−4.7. 런타임에서 환경만 Y축 180° 회전하여 석조 공간 x=−4,z=−3, 출입구 −Z, 물가 x>4.7로 정규화한다. 충돌체는 이 최종 좌표에 별도 배치. |
| 예수님 프록시 | `Games/PathOfDawn/Assets/Art/Generated/JesusProxy.fbx` | 임시, 저장소용 절차 생성 | 약 1.8m, 발 기준 원점, Unity +Z 전면. Generic 리그와 `JesusProxy_BasicIdle`. |
| 여행자 프록시 | `Games/PathOfDawn/Assets/Art/Generated/TravelerProxy.fbx` | 임시, 저장소용 절차 생성 | 약 1.75m, 동일 축/피벗 계약. Generic 리그와 `TravelerProxy_BasicIdle`. |
| 미리암 프록시 | `Games/PathOfDawn/Assets/Art/Generated/MiriamProxy.fbx` | 임시, 저장소용 절차 생성 | 약 1.7m, 동일 축/피벗 계약. Generic 리그와 `MiriamProxy_BasicIdle`. |
| 별빛 전장 | `Games/FantasyCards/Assets/Art/Generated/StarfallArena.fbx` | 임시, 저장소용 절차 생성 | 직경 16.4m. 표면 y≈0.15m, UI/슬롯은 y≥0.25m. 외곽 8개 기둥. 게임의 슬롯 수나 규칙과 독립. |
| Blender 편집 소스 | `Tools/Art/Source/FirstSliceProxies.blend` | 위 5종의 원본 장면 | Unity 프로젝트 외부 보관. FBX를 Unity에 가져온다. |
| Blender 미리보기 | `Tools/Art/Previews/*_BlenderPreview.png` | 실제 Blender Cycles 렌더 | 원화 승인·Unity 실행 증거 아님. |
| 카드 원화 아틀라스 | `Games/FantasyCards/Assets/Resources/Art/card-atlas.png` | 이미지 생성 도구로 제작하는 후보 | 출처·슬롯 매핑·교체는 `Docs/Illustration.md`를 참조. 존재·검증 상태는 해당 문서가 기준. |
| 한글 글꼴 | 각 프로젝트 `Assets/Resources/Fonts` | 별도 설치 단계에서 원본/라이선스 포함 | 실제 파일명과 라이선스·출처는 프로젝트 글꼴 폴더와 `Docs/Development.md`를 참조. 본 장부는 미검증 글꼴을 승인하지 않음. |

## 재현과 검증

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
- 모델의 `POD_*` / `FC_*` 재질 이름과 기본색을 바탕으로 URP/Lit 재질을 만든다. 모든 MeshRenderer/SkinnedMeshRenderer 슬롯에 재질을 연결한다.
- 리그는 Root/Pelvis/Spine/Head/UpperArm/Forearm/Leg로 구성한 Generic 임시 리그다. 최종 Humanoid 리타게팅을 보장하지 않는다. 대화 시선 IK, 입모양, 손가락, 걷기는 후속 작업이다.
- 천과 머리카락은 단순 스킨 또는 단색 메시다. 실제 천 시뮬레이션, 헤어카드, 피부 셰이더가 아니다.
- 환경에는 카메라·광원·충돌체·오디오를 내보내지 않는다. Unity의 카메라, 광원, 상호작용 앵커, 충돌 영역은 별도 책임이다.
- 게임 로직의 안정 ID/컴포넌트는 부모에 두고 시각 자식 프리팹만 바꾼다. 생성된 메시 이름을 저장 데이터의 콘텐츠 ID로 사용하지 않는다.

## 남은 제작

승인 원화 확보, 캐릭터 턴어라운드와 역사/문화 검수, 얼굴·손 토폴로지, UV·텍스처, 부드러운 스키닝, 발 접지와 걷기, 자연스러운 대화 반응, 머리카락과 천, 환경 LOD·충돌체·광원 베이크, 오디오 제작 및 PC/웹 실측. 이 항목을 완료하기 전 현재 프록시를 최종 고품질 비주얼이라고 보고하지 않는다.
