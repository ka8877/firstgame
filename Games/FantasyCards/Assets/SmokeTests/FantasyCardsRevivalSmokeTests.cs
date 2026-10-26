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
    /// <summary>The current client-style entry flow; retired avatar-selection fixtures are historical.</summary>
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
        public IEnumerator RevivalTitleLocalRoomsDeckChoiceShuffleAndContinueUseLiveCallbacks()
        {
            Assert.That(UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>(), Is.Not.Null);
            SmokeUi.AssertVisibleKorean("판타지마스터즈");
            Assert.That(SmokeUi.FindButton("게임 시작"), Is.Not.Null);
            Assert.That(SmokeUi.FindButton("새로운 결투"), Is.Null, "The retired anime-avatar entry must not remain the primary flow.");
            Assert.That(ActiveButtons("Leader ").Length, Is.Zero);
            Assert.That(SmokeUi.FindButton("이어하기").interactable, Is.False);
            SmokeUi.AssertNoMissingScripts();
            yield return SmokeUi.Capture("FantasyCards", "revival-01-title", 1280, 720);
            yield return SmokeUi.Capture("FantasyCards", "revival-01-title-mobile", 844, 390);

            SmokeUi.Click("게임 시작"); yield return null;
            Assert.That(ActiveButtons("Practice room ").Length, Is.EqualTo(3));
            Assert.That(SmokeUi.HasText("AI 대전"), Is.True, "Rooms must identify their actual single-player opponent.");
            Assert.That(ActiveButtons("Leader ").Length, Is.Zero);
            yield return SmokeUi.Capture("FantasyCards", "revival-02-local-lobby", 1280, 720);
            ActiveButtons("Practice room ").Single(button => button.gameObject.name == "Practice room 0").onClick.Invoke();
            yield return null;
            Assert.That(SmokeUi.FindButton("START"), Is.Not.Null);
            SmokeUi.Click("덱 선택"); yield return null;
            Assert.That(ActiveButtons("Starter deck ").Length, Is.EqualTo(4));
            SmokeUi.Click(DemoCatalog.GetDeck("forest").name); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "revival-03-four-starter-decks", 1280, 720);
            SmokeUi.Click("대전실로"); yield return null;
            Assert.That(SmokeUi.HasText(DemoCatalog.GetDeck("forest").name), Is.True);
            Assert.That(SmokeUi.HasText("컴퓨터"), Is.True);
            yield return SmokeUi.Capture("FantasyCards", "revival-04-local-game-room", 844, 390);
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
            yield return SmokeUi.Capture("FantasyCards", "revival-05-shuffled-opening", 1280, 720);
            SmokeUi.Click("이대로 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("Ready") != null && SmokeUi.FindButton("Ready").interactable,
                15, "The opening confirmation must enter the current battle board.");
            Assert.That(SmokeUi.ReadEngine().State.players[0].hand.Count, Is.EqualTo(6));
            Assert.That(SmokeUi.HasText("공격 준비"), Is.True);
            yield return SmokeUi.Capture("FantasyCards", "revival-06-battle-desktop", 1280, 720);
            yield return SmokeUi.Capture("FantasyCards", "revival-06-battle-mobile", 844, 390);
            yield return SaveThroughMenu();
            string saved = JsonUtility.ToJson(SmokeUi.ReadSavedBattle());
            int appId = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>().GetInstanceID();
            yield return SmokeUi.LoadBoot();
            Assert.That(UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>().GetInstanceID(), Is.Not.EqualTo(appId));
            SmokeUi.Click("이어하기");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("Ready") != null && SmokeUi.FindButton("Ready").interactable,
                15, "Continue must reconstruct the live classic battle in the new client flow.");
            yield return SaveThroughMenu();
            Assert.That(JsonUtility.ToJson(SmokeUi.ReadSavedBattle()), Is.EqualTo(saved));
            Assert.That(backend.Read("preserved-previous-player-slot"), Is.EqualTo("previous-player-save-bytes"));
            yield return SmokeUi.Capture("FantasyCards", "revival-07-continued-battle", 1280, 720);
            SmokeUi.AssertNoMissingScripts();
            LogAssert.NoUnexpectedReceived();
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
