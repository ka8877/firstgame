using System;
using System.Collections;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Presentation;
using FirstGame.Shared;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FantasyCards.SmokeTests
{
    /// <summary>Current remaster callbacks and catalog browsing; retired avatar fixtures are historical.</summary>
    public sealed partial class FantasyCardsRevivalSmokeTests
    {
        IDisposable storageScope, preferenceScope;
        MemorySaveBackend backend;

        [UnitySetUp]
        public IEnumerator EnterGame()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            backend = new MemorySaveBackend();
            backend.WriteAtomic("preserved-previous-player-slot", "previous-player-save-bytes");
            storageScope = PersistentSaves.UseBackendForTesting(backend);
            preferenceScope = SmokeUi.PreserveDemoPreferences();
            yield return SmokeUi.LoadBoot();
        }

        [UnityTearDown]
        public IEnumerator LeaveGame()
        {
            yield return SmokeUi.UnloadBoot();
            preferenceScope?.Dispose(); preferenceScope = null;
            storageScope?.Dispose(); storageScope = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RemasterTitleLocalRoomsDeckChoiceShuffleAndContinueUseLiveCallbacks()
        {
            Assert.That(UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>(), Is.Not.Null);
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                .Where(listener => listener.isActiveAndEnabled).ToArray();
            Assert.That(listeners.Length, Is.EqualTo(1), "Native bundled music requires exactly one active output AudioListener.");
            var title = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .FirstOrDefault(text => text.gameObject.activeInHierarchy && string.Concat(text.text.Where(character => !char.IsWhiteSpace(character))) == "판타지마스터즈");
            Assert.That(title, Is.Not.Null, "The modern title must retain the complete Korean game name across its designed line break.");
            SmokeUi.AssertVisibleKorean(title.text);
            Assert.That(SmokeUi.FindButton("게임 시작"), Is.Not.Null);
            Assert.That(SmokeUi.FindButton("새로운 결투"), Is.Null, "The retired anime-avatar entry must not remain the primary flow.");
            Assert.That(ActiveButtons("Leader ").Length, Is.Zero);
            Assert.That(SmokeUi.FindButton("이어하기").interactable, Is.False);
            SmokeUi.AssertNoMissingScripts();
            yield return SmokeUi.Capture("FantasyCards", "remaster-01-title", 1280, 720);
            yield return SmokeUi.Capture("FantasyCards", "remaster-01-title-mobile", 844, 390);

            SmokeUi.Click("게임 시작"); yield return null;
            Assert.That(ActiveButtons("Practice room ").Length, Is.EqualTo(3));
            Assert.That(SmokeUi.HasText("AI 대전"), Is.True, "Rooms must identify their actual single-player opponent.");
            Assert.That(ActiveButtons("Leader ").Length, Is.Zero);
            yield return SmokeUi.Capture("FantasyCards", "remaster-02-local-lobby", 1280, 720);
            foreach(var brand in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Where(text=>text.gameObject.activeInHierarchy && text.text=="Fantasy Masters"))
            {
                brand.ForceMeshUpdate();
                Assert.That(brand.isTextTruncated,Is.False,"The narrow lobby rail must show the complete Fantasy Masters name.");
                Assert.That(brand.textInfo.characterCount,Is.EqualTo("Fantasy Masters".Length));
            }
            ActiveButtons("Practice room ").Single(button => button.gameObject.name == "Practice room 0").onClick.Invoke();
            yield return null;
            Assert.That(SmokeUi.FindButton("대전 시작"), Is.Not.Null);
            SmokeUi.Click("덱 선택"); yield return null;
            Assert.That(ActiveButtons("Starter deck ").Length, Is.EqualTo(4));
            SmokeUi.Click("다음"); yield return null;
            Assert.That(ActiveButtons("Starter deck ").Length, Is.EqualTo(1));
            Assert.That(ActiveButtons("Starter deck ").Single().gameObject.name, Is.EqualTo("Starter deck fidelity"));
            SmokeUi.Click("이전"); yield return null;
            SmokeUi.Click(DemoCatalog.GetDeck("forest").name); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "remaster-03-four-starter-decks", 1280, 720);
            SmokeUi.Click("대전실로"); yield return null;
            Assert.That(SmokeUi.HasText(DemoCatalog.GetDeck("forest").name), Is.True);
            Assert.That(SmokeUi.HasText("컴퓨터"), Is.True);
            yield return SmokeUi.Capture("FantasyCards", "remaster-04-local-game-room", 844, 390, () =>
            {
                var compactBrand = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                    .Single(text => text.gameObject.activeInHierarchy && text.gameObject.name == "Client brand");
                compactBrand.ForceMeshUpdate();
                Assert.That(compactBrand.text, Is.EqualTo("FM"), "The narrow rail uses an intentional complete abbreviation.");
                Assert.That(compactBrand.isTextTruncated, Is.False);
            });
            SmokeUi.Click("대전 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("이대로 시작") != null && SmokeUi.FindButton("이대로 시작").interactable,
                15, "The actual room start callback must shuffle and deal an opening hand.");
            SmokeUi.TraceUiState("fidelity-opening-before-read");
            var engine = SmokeUi.ReadEngine();
            Assert.That(engine.State.players[0].hand.Count, Is.EqualTo(6));
            uint beforeRng = engine.State.randomState;
            SmokeUi.Click("전체 손패 교체");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("전체 손패 교체") != null &&
                SmokeUi.FindButton("이대로 시작") != null, 5, "The actual mulligan callback must redraw the opening controls.");
            Assert.That(SmokeUi.ReadEngine(), Is.SameAs(engine), "Mulligan must keep the actual battle engine.");
            Assert.That(engine.State, Is.Not.Null, "Mulligan must retain its live state.");
            Assert.That(engine.State.players[0].hand, Is.Not.Null, "Mulligan must retain its live hand.");
            Assert.That(engine.State.players[0].hand.Count, Is.EqualTo(6));
            Assert.That(engine.State.randomState, Is.Not.EqualTo(beforeRng), "Whole-hand replacement must run the core shuffle.");
            Assert.That(SmokeUi.FindButton("전체 손패 교체").interactable, Is.False);
            yield return SmokeUi.Capture("FantasyCards", "remaster-05-shuffled-opening", 1280, 720);
            SmokeUi.Click("이대로 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("준비 완료") != null && SmokeUi.FindButton("준비 완료").interactable,
                15, "The opening confirmation must enter the current battle board.");
            Assert.That(SmokeUi.ReadEngine().State.players[0].hand.Count, Is.EqualTo(6));
            Assert.That(SmokeUi.HasText("공격 준비"), Is.True);
            yield return SmokeUi.Capture("FantasyCards", "remaster-06-battle-desktop", 1280, 720);
            yield return SmokeUi.Capture("FantasyCards", "remaster-06-battle-mobile", 844, 390);
            yield return SaveThroughMenu();
            string saved = JsonUtility.ToJson(SmokeUi.ReadSavedBattle());
            int appId = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>().GetInstanceID();
            yield return SmokeUi.LoadBoot();
            Assert.That(UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>().GetInstanceID(), Is.Not.EqualTo(appId));
            SmokeUi.Click("이어하기");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("준비 완료") != null && SmokeUi.FindButton("준비 완료").interactable,
                15, "Continue must reconstruct the live classic battle in the new client flow.");
            yield return SaveThroughMenu();
            Assert.That(JsonUtility.ToJson(SmokeUi.ReadSavedBattle()), Is.EqualTo(saved));
            Assert.That(backend.Read("preserved-previous-player-slot"), Is.EqualTo("previous-player-save-bytes"));
            yield return SmokeUi.Capture("FantasyCards", "remaster-07-continued-battle", 1280, 720);
            SmokeUi.AssertNoMissingScripts();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RemasterCatalogSearchSortPagingAndDeckControlsUseActualContent()
        {
            Assert.That(CardCatalog.All.Length, Is.GreaterThanOrEqualTo(161), "The fidelity catalog must retain the original shipped cards.");
            Assert.That(CardCatalog.All.Select(card => card.id).Distinct().Count(), Is.EqualTo(CardCatalog.All.Length));
            SmokeUi.Click("카드 도감"); yield return null;
            Assert.That(SmokeUi.HasText("전체 " + CardCatalog.All.Length + "종"), Is.True, "Active labels: " + string.Join(" | ", UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(text => text.gameObject.activeInHierarchy).Select(text => text.text).Take(20)));
            Assert.That(SmokeUi.FindButton("이전").interactable, Is.False);
            yield return SmokeUi.Capture("FantasyCards", "remaster-08-catalog", 1280, 720);

            var seen = new System.Collections.Generic.HashSet<string>();
            int pageTransitions = 0;
            while (true)
            {
                var page = VisibleCards();
                Assert.That(page, Is.Not.Empty);
                foreach (var card in page)
                    Assert.That(seen.Add(card.id), Is.True, "Paging must not repeat a card: " + card.id);
                if (!SmokeUi.FindButton("다음").interactable) break;
                Assert.That(++pageTransitions, Is.LessThan(CardCatalog.All.Length), "Paging must reach an end.");
                SmokeUi.Click("다음"); yield return null;
            }
            Assert.That(seen, Is.EquivalentTo(CardCatalog.All.Select(card => card.id)), "Every shipped card must be reachable through actual next-page callbacks.");
            Assert.That(SmokeUi.FindButton("이전").interactable, Is.True);
            yield return SmokeUi.Capture("FantasyCards", "remaster-09-last-catalog-page", 844, 390);

            SmokeUi.Click("원본 순서"); yield return null;
            Assert.That(SmokeUi.FindButton("소울 순"), Is.Not.Null);
            var firstCosts = VisibleCards().Select(card => card.cost).ToArray();
            Assert.That(firstCosts, Is.Ordered, "The sort callback must sort displayed card costs.");
            SmokeUi.Click("다음"); yield return null;
            var nextCosts = VisibleCards().Select(card => card.cost).ToArray();
            Assert.That(nextCosts, Is.Ordered);
            Assert.That(firstCosts.Last(), Is.LessThanOrEqualTo(nextCosts.First()), "Cost order must continue across pages.");

            SearchFor("코인"); yield return null;
            var ruleResults = VisibleCards();
            Assert.That(ruleResults, Is.Not.Empty);
            Assert.That(ruleResults.All(card => (card.text ?? "").Contains("코인") || card.name.Contains("코인")), Is.True, "Rules text must participate in search.");
            Assert.That(ruleResults.Any(card => !card.name.Contains("코인")), Is.True, "The query must find a rule match without depending on the name.");

            var added = CardCatalog.All.Skip(96).Last(card => CardCatalog.All.Count(other => other.name == card.name) == 1);
            SearchFor(added.name); yield return null;
            Assert.That(VisibleCards().Select(card => card.id), Is.EqualTo(new[] { added.id }), "A card added after the original 96 must be searchable by name.");
            Assert.That(SmokeUi.FindButton("다음").interactable, Is.False);
            ActiveButtons("Card ").Single().onClick.Invoke(); yield return null;
            var original = UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None)
                .Single(image => image.gameObject.activeInHierarchy && image.gameObject.name == "Original printed card");
            Assert.That(original.texture, Is.SameAs(Resources.Load<Texture2D>(added.artResource)), "Detail must display the original complete card source.");
            SmokeUi.Click("돌아가기"); yield return null;
            SearchFor("__NO_SUCH_FM_CARD__"); yield return null;
            Assert.That(VisibleCards(), Is.Empty);
            Assert.That(SmokeUi.HasText("이 조건에 맞는 카드가 없습니다."), Is.True);
            SmokeUi.Click("검색 / 필터 초기화"); yield return null;
            SmokeUi.Click("속성 · 전체"); yield return null;
            SmokeUi.Click("암흑"); yield return null;
            Assert.That(VisibleCards(), Is.Not.Empty);
            Assert.That(VisibleCards().All(card => card.element == Element.Darkness), Is.True);
            SmokeUi.Click("검색 / 필터 초기화"); yield return null;
            SmokeUi.Click("모든 카드"); yield return null;
            var editorDeck = ReadEditorDeck();
            Assert.That(VisibleCards().All(card => editorDeck.Contains(card.id)), Is.True, "Deck-only view must use actual editor membership.");
            SmokeUi.Click("대기실"); yield return null;
            SmokeUi.Click("덱 편집"); yield return null;
            SmokeUi.Click("내 덱 카드만"); yield return null;
            var editable = CardCatalog.Get(editorDeck.First(id => CardCatalog.All.Count(other => other.name == CardCatalog.Get(id).name) == 1));
            int originalCount = editorDeck.Count(id => id == editable.id);
            SearchFor(editable.name); yield return null;
            Assert.That(VisibleCards().Select(card => card.id), Is.EqualTo(new[] { editable.id }));
            Assert.That(editorDeck.Count, Is.EqualTo(50));
            Assert.That(SmokeUi.FindButton("+").interactable, Is.False, "A full 50-card deck cannot accept another card.");
            SmokeUi.Click(ActiveButtons("Collection list card ").Length > 0 ? "−" : "-"); yield return null;
            Assert.That(editorDeck.Count, Is.EqualTo(49));
            Assert.That(editorDeck.Count(id => id == editable.id), Is.EqualTo(originalCount - 1));
            Assert.That(SmokeUi.FindButton("+").interactable, Is.True);
            SmokeUi.Click("+"); yield return null;
            Assert.That(editorDeck.Count, Is.EqualTo(50));
            Assert.That(editorDeck.Count(id => id == editable.id), Is.EqualTo(originalCount));
            Assert.That(SmokeUi.FindButton("+").interactable, Is.False);
            yield return SmokeUi.Capture("FantasyCards", "remaster-10-search-and-deck-counts", 844, 390);
            SmokeUi.Click("덱 저장"); yield return null;
            Assert.That(PersistentSaves.TryLoad(SmokeUi.DeckSlot, SmokeUi.GameId, 1, CardCatalog.Version, out string payload, out string error), Is.True, error);
            Assert.That(JsonUtility.FromJson<DeckPayload>(payload).cards, Is.EqualTo(editorDeck), "The save callback must preserve the edited deck, including card multiplicity.");
            SmokeUi.AssertNoMissingScripts();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RemasterTextRemainsVisibleWhenRebuiltAtActualMobileCanvasSize()
        {
            yield return SmokeUi.Capture("FantasyCards", "remaster-11-title-616x346", 616, 346,
                () =>
                {
                    foreach (Element element in Enum.GetValues(typeof(Element))) AssertMobileLabel(CardCatalog.ElementName(element));
                    AssertMobileLabel(CardCatalog.All.Length + "종 카드  ·  " + CardCatalog.All.Select(card => card.rarity).Distinct().Count() + "등급  ·  " + DemoCatalog.StarterDecks.Length + "개 기본 덱");
                    AssertMobileLabel("게임 시작");
                    AssertMobileLabel("카드 도감");
                }, RebuildAtActualMobileSize);

            SmokeUi.Click("카드 도감"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "remaster-12-collection-616x346", 616, 346,
                () =>
                {
                    foreach (string label in new[] { "카드 도감", "검색", "원본 순서", "모든 카드", "속성 · 전체", "종류 · 전체", "소울 · 전체", "등급 · 전체", "이전", "다음", "대기실" })
                        AssertMobileLabel(label);
                    AssertMobileLabel("AP / DP / HP");
                    var legend = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                        .Single(value => value.gameObject.activeInHierarchy && value.text == "AP / DP / HP");
                    var canvas = legend.GetComponentInParent<Canvas>().rootCanvas;
                    var legendBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, legend.rectTransform);
                    var backBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, SmokeUi.FindButton("대기실").transform);
                    Assert.That(legendBounds.Intersects(backBounds), Is.False, "The mobile AP/DP/HP legend must not overlap the Back button.");
                    var checkedCards = new System.Collections.Generic.HashSet<string>();
                    int mobilePages = 0;
                    do
                    {
                        Assert.That(++mobilePages, Is.LessThanOrEqualTo(CardCatalog.All.Length), "The mobile catalog must reach its final page.");
                        Canvas.ForceUpdateCanvases();
                        AssertMobileMiniCards();
                        foreach (var card in VisibleCards()) checkedCards.Add(card.id);
                        if (!SmokeUi.FindButton("다음").interactable) break;
                        SmokeUi.Click("다음");
                    } while (checkedCards.Count <= CardCatalog.All.Length);
                    Assert.That(checkedCards, Is.EquivalentTo(CardCatalog.All.Select(card => card.id)), "Every real catalog name, cost and stat must fit the mobile collection cards.");
                }, RebuildAtActualMobileSize);
            ActiveButtons("Card ").First().onClick.Invoke(); yield return null;
            SmokeUi.Click("원본 카드"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "remaster-13-original-caption-616x346", 616, 346,
                () =>
                {
                    AssertMobileLabel("원본 인쇄 정보와 현재 DB 수치는 버전에 따라 다를 수 있습니다.");
                    AssertMobileLabel("능력 설명");
                }, RebuildAtActualMobileSize);
            SmokeUi.Click("능력 설명"); yield return null;
            SmokeUi.Click("돌아가기"); yield return null;
            SmokeUi.Click("대기실"); yield return null;
            ActiveButtons("Practice room ").Single(button => button.gameObject.name == "Practice room 0").onClick.Invoke(); yield return null;
            SmokeUi.Click("대전 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("이대로 시작") != null && SmokeUi.FindButton("이대로 시작").interactable, 15, "The room must reach its real opening hand.");
            UseLegalStressHand();
            yield return SmokeUi.Capture("FantasyCards", "remaster-15-long-coins-opening-616x346", 616, 346,
                () =>
                {
                    Assert.That(ActiveButtons("Card ").Length, Is.EqualTo(6));
                    AssertMobileLabel("카드 수치 순서  AP / DP / HP");
                    AssertMobileMiniCards();
                }, RebuildAtActualMobileSize);
            SmokeUi.Click("이대로 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("준비 완료") != null && SmokeUi.FindButton("준비 완료").interactable, 15, "The confirmed hand must reach the real battle.");
            yield return SmokeUi.Capture("FantasyCards", "remaster-16-long-coins-hand-616x346", 616, 346,
                () =>
                {
                    Assert.That(ActiveButtons("Card ").Length, Is.EqualTo(6));
                    AssertMobileMiniCards();
                    var hand = UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None)
                        .Single(value => value.gameObject.activeInHierarchy && value.gameObject.name == "Scrollable hand");
                    Assert.That(hand.content.rect.width, Is.GreaterThan(hand.viewport.rect.width), "The sixth mobile hand card must remain reachable by horizontal scrolling.");
                    hand.horizontalNormalizedPosition = 1;
                    Canvas.ForceUpdateCanvases();
                    AssertMobileMiniCards();
                }, RebuildAtActualMobileSize);
            var ordinaryState = SmokeUi.ReadEngine().State.Copy();
            var railApp = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            var lockedField = typeof(FantasyCardsApp).GetField("locked", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            bool ordinaryLocked = (bool)lockedField.GetValue(railApp);
            try
            {
                SetLegalResourceStressState();
                yield return SmokeUi.Capture("FantasyCards", "remaster-17-resource-rail-616x346", 616, 346,
                    () =>
                    {
                        AssertMobileResourceRail();
                        var engine = SmokeUi.ReadEngine();
                        engine.State.activePlayer = 1;
                        BattleEngine.Validate(engine.State);
                        RebuildAtActualMobileSize(); AssertMobileResourceRail();
                        lockedField.SetValue(railApp, true);
                        RebuildAtActualMobileSize(); AssertMobileResourceRail();
                    }, RebuildAtActualMobileSize);
            }
            finally
            {
                if (railApp != null)
                {
                    railApp.StopAllCoroutines();
                    typeof(FantasyCardsApp).GetField("engine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(railApp, new BattleEngine(ordinaryState));
                    lockedField.SetValue(railApp, ordinaryLocked);
                    typeof(FantasyCardsApp).GetMethod("ShowBattle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(railApp, null);
                }
            }
            SmokeUi.Click("기록 / 메뉴"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "remaster-14-menu-616x346", 616, 346,
                () =>
                {
                    foreach (string label in new[] { "전투 메뉴", "전투 기록", "음악", "저장", "대기실", "전투로", "소각장 / 상태" }) AssertMobileLabel(label);
                }, RebuildAtActualMobileSize);
            LogAssert.NoUnexpectedReceived();
        }

        static void RebuildAtActualMobileSize()
        {
            var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Single(value => value.gameObject.activeInHierarchy && value.isRootCanvas && value.gameObject.name == "Fantasy Masters Client");
            Assert.That(canvas.pixelRect.width, Is.EqualTo(616).Within(.5f));
            Assert.That(canvas.pixelRect.height, Is.EqualTo(346).Within(.5f));
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            var redraw = typeof(FantasyCardsApp).GetField("redraw", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(app) as Action;
            Assert.That(redraw, Is.Not.Null);
            // Recreate TMP objects after the capture canvas has its Web dimensions;
            // merely shrinking objects authored at 1280 misses the mobile font floor.
            redraw();
            Canvas.ForceUpdateCanvases();
        }

        static void AssertMobileLabel(string label)
        {
            var text = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Single(value => value.gameObject.activeInHierarchy && value.text == label);
            AssertMobileLabel(text);
        }

        static void AssertMobileLabel(TMP_Text text)
        {
            string label = text.text;
            text.ForceMeshUpdate();
            var glyphs = text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(value => value.isVisible).ToArray();
            Assert.That(new string(glyphs.Select(value => value.character).ToArray()),
                Is.EqualTo(new string(label.Where(value => !char.IsWhiteSpace(value)).ToArray())), label + " must draw every glyph at 616x346.");
            Assert.That(text.isTextTruncated, Is.False, label + " must fit its text area.");
            var canvas = text.GetComponentInParent<Canvas>().rootCanvas;
            Assert.That(text.fontSize * canvas.scaleFactor, Is.GreaterThanOrEqualTo(11.9f), label + " must remain at least 12 pixels.");
            float fringe = 1f / canvas.scaleFactor;
            var bounds = text.rectTransform.rect;
            foreach (var glyph in glyphs)
            {
                Assert.That(glyph.pointSize * canvas.scaleFactor, Is.GreaterThanOrEqualTo(11.9f), label + " actual glyph size");
                Assert.That(glyph.bottomLeft.y, Is.GreaterThanOrEqualTo(bounds.yMin - fringe), label + " lower glyph edge");
                Assert.That(glyph.topRight.y, Is.LessThanOrEqualTo(bounds.yMax + fringe), label + " upper glyph edge");
                Assert.That(glyph.bottomLeft.x, Is.GreaterThanOrEqualTo(bounds.xMin - fringe), label + " left glyph edge");
                Assert.That(glyph.topRight.x, Is.LessThanOrEqualTo(bounds.xMax + fringe), label + " right glyph edge");
            }
        }

        static void AssertMobileMiniCards()
        {
            foreach (var button in ActiveButtons("Card "))
            {
                var card = CardCatalog.Get(button.gameObject.name.Substring("Card ".Length));
                var labels = button.GetComponentsInChildren<TMP_Text>().Where(value => value.gameObject.name.StartsWith("Mini ", StringComparison.Ordinal)).ToArray();
                Assert.That(labels.Single(value => value.gameObject.name == "Mini name").text, Is.EqualTo(card.name));
                foreach (var label in labels)
                {
                    AssertMobileLabel(label);
                    Assert.That(label.textInfo.lineCount, Is.EqualTo(1), card.name + " must keep each cost or stat value on one line.");
                    var cardRect = (RectTransform)button.transform;
                    float fringe = 1 / label.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
                    foreach (var glyph in label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(value => value.isVisible))
                    {
                        var bottom = cardRect.InverseTransformPoint(label.rectTransform.TransformPoint(glyph.bottomLeft));
                        var top = cardRect.InverseTransformPoint(label.rectTransform.TransformPoint(glyph.topRight));
                        Assert.That(bottom.y, Is.GreaterThanOrEqualTo(cardRect.rect.yMin - fringe), card.name + " text must stay above the bottom card border.");
                        Assert.That(top.y, Is.LessThanOrEqualTo(cardRect.rect.yMax + fringe), card.name + " text must stay below the top card border.");
                    }
                }
                if (card.kind == CardKind.Unit || card.kind == CardKind.Equipment)
                {
                    Assert.That(labels.Single(value => value.gameObject.name == "Mini AP").text, Is.EqualTo(card.attack + CardCatalog.CoinText(card.attackCoin)));
                    Assert.That(labels.Single(value => value.gameObject.name == "Mini DP").text, Is.EqualTo(card.defense + CardCatalog.CoinText(card.defenseCoin)));
                    Assert.That(labels.Single(value => value.gameObject.name == "Mini HP").text, Is.EqualTo(card.health + CardCatalog.CoinText(card.healthCoin)));
                }
            }
        }

        static void SetLegalResourceStressState()
        {
            var engine = SmokeUi.ReadEngine(); var state = engine.State; var player = state.players[0];
            state.turn = 59; player.levelPoints = 50; player.genericSoul = 50;
            foreach (Element element in Enum.GetValues(typeof(Element))) player.SetResource(element, 50);
            while (player.terrain.Count < state.rules.terrainLimit)
            {
                var terrain = player.deck.First(card => CardCatalog.Get(card.definitionId).kind == CardKind.Terrain);
                player.deck.Remove(terrain); player.terrain.Add(terrain);
            }
            BattleEngine.Validate(state);
        }

        static void AssertMobileResourceRail()
        {
            var labels = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Where(value => value.gameObject.activeInHierarchy && value.gameObject.name.StartsWith("Rail ", StringComparison.Ordinal)).ToArray();
            Assert.That(labels, Is.Not.Empty);
            foreach (var label in labels)
            {
                AssertMobileLabel(label);
                Assert.That(label.textInfo.lineCount, Is.EqualTo(1), label.gameObject.name + " must remain on one line.");
            }
            foreach (Element element in Enum.GetValues(typeof(Element)))
            {
                Assert.That(labels.Single(value => value.gameObject.name == "Rail soul name " + element).text, Is.EqualTo(CardCatalog.ElementName(element)));
                Assert.That(labels.Single(value => value.gameObject.name == "Rail soul value " + element).text, Is.EqualTo("50"));
            }
            Assert.That(labels.Single(value => value.gameObject.name == "Rail LP").text, Is.EqualTo("LP50"));
            Assert.That(labels.Single(value => value.gameObject.name == "Rail total soul").text, Is.EqualTo("총소울50"));
            Assert.That(labels.Single(value => value.gameObject.name == "Rail terrain").text, Is.EqualTo("지형10/10"));
            var canvas = labels[0].GetComponentInParent<Canvas>().rootCanvas;
            var rail = GameObject.Find("Battle resource rail").GetComponent<RectTransform>();
            foreach (var label in labels)
            {
                var inside = RectTransformUtility.CalculateRelativeRectTransformBounds(rail, label.rectTransform);
                Assert.That(inside.min.y, Is.GreaterThanOrEqualTo(rail.rect.yMin), label.gameObject.name + " must stay inside the rail bottom.");
                Assert.That(inside.max.y, Is.LessThanOrEqualTo(rail.rect.yMax), label.gameObject.name + " must stay inside the rail top.");
                Assert.That(inside.min.x, Is.GreaterThanOrEqualTo(rail.rect.xMin), label.gameObject.name + " must stay inside the rail left.");
                Assert.That(inside.max.x, Is.LessThanOrEqualTo(rail.rect.xMax), label.gameObject.name + " must stay inside the rail right.");
            }
            var bounds = labels.Select(label => RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, label.rectTransform)).ToArray();
            for (int i = 0; i < bounds.Length; i++)
                for (int j = i + 1; j < bounds.Length; j++)
                    Assert.That(bounds[i].Intersects(bounds[j]), Is.False, labels[i].gameObject.name + " and " + labels[j].gameObject.name + " must not overlap.");
        }

        static void UseLegalStressHand()
        {
            var all = CardCatalog.All;
            int StatLength(CardDefinition card) => new[] { card.attack + CardCatalog.CoinText(card.attackCoin), card.defense + CardCatalog.CoinText(card.defenseCoin), card.health + CardCatalog.CoinText(card.healthCoin) }.Max(value => value.Length);
            var stress = new[] { all.Single(card => card.name == "포그자이언트"), all.Single(card => card.name == "레전드비스트"), all.First(card => card.kind == CardKind.Terrain && card.element == Element.Darkness), all.OrderByDescending(card => card.cost).First(), all.OrderByDescending(card => card.coloredCost).First() }
                .Concat(all.OrderByDescending(StatLength)).GroupBy(card => card.id).Select(group => group.First()).Take(6).ToArray();
            Assert.That(stress.Max(StatLength), Is.GreaterThanOrEqualTo(5), "The fixture must include the longest real coin expression class.");
            var deck = all.Where(card => card.kind == CardKind.Terrain).GroupBy(card => card.element).Select(group => group.First().id)
                .Concat(stress.Select(card => card.id)).Concat(all.Select(card => card.id)).Distinct().Take(50).ToList();
            DemoCatalog.ValidatePlayableDeck(deck);
            var engine = new BattleEngine(79, SmokeUi.ReadEngine().State.rules, deck);
            var player = engine.State.players[0];
            for (int i = 0; i < stress.Length; i++)
            {
                int inHand = player.hand.FindIndex(card => card.definitionId == stress[i].id);
                var previous = player.hand[i];
                if (inHand >= 0) { player.hand[i] = player.hand[inHand]; player.hand[inHand] = previous; }
                else
                {
                    int inDeck = player.deck.FindIndex(card => card.definitionId == stress[i].id);
                    Assert.That(inDeck, Is.GreaterThanOrEqualTo(0));
                    player.hand[i] = player.deck[inDeck]; player.deck[inDeck] = previous;
                }
            }
            BattleEngine.Validate(engine.State);
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            typeof(FantasyCardsApp).GetField("engine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(app, engine);
            typeof(FantasyCardsApp).GetMethod("ShowOpeningHand", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(app, null);
        }

        [Serializable] sealed class DeckPayload { public string[] cards; }

        static CardDefinition[] VisibleCards() => ActiveButtons("Card ").Concat(ActiveButtons("Collection list card "))
            .OrderBy(button => button.transform.GetSiblingIndex())
            .Select(button => CardCatalog.Get(button.gameObject.name.Substring(button.gameObject.name.StartsWith("Card ", StringComparison.Ordinal)
                ? "Card ".Length : "Collection list card ".Length))).ToArray();

        static void SearchFor(string value)
        {
            var input = UnityEngine.Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None)
                .Single(field => field.gameObject.activeInHierarchy && field.gameObject.name == "Card search input");
            input.text = value;
            SmokeUi.Click("검색");
        }

        static System.Collections.Generic.List<string> ReadEditorDeck()
        {
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            var field = typeof(FantasyCardsApp).GetField("customDeck", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var deck = field?.GetValue(app) as System.Collections.Generic.List<string>;
            Assert.That(deck, Is.Not.Null);
            return deck;
        }

        static Button[] ActiveButtons(string prefix) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Where(button => button.gameObject.activeInHierarchy && button.gameObject.name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();

        static IEnumerator SaveThroughMenu()
        {
            SmokeUi.Click("기록 / 메뉴"); yield return null;
            SmokeUi.Click("저장"); yield return null;
            SmokeUi.Click("전투로"); yield return null;
        }
    }
}
