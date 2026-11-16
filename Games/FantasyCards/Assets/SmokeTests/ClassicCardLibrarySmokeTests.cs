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
    public sealed class ClassicCardLibrarySmokeTests
    {
        IDisposable storage, preferences;
        MemorySaveBackend backend;
        string attempt;
        FantasyCardsApp App => UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
        Button Named(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(button => button.gameObject.activeInHierarchy && button.name == name);
        IEnumerator Click(string label) { SmokeUi.Click(label); yield return null; yield return null; }
        IEnumerator Press(string name) { var button = Named(name); Assert.That(button.interactable, Is.True, name); button.onClick.Invoke(); yield return null; yield return null; }
        IEnumerator Shot(string name, int width = 1280, int height = 720) => SmokeUi.Capture("FantasyCards", "classic-library-" + attempt + "-" + name, width, height, settleSeconds: .35f);
        void AssertActualTotals(string expected)
        {
            var label = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Single(text => text.gameObject.activeInHierarchy && text.name == "Classic library actual totals");
            Assert.That(label.text, Is.EqualTo(expected), "The visible status must report actual global totals and live search results.");
            label.ForceMeshUpdate();
            Assert.That(label.isTextTruncated, Is.False, "Actual collection and currency totals must remain readable in the compact dock.");
            Assert.That(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(character => character.isVisible), Is.True);
        }

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend = new MemorySaveBackend(); backend.WriteAtomic("preserved-player", "original player bytes");
            storage = PersistentSaves.UseBackendForTesting(backend); preferences = SmokeUi.PreserveDemoPreferences();
            yield return SmokeUi.LoadBoot();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            yield return SmokeUi.UnloadBoot(); preferences?.Dispose(); storage?.Dispose();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator OriginalEditorSelectionAndDeckTransferPreserveOwnedProfileAndReload()
        {
            yield return Click("게임 시작");
            yield return Press("Quest starter choice mono-light");
            yield return Click("이 덱을 무료로 받기");
            Assert.That(App.QuestDeckCards.Count, Is.EqualTo(50));
            string profile = JsonUtility.ToJson(App.QuestProfileSnapshot);
            var initialDeck = App.QuestDeckCards.ToArray();
            int money = App.QuestSp;
            yield return Click("덱 편집");
            long actualOwned = App.QuestProfileSnapshot.ownedCards.Sum(card => (long)card.count);
            int actualKinds = App.QuestProfileSnapshot.ownedCards.Count(card => card.count > 0);
            AssertActualTotals("보유 " + actualOwned + "장 / " + actualKinds + "종 · 검색 결과 " + actualKinds + "종 · " + money + " SP");
            Assert.That(App.ClassicLibraryEditingOwned, Is.True);
            Assert.That(App.ClassicLibraryVisibleDeckCopies, Is.EqualTo(10));
            var panes = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                .Where(rect => rect.gameObject.activeInHierarchy && (rect.name == "Classic owned card list" || rect.name == "Classic actual deck list")).ToArray();
            Assert.That(panes.Length, Is.EqualTo(2), "The verified original editor has two live card-list panes.");
            var copies = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Classic physical deck copy ")).ToArray();
            Assert.That(copies.Length, Is.EqualTo(10));
            for (int i = 0; i < 10; i++) Assert.That(copies.Any(button => button.name == "Classic physical deck copy " + i + " " + initialDeck[i]), Is.True,
                "The visible deck list must show stored physical copies, including duplicates, without regrouping or inventing cards.");
            Assert.That(Named("Classic library add selected card").interactable, Is.False, "Full deck additions remain prohibited.");
            yield return Shot("original-dual-list-desktop");
            yield return Shot("original-dual-list-touch", 616, 346);

            var choice = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Classic library card "))
                .OrderBy(button => button.transform.GetSiblingIndex()).Skip(1).First();
            string chosenId = choice.name.Substring("Classic library card ".Length);
            yield return Press(choice.name);
            Assert.That(App.ClassicLibrarySelectedCardId, Is.EqualTo(chosenId));
            Assert.That(App.QuestDeckCards, Is.EqualTo(initialDeck), "Selecting a card must not edit the deck.");
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot), Is.EqualTo(profile), "Selection must not change ownership, rewards, currency or profile data.");
            yield return Click("카드보기");
            yield return SmokeUi.WaitUntil(() => UnityEngine.Object.FindObjectsByType<FmArcanaCardSurface>(FindObjectsSortMode.None)
                .Count(surface => surface.gameObject.activeInHierarchy && surface.DefinitionId == chosenId && surface.UsesFullPrint) >= 2,
                10, "The selected original print must appear in both the live card view and the selected-card dock.");
            AssertActualTotals("보유 " + actualOwned + "장 / " + actualKinds + "종 · 검색 결과 " + actualKinds + "종 · " + money + " SP");
            yield return Shot("selected-original-print");
            yield return Click("목록보기");
            int quantity = App.QuestDeckCards.Count(id => id == chosenId);
            yield return Press("Classic library remove selected card");
            Assert.That(App.QuestDeckCards.Count, Is.EqualTo(49));
            Assert.That(App.QuestDeckCards.Count(id => id == chosenId), Is.EqualTo(quantity - 1));
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot), Is.EqualTo(profile), "Removing a deck copy must not destroy an owned card.");
            bool hadSavedDeck = backend.Exists("fantasy-quest-deck-v1");
            string deckBeforeInvalidSave = hadSavedDeck ? backend.Read("fantasy-quest-deck-v1") : null;
            string profileBeforeInvalidSave = backend.Read(FmQuestEconomy.SaveSlot);
            yield return Click("덱 저장");
            Assert.That(App.QuestCurrentScreen, Is.EqualTo("notice"));
            Assert.That(GameObject.Find("Quest information notice"), Is.Not.Null);
            Assert.That(SmokeUi.HasText("덱 저장 확인"), Is.True);
            Assert.That(SmokeUi.HasText("덱은 50장이어야 합니다."), Is.True, "A rejected real save must show the validator's actual reason.");
            Assert.That(App.QuestDeckCards.Count, Is.EqualTo(49));
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot), Is.EqualTo(profile));
            Assert.That(App.QuestSp, Is.EqualTo(money));
            Assert.That(backend.Exists("fantasy-quest-deck-v1"), Is.EqualTo(hadSavedDeck));
            if (hadSavedDeck) Assert.That(backend.Read("fantasy-quest-deck-v1"), Is.EqualTo(deckBeforeInvalidSave), "An invalid save cannot overwrite the last valid deck.");
            Assert.That(backend.Read(FmQuestEconomy.SaveSlot), Is.EqualTo(profileBeforeInvalidSave));
            yield return Click("확인");
            Assert.That(App.QuestCurrentScreen, Is.EqualTo("deck"));
            Assert.That(App.ClassicLibrarySelectedCardId, Is.EqualTo(chosenId));
            yield return Press("Classic library add selected card");
            Assert.That(App.QuestDeckCards.Count, Is.EqualTo(50));
            Assert.That(App.QuestDeckCards.Count(id => id == chosenId), Is.EqualTo(quantity));
            Assert.That(Named("Classic library add selected card").interactable, Is.False);
            Assert.That(App.QuestSp, Is.EqualTo(money));
            var editedDeck = App.QuestDeckCards.ToArray();
            yield return Click("덱 저장");
            Assert.That(App.QuestCurrentScreen, Is.EqualTo("lobby"));
            var saveNotice = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Single(text => text.gameObject.activeInHierarchy && text.name == "Classic actual single-player notice");
            Assert.That(saveNotice.text, Is.EqualTo("보유 카드로 구성한 50장 덱을 저장했습니다."), "The real successful save must confirm its outcome to the player before leaving the lobby.");
            saveNotice.ForceMeshUpdate();
            Assert.That(saveNotice.isTextTruncated, Is.False);
            Assert.That(saveNotice.textInfo.characterInfo.Take(saveNotice.textInfo.characterCount).Any(character => character.isVisible), Is.True);
            yield return SmokeUi.LoadBoot(); yield return Click("게임 시작");
            Assert.That(App.QuestDeckCards, Is.EqualTo(editedDeck), "Real save/reload must preserve every edited card ID and order.");
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot), Is.EqualTo(profile));
            Assert.That(App.QuestSp, Is.EqualTo(money));
            Assert.That(backend.Read("preserved-player"), Is.EqualTo("original player bytes"));

            string savedProfileBytes = backend.Read(FmQuestEconomy.SaveSlot);
            string savedDeckBytes = backend.Read("fantasy-quest-deck-v1");
            yield return Click("자유 연습");
            yield return Click("출전 카드");
            AssertActualTotals("전체 " + CardCatalog.All.Length + "종 · 검색 결과 " + CardCatalog.All.Length + "종");
            var search = UnityEngine.Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None)
                .Single(field => field.gameObject.activeInHierarchy && field.name == "Card search input");
            search.text = "2042920028";
            yield return Click("검색");
            Assert.That(App.ClassicLibraryVisibleCardCount, Is.EqualTo(1));
            AssertActualTotals("전체 " + CardCatalog.All.Length + "종 · 검색 결과 1종");
            search = UnityEngine.Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None)
                .Single(field => field.gameObject.activeInHierarchy && field.name == "Card search input");
            search.text = "__classic-library-no-results__";
            yield return Click("검색");
            Assert.That(App.ClassicLibraryVisibleCardCount, Is.Zero);
            Assert.That(SmokeUi.HasText("검색 결과가 없습니다."), Is.True);
            AssertActualTotals("전체 " + CardCatalog.All.Length + "종 · 검색 결과 0종");
            Assert.That(App.QuestDeckCards, Is.EqualTo(editedDeck));
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot), Is.EqualTo(profile));
            Assert.That(backend.Read(FmQuestEconomy.SaveSlot), Is.EqualTo(savedProfileBytes));
            Assert.That(backend.Read("fantasy-quest-deck-v1"), Is.EqualTo(savedDeckBytes));
            Assert.That(backend.Read("preserved-player"), Is.EqualTo("original player bytes"));
            yield return Click("대기실");
            yield return Click("덱 편집");
            yield return Click("덱 저장");
            Assert.That(App.QuestCurrentScreen, Is.EqualTo("free-practice"));
            var practiceNotice = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Single(text => text.gameObject.activeInHierarchy && text.name == "Classic actual practice notice");
            Assert.That(practiceNotice.text, Is.EqualTo("편집한 덱을 저장했습니다."));
            practiceNotice.ForceMeshUpdate();
            Assert.That(practiceNotice.isTextTruncated, Is.False);
            Assert.That(practiceNotice.textInfo.characterInfo.Take(practiceNotice.textInfo.characterCount).Any(character => character.isVisible), Is.True);
            Assert.That(App.QuestDeckCards, Is.EqualTo(editedDeck));
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot), Is.EqualTo(profile));
            Assert.That(backend.Read(FmQuestEconomy.SaveSlot), Is.EqualTo(savedProfileBytes));
            Assert.That(backend.Read("fantasy-quest-deck-v1"), Is.EqualTo(savedDeckBytes));
            yield return Click("카드 앨범");
            Assert.That(App.OriginalArchiveFilteredCount, Is.EqualTo(15350));
            yield return Shot("complete-original-album");
            yield return Click("전체 원본 카드");
            Assert.That(App.OriginalArchiveFilteredCount, Is.EqualTo(CardCatalog.All.Length), "Original art and actually supported definitions remain distinct.");
            var first = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .First(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Album card "));
            yield return Press(first.name);
            Assert.That(SmokeUi.FindButton("도감으로"), Is.Not.Null);
            Assert.That(SmokeUi.FindButton("덱에서 보기"), Is.Not.Null);
            Assert.That(App.QuestDeckCards, Is.EqualTo(editedDeck));
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot), Is.EqualTo(profile));
            yield return Shot("supported-full-source-detail");
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }
    }
}
