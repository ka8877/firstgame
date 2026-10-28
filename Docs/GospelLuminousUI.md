# 말씀의 빛 · 2D 화면 구성

`gospel-luminous-2026-10-28`은 첨부한 오리와 Unity Lost Crypt 화면에서 깊이·빛·여백을 참고한 새 횡스크롤 표현이다. HUD·아이콘·메뉴 코드는 이 프로젝트에서 새로 작성했으며 상용 게임의 UI 소스나 고유 자산을 복구한 결과로 소개하지 않는다.

현재 진입점의 오케스트레이션은 `GospelLuminousApp`, UI는 `GospelLuminousUi`가 맡는다. 이전 `GospelJourneyApp`의 3D 메뉴와 원본은 이력으로 보존한다. 새 UI가 성경의 본문·진행·저장 규칙을 소유하지 않는다.

## 화면과 실제 정보

- 제목은 실제 2D 배경 위의 왼쪽 그라데이션과 큰 글자, 새 여정·이어하기·설정으로 구성한다. 새 여정의 첫 화면은 바로 아름다운 게임 공간이며 탄생 말씀은 별도 말씀 버튼으로 순서대로 연다.
- 탐험 HUD는 왼쪽 인물·말씀, 위 중앙 현재 목표, 오른쪽 기록·가방·쉼표 메뉴와 하단 작은 본문으로 구성한다. 참고 화면의 체력·마력 표시를 가져와 거짓 전투 수치를 만들지 않는다. 작은 빛 표시는 호출자가 전달한 실제 읽은 말씀 수다.
- 성경 본문은 출처·장절을 붙인 별도 스크롤 영역이다. 인물의 학습용 대화와 직접 인용을 섞지 않는다. 말씀을 읽는 동안에도 위쪽 세계가 보인다.
- 인물 메뉴·말씀 기록은 호출자가 실제 상태에서 만든 문장을 표시한다. 가방의 여섯 소품에는 학습용 게임 설정이라는 안내를 둔다. 등잔 장착/보관과 말씀 수첩 열기만 콜백으로 실제 기능에 연결한다.
- 터치는 왼쪽·오른쪽의 독립된 누름 입력, 달리기 토글, 점프와 대화 버튼을 제공한다. 메뉴 전환·컴포넌트 비활성화 때 이동 누름과 달리기를 해제한다.

1280×720 중앙 프레임과 CanvasScaler Expand를 사용한다. 본문은 대체로24–27, 작은 안내는20–23이며 터치 표적은58–78 기준 픽셀이다. HUD 바깥에는 입력을 가로막는 전체 화면 이미지를 두지 않는다. 메뉴만 어두운 전체 화면 막을 사용한다. 사용한 글꼴은 기존 `Fonts/Korean`과 `Fonts/Display`다.

## 코드 계약

```csharp
var ui = gameObject.AddComponent<GospelLuminousUi>();
ui.Build(KoreanFont);
ui.OnStart = StartNew;
ui.OnRead = ReadCurrentScripture;
ui.ShowTitle(canContinue);
ui.ShowHud(objective, reference, verse, readCount, totalCount, touch);
ui.UpdateHud(objective, reference, verse, readCount, totalCount, nearbyName, canInteract);
```

나머지 호출은 `ShowReading(heading, speaker, dialogue, reference, verses, progress, next)`, `ShowPause()`, `ShowInventory(lamp)`, `ShowStatus(body)`, `ShowJournal(body)`, `ShowSettings(volume, muted)`, `CloseModal()`, `Toast(message)`다. 콜백은 시작·이어하기·계속·저장·인물·가방·기록·설정·제목 복귀·등잔·말씀·점프·대화, 실제 이동값과 달리기·음악 음량/음소거·사용자 제스처를 전달한다.

각 버튼의 벡터 아이콘은 `GospelLuminousGlyph`가 실제 uGUI 메시로 그린다. CanvasRenderer와 새 메시 경로를 명시하고, 폰트에 없는 장식 문자나 빈 아이콘에 의존하지 않는다. UI의 둥근 모서리와 제목 그라데이션은 코드로 만든 작은 공용 텍스처이며 세계 원화의 대체재가 아니다.

## 검증 범위

코드의 공백 오류 확인을 마쳤다. 현재 구현만으로 실제 Unity/Web 렌더링·모바일 입력·사용자 미적 승인을 주장하지 않는다. 실제 시작·메뉴·말씀 스크롤·저장/복원 화면은 단일 실행 담당이 기록하는 새 Luminous 검증 결과를 따른다.
