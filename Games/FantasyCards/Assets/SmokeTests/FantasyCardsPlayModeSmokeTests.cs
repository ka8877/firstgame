using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        IDisposable preferenceScope;
        MemorySaveBackend backend;

        [UnitySetUp]
        public IEnumerator EnterGame()
        {
            // Avoid running a previously open game scene before test storage is installed.
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
            // Runtime quit/pause handlers must disappear before real storage is restored.
            yield return SmokeUi.UnloadBoot();
            preferenceScope?.Dispose(); preferenceScope = null;
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

            yield return SmokeUi.StartDemoBattle();
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

        [UnityTest]
        public IEnumerator NewDemoBattlePreservesPreviousVersionSlotByteForByte()
        {
            const string oldSlot = "fantasy-battle", oldVersion = "starlight-0.1.0";
            var oldBattle = new BattleEngine(812).State.Copy();
            oldBattle.schemaVersion = 1; oldBattle.contentVersion = oldVersion;
            Assert.That(PersistentSaves.TrySave(oldSlot, "starlight-oath", 1, oldVersion,
                JsonUtility.ToJson(oldBattle), out var error), Is.True, error);
            string oldBytes = backend.Read(oldSlot);
            yield return SmokeUi.LoadBoot();
            Assert.That(SmokeUi.HasText("이전 체험판 저장은 보관했습니다"), Is.True);
            Assert.That(SmokeUi.FindButton("이어하기").interactable, Is.False, "An old-version slot must not be offered as the new demo save.");

            yield return SmokeUi.StartDemoBattle("lyria", "forest", 0);
            SmokeUi.Click("저장");
            yield return null;
            Assert.That(backend.Read(oldSlot), Is.EqualTo(oldBytes), "Starting the new demo must leave the previous save byte-for-byte intact.");
            Assert.That(backend.Exists("fantasy-battle-demo-v2"), Is.True);
            var fresh = ReadBattle();
            Assert.That(fresh.schemaVersion, Is.EqualTo(2));
            Assert.That(fresh.contentVersion, Is.EqualTo(CardCatalog.Version));
            Assert.That(fresh.players[0].leaderId, Is.EqualTo("lyria"));
            Assert.That(fresh.players[0].deckId, Is.EqualTo("forest"));
            SmokeUi.Click("타이틀");
            yield return null;
            yield return SmokeUi.LoadBoot();
            Assert.That(backend.Read(oldSlot), Is.EqualTo(oldBytes));
            Assert.That(SmokeUi.FindButton("이어하기").interactable, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ContinuingCustomBattlePreservesNewerEditorDeckForTheNextDuel()
        {
            const string deckSlot = "fantasy-deck-demo-v2";
            var deckA = DemoCatalog.GetDeck("forest").cards;
            var deckB = DemoCatalog.GetDeck("tides").cards;
            Assert.That(deckA.OrderBy(id => id), Is.Not.EqualTo(deckB.OrderBy(id => id)));

            // Save A through the real editor, then start and save a custom battle using A.
            yield return SaveStarterAsEditorDeck("forest");
            Assert.That(ReadEditorDeck(), Is.EqualTo(deckA));
            yield return StartCustomBattle("forest");
            SmokeUi.Click("저장");
            yield return null;
            var battleA = ReadBattle();
            Assert.That(battleA.players[0].deckId, Is.EqualTo("custom"));
            AssertBattleCards(battleA, deckA);
            string battlePayloadA = JsonUtility.ToJson(battleA);
            SmokeUi.Click("타이틀");
            yield return null;

            // Later editing belongs to its own slot and must survive restoring the older match.
            yield return SaveStarterAsEditorDeck("tides");
            Assert.That(ReadEditorDeck(), Is.EqualTo(deckB));
            string editorBytesB = backend.Read(deckSlot);
            Assert.That(JsonUtility.ToJson(ReadBattle()), Is.EqualTo(battlePayloadA));
            yield return SmokeUi.LoadBoot();
            SmokeUi.Click("이어하기");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장") != null && SmokeUi.FindButton("저장").interactable,
                8, "Continuing the custom battle did not release input.");
            SmokeUi.Click("저장");
            yield return null;
            Assert.That(JsonUtility.ToJson(ReadBattle()), Is.EqualTo(battlePayloadA), "Continue must retain every card zone and RNG value from battle A.");
            Assert.That(backend.Read(deckSlot), Is.EqualTo(editorBytesB), "Continue must not rewrite the independently saved editor deck.");
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            var editorField = typeof(FantasyCardsApp).GetField("customDeck", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(editorField, Is.Not.Null);
            Assert.That((IEnumerable<string>)editorField.GetValue(app), Is.EqualTo(deckB),
                "Continue must not replace the in-memory editor deck with the older battle's cards.");

            SmokeUi.Click("타이틀");
            yield return null;
            yield return StartCustomBattle("tides");
            SmokeUi.Click("저장");
            yield return null;
            var next = ReadBattle();
            Assert.That(next.players[0].deckId, Is.EqualTo("custom"));
            Assert.That(next.players[0].leaderId, Is.EqualTo("lyria"));
            Assert.That(next.encounterIndex, Is.EqualTo(0));
            AssertBattleCards(next, deckB);
            Assert.That(backend.Read(deckSlot), Is.EqualTo(editorBytesB), "Starting the next duel must preserve the saved editor deck.");
            LogAssert.NoUnexpectedReceived();
        }

        static IEnumerator SaveStarterAsEditorDeck(string starterId)
        {
            SmokeUi.Click("새로운 결투"); yield return null;
            SmokeUi.Click(DemoCatalog.GetLeader("lyria").name); yield return null;
            SmokeUi.Click("기본 덱 선택"); yield return null;
            SmokeUi.Click(DemoCatalog.GetDeck(starterId).name); yield return null;
            SmokeUi.Click("리더 선택"); yield return null;
            SmokeUi.Click("타이틀"); yield return null;
            SmokeUi.Click("덱 구성 · 24장"); yield return null;
            SmokeUi.Click("기본 덱 복원"); yield return null;
            SmokeUi.Click("덱 저장"); yield return null;
        }

        static IEnumerator StartCustomBattle(string starterId)
        {
            SmokeUi.Click("새로운 결투"); yield return null;
            SmokeUi.Click(DemoCatalog.GetLeader("lyria").name); yield return null;
            SmokeUi.Click("기본 덱 선택"); yield return null;
            SmokeUi.Click(DemoCatalog.GetDeck(starterId).name); yield return null;
            SmokeUi.Click("내 편집 덱 사용"); yield return null;
            Assert.That(SmokeUi.FindButton("편집 덱 선택됨"), Is.Not.Null);
            SmokeUi.Click("상대 선택"); yield return null;
            SmokeUi.Click(DemoCatalog.GetEncounter(0).name); yield return null;
            yield return SmokeUi.StartPreparedBattle();
        }

        static void AssertBattleCards(BattleState state, IEnumerable<string> expected)
        {
            var player = state.players[0];
            Assert.That(player.terrain, Is.Empty);
            Assert.That(player.units, Is.Empty);
            Assert.That(player.discard, Is.Empty);
            Assert.That(player.deck.Concat(player.hand).Select(card => card.definitionId).OrderBy(id => id),
                Is.EqualTo(expected.OrderBy(id => id)), "A newly started custom battle must contain exactly the chosen editor deck.");
        }

        [Serializable] sealed class EditorDeckPayload { public List<string> cards = new List<string>(); }
        static List<string> ReadEditorDeck()
        {
            Assert.That(PersistentSaves.TryLoad("fantasy-deck-demo-v2", "starlight-oath", 1, CardCatalog.Version,
                out var payload, out var error), Is.True, error);
            var deck = JsonUtility.FromJson<EditorDeckPayload>(payload);
            Assert.That(deck, Is.Not.Null);
            DemoCatalog.ValidatePlayableDeck(deck.cards);
            return deck.cards;
        }

        static BattleState ReadBattle()
        {
            Assert.That(PersistentSaves.TryLoad("fantasy-battle-demo-v2", "starlight-oath", 1, CardCatalog.Version,
                out var payload, out var error), Is.True, error);
            var state = JsonUtility.FromJson<BattleState>(payload);
            Assert.That(state, Is.Not.Null);
            BattleEngine.Validate(state);
            return state;
        }
    }
}
