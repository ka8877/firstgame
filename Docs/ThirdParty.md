# 외부 구성 요소와 라이선스

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
