# Arcana 빌드와 원본 보존

현재 개정은 `fm-arcana-2026-10-27` / `gospel-authored-2026-10-27`이다. 실제 빌드 크기와 실행 검증은 `ArcanaValidation.md`에 기록한다.

카드 원본 PNG의 픽셀, 크기, GUID는 보존한다. Windows/제작 환경은 기존 비압축 가져오기를 유지하며 WebGL에만 ASTC 4×4, 품질100을 지정한다. 독립 Unity 가져오기 실험에서 285×405 NPOT 이미지는 DXT/ETC Crunch 사용 시 RGBA32로 되돌아갔지만 ASTC는 크기를 바꾸지 않고 압축되었다. 이 설정은 원본 카드의 종횡비를 바꾸거나 원본 화질이 높아졌다고 주장하지 않는다. [Unity Web 텍스처 문서](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-texture-compression.html)에 따라 지원하지 않는 GPU에는 소프트웨어 해제가 적용될 수 있어 메모리와 성능의 실기기 검증이 별도로 필요하다. 인쇄 원본 전체는 카드 상세의 ‘원본 카드’에서 볼 수 있다.

예수님의 길은 현재 Boot에서 참조하지 않는 이전 관찰자 `Resources/Art/Painted`, 루트 Art 이미지/재질과 `Resources/Voice`만 BuildPlayer 실행 중 임시로 제외한다. `LegacyResourceIsolation`은 변경 전에 전체 파일 SHA256/원래 메타/GUID를 Library 저널에 기록하며 AssetDatabase.MoveAsset으로 옮긴다. 정상 종료, 빌드 예외 모두 finally에서 복원한다. 비정상 종료 뒤에는 Editor 초기화와 다음 Ensure 이전에 같은 저널로 복원한다. 현재 Boot 의존성이 있는 자산은 제외를 거부한다. 원본 경로·모든 바이트·GUID 검증이 끝나야 저널을 지우고 타깃별 `LegacyResourceExclusions.json`을 기록한다. 소스 경로를 영구 변경하거나 이전 자산을 삭제하지 않는다.

임시 공간 정리는 고유 ZIP/원본 소스를 보존하고, ZIP의 파일 집합·CRC·전체 바이트 SHA가 대응되는 생성 플레이어 확장본과 재생성 캐시에 한정한다. 현재 작업의 복구 장부는 `Artifacts/Validation/Arcana/*recovery*.json`에 있다. 두 프로젝트의 동일한 패키지 잠금 파일을 확인한 뒤 카드 프로젝트의 무시되는 Library/PackageCache는 성전 프로젝트의 기존 패키지 캐시를 참조한다. Unity는 한 실행 소유자가 순차 실행한다.

작업 공간과 분리된 `/tmp` 파일시스템의 여유 공간을 쓰기 위해 두 프로젝트의 무시되는 Bee/Artifacts/BurstCache/ShaderCache만 작업 전용 경로로 복사하고 전체 파일 집합·바이트 SHA를 비교한 뒤 Library 심볼릭 링크로 연결한다. 장부는 `generated-cache-relocation.json`이며 원본 소스와 패키지 캐시는 이동하지 않는다. `/tmp`가 초기화되면 activate-shell의 작은 복구 스크립트가 이 작업 전용 링크의 빈 대상만 만들고 Unity가 캐시를 재생성한다.
