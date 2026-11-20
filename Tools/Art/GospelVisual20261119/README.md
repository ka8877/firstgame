# 예수님의 길: 새 인물·환경 소스

이 변경은 실제 새 3D 모델과 렌더링 코드를 보존하는 개발 중 소스입니다.
스타레일과 동등한 품질, 새 게임 실행 검증 또는 신규 공개를 뜻하지 않습니다.

`PreparedChildV10`에는 실제 성공한 V10 모델의 FBX/Blender 파일, 사용 텍스처,
바이트 해시·라이선스 연결, 실행했던 설정의 원본 사본과 구축 렌더 3장을 보존합니다.
구축 렌더는 Blender에서 만든 실제 모델 확인 이미지이며 게임 화면이 아닙니다.
얼굴·머리·109개 뼈·122개 표정 모프를 원본에서 유지하고 시대 의상과 샌들을 추가했습니다.

원본 인물은 hinzka가 공개한 VRoid 모델입니다. 사용·수정·재배포 조건은
`Tools/Research/GospelVisual20261119/AnimeCandidates01/hinzka-README.md`의
실제 배포 허락을 따릅니다. 원본 내 오래된 VRM 메타데이터와 이 최신 작성자 허락은
별도로 보존하며 이 모델을 CC0라고 부르지 않습니다. 리넨·배경 표면은 Poly Haven CC0이며
`LinenSource01/source-manifest.json`, `EnvironmentSource01/source-manifest.json`과
`Docs/ThirdParty.md`에 원 출처와 해시를 기록했습니다.

현재 새 인물은 Unity에서 가져오기/프리팹 생성 전입니다. 실제 Unity 실행은 라이선스
부재로 종료198을 반환했고, 공식 Personal 활성화도 `NOT_SIGNED_IN`을 반환했습니다.
만료된 로그인 링크를 재사용하지 않습니다. 계정 인증과 유효한 라이선스가 확보된 뒤
실제 import와 게임 화면을 먼저 확인해야 합니다. C# 보조 컴파일 통과는 그 검사를 대신하지 않습니다.

복구할 때 일반 Git LFS로 원본을 받은 뒤 `PreparedChildV10/source-manifest.json`의
모든 파일 해시를 대조합니다. 실행했던 `settings.executed01.json`의 절대 경로는
과거 실행 기록입니다. 별도의 `settings.portable.unexecuted.json`에서
`@REPO_ROOT@`를 현재 저장소의 절대 경로로 바꾼 **새 설정 파일**을 만들어야 합니다.
기존 실행 설정을 덮거나 새 설정을 이미 검증한 것으로 표시하지 않습니다.

실제 Editor를 시작하기 전에 `FIRSTGAME_RELEASE_PROFILE=gospel-visual`을 설정해
이번 시각 개선 경로를 선택합니다. 새 인물 검증 전에는 `GospelVisualProjectSetup`의
완료 marker를 만들지 않습니다.

준비 진입점은 `Firstgame.GospelNpr.Editor.GospelHeroChildPreparation.PrepareFromEnvironment`이며
`FIRSTGAME_HERO_CHILD_SETTINGS`, `FIRSTGAME_HERO_CHILD_REPORT`를 새 실제 설정과
기존 보고 폴더 아래 새 파일로 지정합니다. 설정 파일의 실제 SHA256도 별도로 기록합니다.
프로젝트의 Unlimited skin weights와 기존 locomotion controller가 필요합니다.
실제 Unity에서 모델/텍스처·리그·표정·옷과 이동/대화 화면을 검증한 뒤,
최종 소스에 대한 새 Native·Web·브라우저 검증으로 진행합니다.

판마의 진행 중 변경과 공개 파일은 이번 예수님 게임 소스 저장에 포함하지 않습니다.
