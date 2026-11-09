# 다음 후보 초안: 조롱공격598과 라이벌라빗

**상태: 제안 초안 · 미구현 · 미검증.** 2026-11-09에 보존 자료와 현재 소스를 읽기 전용으로 대조했다. 새로운 네트워크 조회, 원작 클라이언트 확보, 생성기·Core·Unity·브라우저 실행을 수행한 기록이 아니다. 현재 릴리스가 끝난 뒤 별도 회차로 진행할 후보이며, 완료된 Web 근거를 이번 제안의 구현·검증으로 재사용하지 않는다. 이 문서만 추가하며 고정된 제품 소스·생성물·현재 카드 수·기존 검증 원본을 변경하지 않는다.

## 제안 범위와 근거

다음 한 작업으로 **조롱공격598과 대표 카드 라이벌라빗 `16092828518` 한 장**을 제안한다. 공식 설명과 후기 카드 원문이 정확히 일치하고, 이 원본 행에는 별도 상태·오라·성장·상성 기능이 없다. 같은 기술을 가진 다른 변형까지 포괄 승인하는 제안이 아니다.

| 자료 | 확인한 내용 |
| --- | --- |
| [공식 FAQ736 보존 본문](../../Tools/Research/FmRenewal/faq736-text.txt), 105–107행 | **조롱공격**: “AP 1 이상 공격시, 상대의 HP를 1로 변경하고, 상대의 감소한 HP를 상대의 AP에 더한다.” |
| [FAQ736 보존 응답 정보](../../Tools/Research/FmRenewal/faq736.json) | 실제 최종 URL은 `https://web.archive.org/web/20160911064803id_/http://www.fantasymasters.co.kr/HelpDesk/FAQ_view.asp?category=106&Nowpage=1&faqNo=736`, 응답160318바이트, SHA256 `8cb6a3eee331d090d8bee55a8cc138d95affb0d3c0520b1b304345ca8602c09f`. 요청 URL의2017 시각을 실제 보존 시각으로 표시하지 않는다. |
| [전체 DB 보존본](../../Tools/Data/Renewal/complete-observations.json), `fields.ID=16092828518` 행 | [라이벌라빗 원본 상세](https://fmlunatic.com/card/16092828518). `ID`는392285행, 정확한 기술 설명은392297행, `DeckLimit=2`는392301행이다. 출시일은2016-09-28이며, DB의2023 생성·갱신 시각은 원작 출시·패치 시각이 아니다. |
| [공개 enum 보존본](../../Tools/Data/Restoration/enumtable-observation.json), 3242–3246행 | 배열 값의 실제 기술 코드는 **598 = 조롱공격**이다. 바깥 객체 키597을 기술 코드로 사용하지 않는다. |
| [현재 원본 아카이브](../../Games/FantasyCards/Assets/Game/Core/OriginalArchiveData.cs), 376행 | `fm_16092828518`은 현재 `playable=false`인 원본 열람 항목이다. |
| [미디어 보존 목록](../../Tools/Data/Renewal/media-manifest.json), `sourceId=16092828518` 행 | 원본 WebP25008바이트, SHA256 `a535e98125ab213e159040c2d00bf672ff3b79f32dc3fe9253d679631922cab6`. 전달 JPG49008바이트, SHA256 `189be01981ba2557d055b9cf04a171d5a2a60a60711d0dcbf5b76491fe3c8935`. 기존285×405 전체 그림을 사용한다. |

보존 범위와 자료의 한계는 [Renewal 조사](FmRenewalSources.md)를 따른다. 공식 FAQ 본문은2016-09-11 보존본이며, 위 카드의 원문은2016-09-28 출시 행이다. 두 자료의 같은 효과 설명을 대조한 것이며, 종료 직전 전체 카드·패치를 확인했다는 뜻은 아니다.

라이벌라빗의 `fields` 전체를 UTF-8, `ensure_ascii=False`, 키 정렬, 구분자 `(',', ':')`로 직렬화한 SHA256은 다음과 같다. 구현 시 이 전체 행을 다시 검사하고 일치할 때만 한 장을 승인해야 한다.

```text
a2082e49292ed108c5b8171d0406c0b8c8717fc6e8e909368abe2e5c3699df94
```

| 원본 필드 | 그대로 보존할 값 |
| --- | --- |
| ID / 이름 | `16092828518` / 라이벌라빗 |
| 출시 / 등급 / 랭크 | `ReleaseDate=20160928`, `RareType=3` 언커먼, `RankType=1` N |
| 분류 / 능력 / 기술 | `Category=2`, `UnitType=21`, `Skill1=598`, `Skill2=1` |
| 원문 / 수치 연산 | 빈 `TextBase`, 정확한 조롱공격 설명만 있는 `TextExtra`, `ATKStatType=DEFStatType=HPStatType=1` |
| AP / DP / HP | `3+C2` / `0` / `3−C1` |
| 레벨 / 소울 | `LV=1`, `SoulType=4`, `RequireSoul=4101` 숲 유소울1 |
| 개별 덱 제한 | **`DeckLimit=2`**. 일반 카드처럼4로 바꾸지 않는다. |
| 그림 | 기존 `Games/FantasyCards/Assets/StreamingAssets/Cards/fm_16092828518.jpg`와 `.meta` |

## 최소 구현과 판정 순서

현재 [명시적 승인표](../../Tools/Data/Renewal/build-catalog.py)의 `CANONICAL_SKILLS`에 위 **ID·기술598·전체 필드 SHA만** 추가하는 방식이 적합하다. 일반 `SKILLS` 집합에598을 넣어 다른 랭크·상성·장비·상태가 섞인 변형을 함께 승인하지 않는다. [카탈로그 생성기](../../Tools/Data/ClassicCards/generate-catalog.py)는 기존 개별 `DeckLimit`, 수치별 코인, N랭크와 능력 비트를 그대로 생성할 수 있다.

전투는 기존 [RenewalStrike](../../Games/FantasyCards/Assets/Game/Core/BattleEngine.Renewal.cs)의 공격 전 기술 처리에 작은 분기를 추가하는 범위다. 기존 선제 방어 처리와 일반 공격·생존 반격 경로를 유지한다. 공식 본문에 없는 새로운 전체 기술 우선순위 체계를 이번 한 기술을 위해 만들지 않는다.

조건은 **이번 공격의 AP가1 이상**인 경우다. 일반 타격 피해를 계산하기 전에 상대의 **현재 남은 HP**를1로 만들고, 그 감소량을 **상대의 실제 AP**에 더한다. 현재 변수와 기존 helper를 사용한다면 의도한 연산은 다음과 같다. 아래는 구현 제안이며 현재 코드에 추가되었다는 뜻이 아니다.

```csharp
if (active)
{
    int lostHp = Math.Max(0, targetHp - 1);
    PremiumHealth(target, 1);
    PremiumAttack(target, targetAp + lostHp);
}
```

`PremiumHealth`는 기존 `damage`를 보존하며 총 HP를 `damage+1`로 맞추므로 이전 상처를 두 번 계산하지 않는다. `PremiumAttack`은 실제 rolled/base 상태에 맞춰 AP를 변경한다. 예를 들어 상대가 AP2·남은HP5라면 기술 직후 AP6·남은HP1이 된다. 이후 일반 타격이 DP에 막혀 피해0이면 그 상대는 강화된 AP로 생존 반격할 수 있다.

이 효과는 자신의 AP 증가나 추가 공격 피해가 아니다. 상대 HP 대입을 HP 타격 피해·처치·흡혈량으로 기록하지 않는다. 이전 상처를 지우거나 최대 HP를 감소량으로 사용하지 않으며, 사망한 대상을 되살리지 않는다. 생존 반격으로 공격하는 라이벌라빗에도 같은 조건을 적용한다. [공식 FAQ734](../../Tools/Research/FmRenewal/faq734-text.txt)의208–214행은 같은 턴의 반복 방어와 다음 턴 전까지의 감소 수치 유지를 설명하므로, 반복 교전에서는 갱신된 현재 수치를 사용해야 한다.

## 최소 검증과 호환 조건

후속 회차의 [기존 세 기술 검사](../../Tools/Validation/Tests/CanonicalAttackSkillsTests.cs)와 같은 실제 엔진/재생 프레임 방식으로 다음을 검증한다.

1. AP0에서는 미발동하고 AP1에서 발동한다. DP에 막혀 일반 피해가0이어도 기술의 사전 HP/AP 변화는 적용된다.
2. 상대의 현재 남은 HP만 감소량으로 사용한다. 기존 상처가 있는 경우에도 `damage`를 유지하고, 남은HP1이면 AP 증가량은0이다.
3. 공격 전 기술 이벤트 → 일반 타격 → 생존 반격 순서를 확인한다. 상대 AP 증가가 실제 반격에 적용되며, 자신의 AP와 `additionalAttackDamage`에 잘못 더하지 않는다.
4. 라이벌라빗의 생존 반격에도 발동한다. 같은 턴 반복 교전에서 이미HP1인 대상의 AP를 다시 증가시키지 않는다. 기존 다음 턴 회복 경로도 유지한다.
5. HP 대입을 별도 피해·처치·흡혈로 기록하지 않는다. 실제 코인 결과·RNG 소비와 깊은 복사 재생 프레임을 확인한다.
6. `schema3` 저장 후 재개의 상태·RNG·전투 결과가 일치하고 기존 저장·카드 ID·소유·덱·SP·기본덱 순서를 보존한다. 새로운 저장 필드·RNG·생성 카드 ID 체계를 추가할 필요가 없다.
7. 정확히 이 한 장만 출전으로 추가되고 다른598 변형은 기존 미지원 사유를 유지한다. `DeckLimit=2`, 실제 코인/비용/능력과 기존 그림·GUID 연결을 검사한다.
8. 새 Core 검증 후 해당 카드의 실제 장착·공격·반격 수치와 로그를 Native/Web에서 확인하고 현재 소스에 결합한 새 근거를 남긴다. 기존 Web 통과를 변경 후 통과로 표시하지 않는다.

현재 출전 수는2649종, 보존 원본은15350종이다. 후속 구현과 검증이 실제로 완료된다면 출전2650종·미지원12700종이 예상되지만, 이 초안으로 현재 수를 갱신하지 않는다. 생성 결과와 `RestorationCardsTests`, `PolishPremiumTests`, `canon_release_evidence.py`의2649 기대값은 그 후속 회차에서 함께 검토한다. 기존2649종 정의와 모든 원본·실패·검증 이력은 보존한다.
