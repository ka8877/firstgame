using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using FirstGame.Shared;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace FantasyCards.SmokeTests
{
    // Genuine core outcomes are restored through real UI callbacks.
    // This verifies the result/continue/retry flow, not every battle pointer interaction.
    public sealed class FantasyCardsResultFlowSmokeTests
    {
        const string Slot = "fantasy-battle-demo-v2";
        IDisposable storageScope;
        IDisposable preferenceScope;
        MemorySaveBackend backend;

        [UnitySetUp]
        public IEnumerator EnterGame()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            backend = new MemorySaveBackend();
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
        public IEnumerator GenuineVictoryAndDefeatRestoreResultsAndRetryFreshBattle()
        {
            var outcomes = FindLegalCompletedOutcomes();
            for (int winner = 0; winner <= 1; winner++)
            {
                var completed = outcomes[winner];
                string completedJson = JsonUtility.ToJson(completed);
                Assert.That(PersistentSaves.TrySave(Slot, "starlight-oath", 1,
                    CardCatalog.Version, completedJson, out var error), Is.True, error);
                Assert.That(PersistentSaves.Exists(Slot), Is.True, "Completed save must exist before Boot reload.");
                Assert.That(backend.Exists(Slot), Is.True, "The completed save must reach this fixture's backend.");
                var serviceField = typeof(PersistentSaves).GetField("service", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var serviceBeforeReload = serviceField.GetValue(null);

                // Recreate the app after the save was written: title availability and Continue
                // must come from the serialized completed state, not the simulated engine object.
                int previousAppId = UnityEngine.Object.FindFirstObjectByType<FantasyCards.Presentation.FantasyCardsApp>().GetInstanceID();
                yield return SmokeUi.LoadBoot();
                Assert.That(serviceField.GetValue(null), Is.SameAs(serviceBeforeReload),
                    "Boot reload must not replace the fixture's save service. localBackendHasSave=" + backend.Exists(Slot) +
                    " playing=" + Application.isPlaying + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
                Assert.That(PersistentSaves.Exists(Slot), Is.True, "Reload must preserve the isolated save backend. localBackendHasSave=" + backend.Exists(Slot));
                Assert.That(UnityEngine.Object.FindFirstObjectByType<FantasyCards.Presentation.FantasyCardsApp>().GetInstanceID(), Is.Not.EqualTo(previousAppId),
                    "Boot reload must create a new app instance.");
                var continueButtons = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                    .Where(b => b.gameObject.activeInHierarchy && b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "이어하기")).ToArray();
                Assert.That(continueButtons.Length, Is.EqualTo(1), "Continue buttons after reload: " +
                    string.Join(" | ", continueButtons.Select(b => b.gameObject.scene.name + ":" + b.gameObject.scene.path + ":" + b.interactable)));
                Assert.That(SmokeUi.FindButton("이어하기").interactable, Is.True);
                SmokeUi.Click("이어하기");
                yield return null;
                string resultTitle = winner == 0 ? "서약을 지켰습니다" : "별빛은 다시 떠오릅니다";
                SmokeUi.AssertVisibleKorean(resultTitle);
                Assert.That(SmokeUi.HasText((winner == 0 ? "승리" : "패배") + " · " + completed.turn + "턴"), Is.True);
                string nextAction = winner == 0 ? "다음 결투" : "다시 도전";
                Assert.That(SmokeUi.FindButton(nextAction).interactable, Is.True);
                Assert.That(JsonUtility.ToJson(ReadBattle()), Is.EqualTo(completedJson));
                yield return SmokeUi.Capture("FantasyCards", winner == 0 ? "04-restored-victory" : "05-restored-defeat");

                SmokeUi.Click(nextAction);
                yield return null;
                SmokeUi.AssertVisibleKorean("별빛 시험의 세 결투");
                yield return SmokeUi.StartPreparedBattle();
                var fresh = ReadBattle();
                Assert.That(fresh.phase, Is.EqualTo(BattlePhase.Main));
                Assert.That(fresh.winner, Is.EqualTo(-1));
                Assert.That(fresh.turn, Is.EqualTo(1));
                Assert.That(fresh.activePlayer, Is.EqualTo(0));
                Assert.That(fresh.encounterIndex, Is.EqualTo(winner == 0 ? completed.encounterIndex + 1 : completed.encounterIndex),
                    "Victory must select the next encounter; defeat must retain the current encounter.");
                Assert.That(fresh.players[0].leaderId, Is.EqualTo(completed.players[0].leaderId));
                Assert.That(fresh.players[0].deckId, Is.EqualTo(completed.players[0].deckId));
                var encounter = DemoCatalog.GetEncounter(fresh.encounterIndex);
                Assert.That(fresh.players[1].leaderId, Is.EqualTo(encounter.leaderId));
                Assert.That(fresh.players[1].deckId, Is.EqualTo(encounter.deckId));
                foreach (var player in fresh.players)
                {
                    Assert.That(player.health, Is.EqualTo(fresh.rules.startingHealth));
                    Assert.That(player.units.Count, Is.Zero);
                    Assert.That(player.terrain.Count, Is.Zero);
                    Assert.That(player.hand.Count, Is.EqualTo(fresh.rules.openingHand));
                    Assert.That(player.deck.Count, Is.EqualTo(fresh.rules.deckSize - fresh.rules.openingHand));
                    Assert.That(player.discard, Is.Empty);
                    Assert.That(player.dawn + player.tide, Is.Zero);
                    Assert.That(player.leaderSkillUses, Is.Zero);
                    Assert.That(player.lastLeaderSkillTurn, Is.Zero);
                    Assert.That(player.deck.Concat(player.hand).Select(card => card.definitionId).OrderBy(id => id),
                        Is.EqualTo(DemoCatalog.GetDeck(player.deckId).cards.OrderBy(id => id)), "A fresh battle must contain the chosen complete starter deck.");
                }
                Assert.That(fresh.randomState, Is.Not.Zero, "New matches select a fresh seed and keep their shuffled RNG state.");
                Assert.That(fresh.openingHandOpen, Is.False);
                Assert.That(fresh.players[0].mulliganUsed, Is.True);
                Assert.That(fresh.events.Count(e => e.kind == "shuffle"), Is.EqualTo(2));
                Assert.That(fresh.events.Single(e => e.kind == "mulligan").amount, Is.Zero, "The actual keep-hand callback must confirm the new opening hand.");
                Assert.That(fresh.events.Any(e => e.kind == "result" || e.kind == "leader" || e.kind == "play"), Is.False);
                BattleEngine.Validate(fresh);
                Assert.That(SmokeUi.FindButton("다시 도전"), Is.Null);
                Assert.That(SmokeUi.FindButton("다음 결투"), Is.Null);
                Assert.That(SmokeUi.HasText(resultTitle), Is.False);
                SmokeUi.Click("타이틀");
                yield return null;
            }
            SmokeUi.AssertNoMissingScripts();
            LogAssert.NoUnexpectedReceived();
        }

        static Dictionary<int, BattleState> FindLegalCompletedOutcomes()
        {
            var results = new Dictionary<int, BattleState>();
            // No forced health, winner, concede, or direct phase mutation: both sides take legal AI actions.
            for (uint seed = 1; seed <= 128 && results.Count < 2; seed++)
            foreach (var deck in DemoCatalog.StarterDecks)
            {
                if (results.Count == 2) break;
                var engine = DemoCatalog.CreateBattle(seed, deck.id, deck.recommendedLeaderId, 0);
                int actions = 0;
                while (engine.State.phase != BattlePhase.Complete && actions++ < 2000)
                {
                    int actor = engine.State.phase == BattlePhase.BlockResponse ? 1 - engine.State.activePlayer : engine.State.activePlayer;
                    Assert.That(BattleAi.Step(engine, actor, out var reason), Is.True, reason);
                }
                Assert.That(engine.State.phase, Is.EqualTo(BattlePhase.Complete), "Seed " + seed + " exceeded 2000 legal actions.");
                BattleEngine.Validate(engine.State);
                int winner = engine.State.winner;
                if (winner < 2 && !results.ContainsKey(winner))
                {
                    results.Add(winner, engine.State.Copy());
                    TestContext.Out.WriteLine("RESULT_RESTORE_FIXTURE seed=" + seed + " deck=" + deck.id + " leader=" + deck.recommendedLeaderId + " winner=" + winner + " actions=" + actions + " turn=" + engine.State.turn);
                }
            }
            Assert.That(results.ContainsKey(0) && results.ContainsKey(1), Is.True, "The bounded deterministic seed search must supply a natural victory and defeat.");
            return results;
        }

        static BattleState ReadBattle()
        {
            Assert.That(PersistentSaves.TryLoad(Slot, "starlight-oath", 1, CardCatalog.Version,
                out var payload, out var error), Is.True, error);
            var state = JsonUtility.FromJson<BattleState>(payload);
            BattleEngine.Validate(state);
            return state;
        }
    }
}
