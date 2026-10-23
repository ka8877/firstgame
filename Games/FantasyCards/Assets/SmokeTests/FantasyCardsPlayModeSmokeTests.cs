using System;
using System.Collections;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Presentation;
using FirstGame.Shared;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace FantasyCards.SmokeTests
{
    // Editor-only assembly, but every assertion below executes after EnterPlayMode.
    public sealed class FantasyCardsPlayModeSmokeTests
    {
        IDisposable storageScope;

        [UnitySetUp]
        public IEnumerator EnterGame()
        {
            // Avoid running a previously open game scene before test storage is installed.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            storageScope = PersistentSaves.UseBackendForTesting(new MemorySaveBackend());
            yield return SmokeUi.LoadBoot();
        }

        [UnityTearDown]
        public IEnumerator LeaveGame()
        {
            // Runtime quit/pause handlers must disappear before real storage is restored.
            yield return SmokeUi.UnloadBoot();
            storageScope?.Dispose(); storageScope = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TerrainPlayPersistsAcrossRealSceneReloadAndContinue()
        {
            Assert.That(UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>(), Is.Not.Null);
            Assert.That(GameObject.Find("StarfallArena"), Is.Not.Null, "The generated arena must be connected to Boot.");
            SmokeUi.AssertVisibleKorean("별빛의 서약");
            SmokeUi.AssertNoMissingScripts();
            yield return SmokeUi.Capture("FantasyCards", "01-title");
            Assert.That(SmokeUi.FindButton("이어하기").interactable, Is.False);

            SmokeUi.Click("새로운 결투");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장") != null && SmokeUi.FindButton("저장").interactable,
                5, "Battle Timeline did not release the UI input lock.");
            var initial = ReadBattle();
            Assert.That(initial.players[0].hand.Count, Is.GreaterThan(0));
            var terrain = initial.players[0].hand.First(c => CardCatalog.Get(c.definitionId).kind == CardKind.Terrain);
            var definition = CardCatalog.Get(terrain.definitionId);
            SmokeUi.Click(definition.name);
            yield return null;
            SmokeUi.Click("지형 전개");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장") != null && SmokeUi.FindButton("저장").interactable,
                5, "Terrain presentation did not finish.");
            SmokeUi.Click("저장");
            yield return null;
            var played = ReadBattle();
            Assert.That(played.players[0].hand.Count, Is.EqualTo(initial.players[0].hand.Count - 1));
            Assert.That(played.players[0].terrain.Count, Is.EqualTo(initial.players[0].terrain.Count + 1));
            Assert.That(played.players[0].terrain.Last().definitionId, Is.EqualTo(definition.id));
            Assert.That(played.players[0].Resource(definition.element), Is.EqualTo(initial.players[0].Resource(definition.element) + 1));
            Assert.That(played.randomState, Is.EqualTo(initial.randomState), "Visual/terrain actions must not consume coin RNG.");
            Assert.That(SmokeUi.HasText("내 지형  " + definition.name), Is.True, "The visible board did not show the terrain.");
            var payloadBeforeReload = JsonUtility.ToJson(played);
            yield return SmokeUi.Capture("FantasyCards", "02-terrain-battle");

            SmokeUi.Click("타이틀");
            yield return null;
            SmokeUi.AssertVisibleKorean("별빛의 서약");
            Assert.That(SmokeUi.FindButton("이어하기").interactable, Is.True);
            yield return SmokeUi.LoadBoot(); // Destroy the app; do not continue using its original in-memory engine.
            SmokeUi.Click("이어하기");
            yield return null;
            Assert.That(SmokeUi.HasText("내 지형  " + definition.name), Is.True);
            SmokeUi.Click("저장"); // Serialize the recreated engine, proving the UI restored live state.
            yield return null;
            Assert.That(JsonUtility.ToJson(ReadBattle()), Is.EqualTo(payloadBeforeReload));
            yield return SmokeUi.Capture("FantasyCards", "03-continued-battle");
            SmokeUi.AssertNoMissingScripts();
            LogAssert.NoUnexpectedReceived();
        }

        static BattleState ReadBattle()
        {
            Assert.That(PersistentSaves.TryLoad("fantasy-battle", "starlight-oath", 1, CardCatalog.Version,
                out var payload, out var error), Is.True, error);
            var state = JsonUtility.FromJson<BattleState>(payload);
            Assert.That(state, Is.Not.Null);
            return state;
        }
    }
}
