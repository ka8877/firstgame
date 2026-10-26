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
    public sealed class FantasyCardsRevivalSmokeTests
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
            SmokeUi.AssertVisibleKorean("판타지마스터즈");
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
            Assert.That(SmokeUi.FindButton("START"), Is.Not.Null);
            SmokeUi.Click("덱 선택"); yield return null;
            Assert.That(ActiveButtons("Starter deck ").Length, Is.EqualTo(4));
            SmokeUi.Click(DemoCatalog.GetDeck("forest").name); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "remaster-03-four-starter-decks", 1280, 720);
            SmokeUi.Click("대전실로"); yield return null;
            Assert.That(SmokeUi.HasText(DemoCatalog.GetDeck("forest").name), Is.True);
            Assert.That(SmokeUi.HasText("컴퓨터"), Is.True);
            yield return SmokeUi.Capture("FantasyCards", "remaster-04-local-game-room", 844, 390);
            SmokeUi.Click("START");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("이대로 시작") != null && SmokeUi.FindButton("이대로 시작").interactable,
                15, "The actual room START callback must shuffle and deal an opening hand.");
            var engine = SmokeUi.ReadEngine();
            Assert.That(engine.State.players[0].hand.Count, Is.EqualTo(6));
            uint beforeRng = engine.State.randomState;
            SmokeUi.Click("전체 손패 교체"); yield return null;
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
            Assert.That(CardCatalog.All.Length, Is.EqualTo(161), "The remaster must ship the expanded actual catalog.");
            Assert.That(CardCatalog.All.Select(card => card.id).Distinct().Count(), Is.EqualTo(161));
            SmokeUi.Click("카드 도감"); yield return null;
            Assert.That(SmokeUi.HasText("전체 161종"), Is.True);
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
            SmokeUi.Click("-"); yield return null;
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
                    AssertMobileLabel("싱글플레이 · 일곱 속성 · " + CardCatalog.All.Length + "종 카드");
                    AssertMobileLabel("게임 시작");
                    AssertMobileLabel("카드 도감");
                }, RebuildAtActualMobileSize);

            SmokeUi.Click("카드 도감"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "remaster-12-collection-616x346", 616, 346,
                () =>
                {
                    foreach (string label in new[] { "CARD COLLECTION · 카드 도감", "검색", "원본 순서", "모든 카드", "속성 · 전체", "종류 · 전체", "소울 · 전체", "등급 · 전체", "이전", "다음", "대기실" })
                        AssertMobileLabel(label);
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
            SmokeUi.Click("START");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("이대로 시작") != null && SmokeUi.FindButton("이대로 시작").interactable, 15, "The room must reach its real opening hand.");
            SmokeUi.Click("이대로 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("준비 완료") != null && SmokeUi.FindButton("준비 완료").interactable, 15, "The confirmed hand must reach the real battle.");
            SmokeUi.Click("기록 / 메뉴"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "remaster-14-menu-616x346", 616, 346,
                () =>
                {
                    foreach (string label in new[] { "GAME OPTION", "전투 기록", "음악", "저장", "대기실", "돌아가기" }) AssertMobileLabel(label);
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
                Assert.That(glyph.bottomLeft.y, Is.GreaterThanOrEqualTo(bounds.yMin - fringe), label + " lower glyph edge");
                Assert.That(glyph.topRight.y, Is.LessThanOrEqualTo(bounds.yMax + fringe), label + " upper glyph edge");
                Assert.That(glyph.bottomLeft.x, Is.GreaterThanOrEqualTo(bounds.xMin - fringe), label + " left glyph edge");
                Assert.That(glyph.topRight.x, Is.LessThanOrEqualTo(bounds.xMax + fringe), label + " right glyph edge");
            }
        }

        [Serializable] sealed class DeckPayload { public string[] cards; }

        static CardDefinition[] VisibleCards() => ActiveButtons("Card ")
            .OrderBy(button => button.transform.GetSiblingIndex())
            .Select(button => CardCatalog.Get(button.gameObject.name.Substring("Card ".Length))).ToArray();

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
            SmokeUi.Click("돌아가기"); yield return null;
        }
    }
}
