using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FantasyCards.Core;
using FantasyCards.Presentation;
using FirstGame.Shared;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace FantasyCards.SmokeTests
{
    public sealed class FantasyCardsSaveFailureSmokeTests
    {
        const string Slot = "fantasy-battle-demo-v2";
        IDisposable storageScope;
        IDisposable preferenceScope;
        FailingSaveBackend backend;
        Keyboard keyboard;
        InputSettings originalInputSettings, fixtureInputSettings;

        sealed class FailingSaveBackend : ISaveBackend
        {
            readonly Dictionary<string, string> slots = new Dictionary<string, string>();
            public bool FailWrites;
            public int WriteAttempts;
            public bool Exists(string slot) => slots.ContainsKey(slot);
            public string Read(string slot) => slots[slot];
            public void WriteAtomic(string slot, string contents)
            {
                WriteAttempts++;
                if (FailWrites) throw new IOException("Injected write failure for smoke test.");
                slots[slot] = contents;
            }
        }

        [UnitySetUp]
        public IEnumerator EnterGame()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            backend = new FailingSaveBackend();
            storageScope = PersistentSaves.UseBackendForTesting(backend);
            preferenceScope = SmokeUi.PreserveDemoPreferences();
            originalInputSettings = InputSystem.settings;
            fixtureInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
            fixtureInputSettings.hideFlags = HideFlags.HideAndDontSave;
            fixtureInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            fixtureInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = fixtureInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SmokeUi.LoadBoot();
        }

        [UnityTearDown]
        public IEnumerator LeaveGame()
        {
            yield return SmokeUi.UnloadBoot();
            preferenceScope?.Dispose(); preferenceScope = null;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
            if (fixtureInputSettings != null) UnityEngine.Object.Destroy(fixtureInputSettings);
            originalInputSettings = fixtureInputSettings = null;
            storageScope?.Dispose(); storageScope = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FailedTitleAndEscapeKeepLiveProgressUntilSaveRetrySucceeds()
        {
            yield return SmokeUi.StartDemoBattle();
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            var liveEngine = ReadEngine(app);
            var savedA = JsonUtility.ToJson(liveEngine.State);
            Assert.That(ReadSavedPayload(), Is.EqualTo(savedA));
            var rawA = backend.Read(Slot);
            var terrain = liveEngine.State.players[0].hand.First(c => CardCatalog.Get(c.definitionId).kind == CardKind.Terrain);
            var definition = CardCatalog.Get(terrain.definitionId);

            // Make real UI progress while persistence fails; never force a core state mutation.
            backend.FailWrites = true;
            SmokeUi.Click(definition.name);
            yield return null;
            SmokeUi.Click("지형 전개");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장") != null && SmokeUi.FindButton("저장").interactable,
                5, "Terrain presentation did not finish.");
            var liveB = JsonUtility.ToJson(liveEngine.State);
            Assert.That(liveB, Is.Not.EqualTo(savedA));
            Assert.That(liveEngine.State.players[0].terrain.Single().definitionId, Is.EqualTo(definition.id));
            AssertFailurePreservesProgress(app, liveEngine, liveB, rawA, definition.name);

            int attempts = backend.WriteAttempts;
            SmokeUi.Click("타이틀");
            yield return null;
            Assert.That(backend.WriteAttempts, Is.GreaterThan(attempts), "Title must attempt to persist current progress.");
            AssertFailurePreservesProgress(app, liveEngine, liveB, rawA, definition.name);

            attempts = backend.WriteAttempts;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(backend.WriteAttempts, Is.GreaterThan(attempts), "Actual Escape input must reach the runtime save path.");
            AssertFailurePreservesProgress(app, liveEngine, liveB, rawA, definition.name);

            backend.FailWrites = false;
            SmokeUi.Click("저장");
            yield return null;
            Assert.That(ReadSavedPayload(), Is.EqualTo(liveB), "A successful retry must persist B, not stale A.");
            Assert.That(SmokeUi.HasText("저장 실패:"), Is.False);
            SmokeUi.Click("타이틀");
            yield return null;
            Assert.That(SmokeUi.FindButton("이어하기").interactable, Is.True);
            int previousAppId = app.GetInstanceID();
            yield return SmokeUi.LoadBoot();
            app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            Assert.That(app.GetInstanceID(), Is.Not.EqualTo(previousAppId));
            SmokeUi.Click("이어하기");
            yield return null;
            Assert.That(JsonUtility.ToJson(ReadEngine(app).State), Is.EqualTo(liveB));
            Assert.That(SmokeUi.HasText("내 지형  " + definition.name), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        void AssertFailurePreservesProgress(FantasyCardsApp app, BattleEngine engine, string liveB, string rawA, string terrainName)
        {
            Assert.That(ReadEngine(app), Is.SameAs(engine), "A failed save must retain the live engine instance.");
            Assert.That(JsonUtility.ToJson(ReadEngine(app).State), Is.EqualTo(liveB));
            Assert.That(backend.Read(Slot), Is.EqualTo(rawA), "The last valid disk payload must remain byte-for-byte intact.");
            Assert.That(SmokeUi.FindButton("저장"), Is.Not.Null, "A failed exit must leave the battle screen available for retry.");
            Assert.That(SmokeUi.FindButton("이어하기"), Is.Null, "Do not offer stale Continue while B is unsaved.");
            Assert.That(SmokeUi.HasText("저장 실패:"), Is.True);
            Assert.That(SmokeUi.HasText("내 지형  " + terrainName), Is.True);
        }

        static BattleEngine ReadEngine(FantasyCardsApp app)
        {
            // Read-only observation of runtime state; gameplay is driven through actual UI/input.
            var field = typeof(FantasyCardsApp).GetField("engine", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var value = field.GetValue(app) as BattleEngine;
            Assert.That(value, Is.Not.Null);
            return value;
        }

        static string ReadSavedPayload()
        {
            Assert.That(PersistentSaves.TryLoad(Slot, "starlight-oath", 1, CardCatalog.Version,
                out var payload, out var error), Is.True, error);
            return payload;
        }
    }
}
