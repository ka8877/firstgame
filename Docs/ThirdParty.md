# 외부 구성 요소와 라이선스

예수님의 길의 2026-11-19 시각 개선에는 hinzka의 `VRoid_V110_Male_v1.1.3.vrm` 원본을 별도로 보존하고 새 고대 복장과 재질을 제작한다. 출처는 [52blendshapes-for-VRoid-face](https://github.com/hinzka/52blendshapes-for-VRoid-face/tree/756f5abab7d2295ad5b5dbc2cd86972c388c48d2), 고정 커밋은 `756f5abab7d2295ad5b5dbc2cd86972c388c48d2`, 원본 SHA256은 `7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99`다. 해당 커밋의 README는 개인·상업 이용과 수정본을 포함한 재배포를 허용한다. 오래된 VRM 내장 메타데이터의 `Redistribution_Prohibited` 표기는 원본에 그대로 보존하며, 실제 이용 근거는 같은 배포자의 고정된 최신 README다. 수정 모델 전체를 CC0로 표시하지 않는다. 원문은 `Tools/Research/GospelVisual20261119/AnimeCandidates01/hinzka-README.md`에 보존한다.

새 회벽·사암·석재 바닥의 원본 2K 표면 지도는 Poly Haven의 [rough_plaster_03](https://polyhaven.com/a/rough_plaster_03), [sandstone_blocks_04](https://polyhaven.com/a/sandstone_blocks_04), [stone_floor](https://polyhaven.com/a/stone_floor), CC0-1.0이다. 공식 파일 정보·원본 바이트·MD5·SHA256·라이선스 원문은 `Tools/Art/GospelVisual20261119/EnvironmentSource01/`에 보존한다. 실제 게임에는 각 표면의 diffuse/OpenGL normal 쌍만 설치하며, 새 얼굴·눈·모발 셰이더는 프로젝트에서 작성한 URP 코드다.

| 구성 요소 | 공식 출처 | 고정/라이선스 |
|---|---|---|
| Unity 에디터/모듈 | Unity release API, `unity.com` 공식 CLI | 6000.3.26f1; 사용자 Unity 라이선스 |
| Unity 패키지 | Unity registry/에디터 BuiltInPackages | 각 manifest의 정확한 버전, 각 패키지 라이선스 |
| Ink Unity Integration | https://github.com/inkle/ink-unity-integration | 2.0.0, a0b734d906a6224050af7c4f39392fd9ba5988fe, MIT; 패키지 `LICENCE.md` 포함 |
| TextMeshPro Essential Resources | 에디터에 포함된 uGUI2.0.0의 공식 unitypackage | 두 프로젝트 `Assets/TextMesh Pro`에 원본 `.meta`와 함께 포함 |
| 한국어 폰트 | https://github.com/notofonts/noto-cjk | f8d157532fbfaeda587e826d4cd5b21a49186f7c, SIL OFL1.1 |
| .NET SDK | Microsoft official releases | 8.0.426, 공식 SHA512 확인 |
| NUnit/테스트 어댑터 | NuGet 공식 배포 | Tools/Validation packages.lock.json |

폰트 원본 `Sans/OTF/Korean/NotoSansCJKkr-Regular.otf` SHA256은 `6bcb2a0703aa137e874fc2dffa85f6c21ba9a67fa329e81b8c801663af7e992a`다. fontTools4.61.1로 Latin/문장부호/화살표/한글 호환 자모/완성형 한글을 남겨 1.7MB로 줄였고, 수정본 패밀리 이름을 **FirstGame Korean**으로 바꿨다. 원본 OFL을 각 프로젝트의 `Assets/Resources/Fonts/OFL.txt`에 포함한다. 원본은 작업 도구 캐시에 보존한다. 더 많은 언어를 추가하면 해당 문자 범위와 TMP glyph 검증을 확장한다.

카드 이미지와 Blender 에셋의 제작 지시문·도구·해시·교체 상태는 `Illustration.md`, `AssetRegister.md`, `Tools/Art/AssetManifest.json`을 참고한다. 기존 승인 원화는 제공되지 않았다. 참고한 기존 카드게임의 이름·이미지·로고를 게임 에셋으로 복제하지 않았다.

플레이어 ZIP의 `ThirdParty` 폴더에는 FirstGame Korean과 TMP 기본 LiberationSans의 OFL 전문을 함께 넣는다. 서사게임에는 Ink의 MIT 라이선스 전문도 포함한다. `Tools/Build/package-builds.py`가 프로젝트에 보존한 원문을 그대로 복사한다.
