# 탄생 이야기·말씀 성경책·주요 대사 음성 검증

현재 개정은 `fm-nativity-2026-11-02` / `gospel-nativity-2026-11-02`, Web 프로필 `nativity`다. 이전 Restage main471adb17/public28d6abf와 모든 과거 원본/검사는 성공 이력으로 보존한다. 현재 소스에서 Core285개, 카드 Native6개, 예수님의 길 Native03의10개가 통과했다. 성공한 두 Unity Web와 실제 PC/터치4프로필은 현재 runtime의 전체 파일 SHA에 묶여 있고 Root가 실제 원본을 직접 검토했다. 카드 PC60·터치63검사, 예수님의 길 PC59·터치58검사이며 공개 배포/공개 실입력은 아직 진행 전이다. 이전 공개판을 새 구현의 통과로 대신하지 않는다.

새 근거는 `Artifacts/Validation/Nativity`의 일반 파일, raw 오디오·새 플레이어·브라우저·stage는 `/tmp/firstgame-nativity-*`다. 기존 원본·GUID·승인/실패 원본·고유 ZIP을 보존한다. 과거 고정 Artifact snapshot만 hardlink하며 live source/tool은 복사한다. `/tmp`와 `/workspace`가 다른 파일 시스템인 경우에는 정확한 byte-copy로 보존한다.

음악과 주요28대사 음성은 확정된 문안/생성기에서 실행 소유자가 로컬 생성했다. 기존22음성의 byte·문안·기존 ASR 시각을 보존하고6개만 추가 생성/ASR했다. 일반 한국어 MeloTTS 화자이며 특정 인물의 목소리를 복제하지 않는다. 신호/독립 ASR·실제 Unity/브라우저 출력·사람의 청취 검수를 구분한다. 새 quiet 획득 안내·성경책·음성 시작/중단/duck/음소거는 실제 새 실행과 원본 검토가 필요하다.

이전의 실제 JPEG GET 시각을 새 다운로드로 바꾸지 않는다. 보존된 Reprise 실제 GET/Root/source/native 입력과 최신 Restage Root 승인 공개 바이트를 새 Nativity History에 고정하고, 새 staged SHA·전후 실제 Git 이미지 객체가 모두 일치하는15350JPEG에만 바이트 근거를 계승한다. 모든 새 비이미지 공개 파일은 게시 후 실제 HTTPS GET한다. 게임 검사 통과는 전부 새 입력에서 수행한다.

이번 Nativity 사이트는 manifest를 포함한985,000,000바이트 미만, 개별 파일100,000,000바이트 미만을 유지해야 한다. 원본 이미지를 파괴하거나 과거 근거를 삭제해 예산을 맞추지 않는다. 현재 대상은 Web이며 물리 휴대폰/Safari/FPS·새 Windows/ZIP/Release·상용 Ori 소스/자산/동등 품질은 주장하지 않는다.

과거 Reprise 실제 GET 기록에는 실행한 최상위 producer SHA가 기록되어 있으나, 그때 import한 HTTP/helper 모듈의 실행 바이트 SHA는 따로 기록되지 않았다. 승인된 Reprise 소스266dcde의 producer/helper Git blob3개를 PublishedRepriseDependencies에 보존했고 최상위 producer는 실제 기록 SHA와 일치한다. 이는 게시 소스 provenance이며 당시 import 실행 바이트를 독립적으로 증명했다고 주장하지 않는다. JPEG 계승 근거는 Root가 승인한 실제 GET 기록·기록된 최상위 producer·현재 staged SHA/bytes와 실제 전후 Git 이미지 객체의 일치다.

Gospel Prepare01의 새fixture NUnit Is.AnyOf 미지원은 동일허용값의 EqualTo.Or로 고쳤다. Native01은8통과/2실패와 함께 전체story레이어 fade 중 미래필드가 비치는 문제가 실제 원본에서 발견되어 시각승인을 거부했다. 실패 XML·원본·96개C# 소스를 보존한 뒤 Root가 story배경은 즉시불투명, 대화판만fade하도록 최소수정했다. 나머지 두fixture는 소스32kOgg와 Unityimport44.1k를 구분한 길이·샘플·mono·정확clip 검증, 그리고11장도입완료chapter를 책열기 전에 검사하고 복귀뒤정확journey를 검사하도록 정정했다. Native02는 같은 NUnit API호환 컴파일실패로 보존되며, Native03은 수정된 현재소스에서10/10·입력변화0으로 통과했다. Core285/카드6은 이 Gospel전용 수정에 영향없어 반복하지 않았다.

두 실제Unity runtime만977,470,886바이트여서 초기의 보수적975MB 기준은 통과하지 못했다. 기존gzip6개의 marker/CRC/decoded SHA를 보존한 level9 무손실 후보는 더 작아지지 않아 채택하지 않았고, 최종runtime6파일은 Unity 원본SHA 그대로다. 원본/음성/픽셀/메타/Native를 바꾸지 않고 Nativity에만985MB 기준을 적용한다. 실제 완성 사이트를 이 기준 아래로 확인해 플랫폼decimal1GB에서 최소15MB 여유를 유지하며, 이전975MB를 통과했다고 주장하지 않는다.

카드 첫 PC 브라우저 실행은 실제 기본덱/600SP·구입/봉인복귀·55장·덱편집·셔플·전투복귀를 진행했으나, 첫 reload의 media range 취소4건에 필요한 같은 navigation의 후속 PCM이 계획에 없어 네트워크 검증을 실패로 보존했다. classifier나 임계값은 바꾸지 않고 봉인팩 복귀 뒤 실제 로비 PCM 검사를 추가해 새 실행한다.

예수님의 길 두 로컬 프로필은 실제 첫 음성의 Source clock/clip/text SHA 관찰→3.5초 PCM→재생 중 성경 열기로 즉시 중단→다음 대사 key1의 실제 재생을 함께 확인했다. 51개 도입 turn·작은 획득 안내·0/1본문 차례·채운 면/빈 미래 면·Joseph 실제 대화·Mary 우물·상태/장비·정확 XZ 저장/완전 reload를 진행했다. 터치 프로필의 성경 본문은 실제 CDP touch drag로 스크롤했으며 물리 휴대폰 검증과 구분한다. Source32kmono와 실제 Native44.1k import, 독립 ASR과 실제 출력 및 사람 청취는 서로 다른 근거다.

카드 첫 PC 실패는 History/CardDesktop01-NoReloadPcm에 원본 그대로 보존했다. 후속 새 PC60검사와 터치63검사는 같은 Unity runtime에서 봉인팩 reload 후 실제 로비 PCM을 추가해 통과했다. 최종 PC는 해당4abort가 다시 발생하지 않았으므로 과거 취소 분류를 새 결과에 전용하지 않았다. 양쪽 새 실행은 기본덱50장/600SP·400SP구입·봉인복귀·실제5개획득·55장보유·49장저장금지/50장복구·셔플·전투 저장/reload를 실제 입력으로 확인했다. 좁은 전장 손패는5장이 동시에 보이고 가로 ScrollRect가 있으므로6장 동시 노출을 주장하지 않는다.
