using System;
using System.Collections;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Real encounter input plants a new heading; resumed Reading saves retain theirs.</summary>
    public sealed class GospelFacingSmokeTests
    {
        IDisposable storage;
        MemorySaveBackend backend;
        Keyboard keyboard;
        InputSettings previousInputs, testInputs;
        bool hadSlot;
        string oldSlot, attempt;
        GospelPilgrimageApp App => UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageApp>();

        IEnumerator Click(string text) { SmokeUi.Click(text); yield return null; yield return null; }
        IEnumerator Shot(string name) => SmokeUi.Capture("PathOfDawn", "g2-" + attempt + "-" + name,
            1280, 720, settleSeconds: .15f);
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static float Planar(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        static float TargetYaw(Vector3 from, Vector3 to) => Mathf.Repeat(Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg, 360);

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend = new MemorySaveBackend(); backend.WriteAtomic("g2-preserved-player", "original player bytes");
            storage = PersistentSaves.UseBackendForTesting(backend);
            hadSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot"); oldSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            previousInputs = InputSystem.settings; testInputs = UnityEngine.Object.Instantiate(previousInputs);
            testInputs.hideFlags = HideFlags.HideAndDontSave;
            testInputs.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputs.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings = testInputs;
            keyboard = InputSystem.AddDevice<Keyboard>(); yield return SmokeUi.LoadBoot();
            yield return new WaitForSecondsRealtime(.25f);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Keys(); yield return SmokeUi.UnloadBoot();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousInputs != null) InputSystem.settings = previousInputs;
            storage?.Dispose();
            if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", oldSlot); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (testInputs != null && InputSystem.settings != testInputs) UnityEngine.Object.DestroyImmediate(testInputs);
        }

        IEnumerator ReachFirstField()
        {
            yield return Click("새 여정 시작");
            int guard = 0;
            while (App.Chronicle.CurrentQuest.IsPrologue && guard++ < 60)
            {
                Assert.That(App.Ui.CurrentView, Is.EqualTo("story"));
                yield return Click("이야기 계속");
            }
            Assert.That(guard, Is.EqualTo(51), "Use the unchanged actual opening, not injected progress.");
            Assert.That(App.Chronicle.CurrentQuest.Id, Is.EqualTo("nazareth-departure"));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(11));
            yield return Click("이야기 속으로");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("journey"));
            yield return new WaitForSecondsRealtime(.15f);
        }

        IEnumerator ReachNpc()
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (Planar(App.Player.Position, App.ObjectivePosition) > 1.25f && Time.realtimeSinceStartup < deadline)
            {
                var delta = App.ObjectivePosition - App.Player.Position;
                var keys = new System.Collections.Generic.List<Key> { Key.LeftShift };
                if (Mathf.Abs(delta.x) > .20f) keys.Add(delta.x > 0 ? Key.D : Key.A);
                if (Mathf.Abs(delta.z) > .20f) keys.Add(delta.z > 0 ? Key.W : Key.S);
                Keys(keys.ToArray()); yield return new WaitForSecondsRealtime(.06f);
            }
            Keys(); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(App.CanInteract, Is.True, "Ordinary XZ input must reach the real Joseph encounter.");
            Assert.That(Planar(App.Player.Position, App.ObjectivePosition), Is.LessThan(1.35f));
            Assert.That(App.Player.IsGrounded, Is.True);
        }

        GospelSave ReadSave(string slot)
        {
            Assert.That(PersistentSaves.TryLoad(slot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, out var payload, out var error), Is.True, error);
            return GospelSaveCodec.Decode(payload);
        }

        void AssertReadingPose(GospelSave saved, int quest, string key, int found)
        {
            Assert.That(App.Chronicle.Stage, Is.EqualTo(GospelQuestStage.Reading));
            Assert.That(App.Ui.CurrentView, Is.EqualTo("story")); Assert.That(App.IsModal, Is.True);
            Assert.That(App.Player.Enabled, Is.False); Assert.That(App.CameraRig.InputEnabled, Is.False);
            Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(quest));
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(found));
            Assert.That(App.Journey.PlayerX, Is.EqualTo(saved.playerX));
            Assert.That(App.Journey.PlayerY, Is.EqualTo(saved.playerY));
            Assert.That(App.Journey.PlayerZ, Is.EqualTo(saved.playerZ));
            Assert.That(App.Journey.PlayerYaw, Is.EqualTo(saved.playerYaw));
            Assert.That(Planar(App.Player.Position, new Vector3(saved.playerX, saved.playerY, saved.playerZ)), Is.LessThan(.01f));
            // SetPose already adds .035 metres before ordinary controller ground settlement.
            Assert.That(Mathf.Abs(App.Player.Position.y - saved.playerY), Is.LessThan(.10f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, saved.playerYaw)), Is.LessThan(.01f));
            Assert.That(App.Player.MeasuredSpeed, Is.LessThan(.02f)); Assert.That(App.Player.IsDashing, Is.False);
            Assert.That(App.Actor.CurrentState, Is.EqualTo("Idle")); Assert.That(App.Actor.CurrentFrame, Is.Zero);
        }

        [UnityTest] public IEnumerator AcceptedKeyboardEncounterPlantsFacingBeforeSavingAndKeepsItAcrossBookAndReload()
        {
            yield return ReachFirstField();
            Assert.That(App.CanInteract, Is.False);
            var farPosition = App.Player.Position; float farYaw = App.Player.FacingYaw;
            var farStage = App.Chronicle.Stage; string farSlot = App.ActiveSaveSlot, farBytes = backend.Read(farSlot);
            Keys(Key.E); yield return null; yield return null; Keys(); yield return null;
            Assert.That(App.Chronicle.Stage, Is.EqualTo(farStage)); Assert.That(App.IsModal, Is.False);
            Assert.That(App.Player.Enabled, Is.True, "A rejected distant interaction must not lock the traveler.");
            Assert.That(App.Player.FacingYaw, Is.EqualTo(farYaw));
            Assert.That(Planar(App.Player.Position, farPosition), Is.LessThan(.01f));
            Assert.That(backend.Read(farSlot), Is.EqualTo(farBytes));

            yield return ReachNpc();
            var player = App.Player;
            Keys(Key.A); yield return new WaitForSecondsRealtime(.06f);
            Assert.That(App.CanInteract, Is.True);
            Assert.That(player.MeasuredSpeed, Is.GreaterThan(.25f), "Enter with actual measured movement so stale animation velocity cannot pass the stop assertion.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.FacingYaw, TargetYaw(player.Position, App.ObjectivePosition))), Is.GreaterThan(60),
                "The accepted encounter must visibly turn away from its real approach heading.");
            var npcObject = GameObject.Find("Encounter · " + App.Chronicle.CurrentQuest.NpcName);
            Assert.That(npcObject, Is.Not.Null); var npc = npcObject.transform;
            string slot = App.ActiveSaveSlot; int sequence = player.DashSequence;
            bool observed = false, immediateEnabled = true, immediateDash = true;
            Vector3 acceptedPosition = Vector3.zero, immediateVelocity = Vector3.one;
            float expectedYaw = 0, immediateYaw = 0, immediateSpeed = -1;
            string entryBytes = null;
            // App subscribed first at Start. This observer runs after its successful
            // Interact+Save, before the same Motor.Update resumes its sampled input.
            Action accepted = () =>
            {
                if (App.Chronicle.Stage != GospelQuestStage.Reading) return;
                observed = true; acceptedPosition = player.Position;
                expectedYaw = TargetYaw(acceptedPosition, npc.position);
                immediateYaw = player.FacingYaw; immediateVelocity = player.Velocity;
                immediateSpeed = player.MeasuredSpeed; immediateEnabled = player.Enabled;
                immediateDash = player.IsDashing; entryBytes = backend.Read(slot);
            };
            player.InteractRequested += accepted;
            try
            {
                Assert.That(player.DashReady, Is.True);
                player.RequestDash(); // The same pending request used by the actual UI action.
                Assert.That(player.IsDashing, Is.True);
                Keys(Key.A, Key.E, Key.LeftCtrl); // Opposite movement, interaction and dash in one real input frame.
                yield return SmokeUi.WaitUntil(() => observed, 3, "The actual E key did not accept the nearby encounter.");
                yield return new WaitForSecondsRealtime(.18f); // Keep the movement key held while the world remains visible.
            }
            finally { player.InteractRequested -= accepted; Keys(); }
            yield return new WaitForSecondsRealtime(.14f);
            Assert.That(immediateEnabled, Is.False); Assert.That(immediateDash, Is.False);
            Assert.That(immediateVelocity, Is.EqualTo(Vector3.zero)); Assert.That(immediateSpeed, Is.Zero);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(immediateYaw, expectedYaw)), Is.LessThan(.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.FacingYaw, expectedYaw)), Is.LessThan(.01f),
                "The remainder of Motor.Update must not overwrite the callback's new facing with held input.");
            Assert.That(Planar(player.Position, acceptedPosition), Is.LessThan(.01f),
                "No remaining movement from the accepting input frame may step out of the planted conversation.");
            Assert.That(player.DashSequence, Is.EqualTo(sequence), "Neither a pending nor a same-frame dash may escape the new conversation lock.");

            var saved = ReadSave(slot);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(saved.playerYaw, expectedYaw)), Is.LessThan(.01f),
                "The original Interact save must contain the new heading; do not mask ordering with another test save.");
            Assert.That(Mathf.Abs(saved.playerX - acceptedPosition.x), Is.LessThan(.0001f));
            Assert.That(Mathf.Abs(saved.playerY - acceptedPosition.y), Is.LessThan(.0001f));
            Assert.That(Mathf.Abs(saved.playerZ - acceptedPosition.z), Is.LessThan(.0001f));
            int quest = App.Chronicle.QuestIndex, found = App.Chronicle.DiscoveredCount;
            string key = App.Chronicle.StoryVoiceKey;
            AssertReadingPose(saved, quest, key, found); yield return Shot("joseph-facing-planted");
            App.ShowBible(); yield return null; yield return Click("여정으로");
            yield return new WaitForSecondsRealtime(.14f); AssertReadingPose(saved, quest, key, found);
            Assert.That(backend.Read(slot), Is.EqualTo(entryBytes));
            yield return SmokeUi.LoadBoot(); yield return Click("이어하기");
            yield return new WaitForSecondsRealtime(.14f); AssertReadingPose(saved, quest, key, found);
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(slot)); Assert.That(backend.Read(slot), Is.EqualTo(entryBytes));
            yield return Shot("joseph-facing-reloaded");
            Assert.That(backend.Read("g2-preserved-player"), Is.EqualTo("original player bytes"));
        }

        [UnityTest] public IEnumerator LegacyReadingHeadingPositionAndOriginalBytesSurviveBookAndRepeatedContinue()
        {
            yield return ReachFirstField(); yield return ReachNpc(); yield return Click("대화");
            Assert.That(App.Chronicle.Stage, Is.EqualTo(GospelQuestStage.Reading));
            yield return Click("이야기 계속"); // Retain a noninitial real dialogue cursor as well as its pose.
            Assert.That(App.Chronicle.CurrentQuest.Id, Is.EqualTo("nazareth-departure"));
            Assert.That(App.Chronicle.Stage, Is.EqualTo(GospelQuestStage.Reading));
            var legacy = App.Journey.CaptureSave();
            legacy.playerX = App.Player.Position.x; legacy.playerZ = App.Player.Position.z;
            legacy.playerY = App.World.HeightAt(legacy.playerX, legacy.playerZ);
            var legacyPosition = new Vector3(legacy.playerX, legacy.playerY, legacy.playerZ);
            legacy.playerYaw = Mathf.Repeat(TargetYaw(legacyPosition, App.ObjectivePosition) + 137, 360);
            string slot = App.ActiveSaveSlot, key = App.Chronicle.StoryVoiceKey;
            int quest = App.Chronicle.QuestIndex, found = App.Chronicle.DiscoveredCount;
            // Compatibility fixture only: isolated memory backend, unchanged live
            // quest/cursor/content/space. No developer save or live actor is edited.
            Assert.That(PersistentSaves.TrySave(slot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, GospelSaveCodec.Encode(legacy), out var error), Is.True, error);
            string original = backend.Read(slot); legacy = ReadSave(slot); // Canonical original serialized floats.
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(legacy.playerYaw, TargetYaw(legacyPosition, App.ObjectivePosition))), Is.GreaterThan(120));

            yield return SmokeUi.LoadBoot(); yield return Click("이어하기");
            yield return new WaitForSecondsRealtime(.14f); AssertReadingPose(legacy, quest, key, found);
            Assert.That(backend.Read(slot), Is.EqualTo(original), "Continue must not silently turn or rewrite an old Reading save.");
            yield return Shot("legacy-reading-original-heading");
            App.ShowBible(); yield return null; yield return Click("여정으로");
            yield return new WaitForSecondsRealtime(.14f); AssertReadingPose(legacy, quest, key, found);
            Assert.That(backend.Read(slot), Is.EqualTo(original));
            yield return SmokeUi.LoadBoot(); yield return Click("이어하기");
            yield return new WaitForSecondsRealtime(.14f); AssertReadingPose(legacy, quest, key, found);
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(slot)); Assert.That(backend.Read(slot), Is.EqualTo(original));
            yield return Shot("legacy-reading-original-heading-reloaded");
            Assert.That(backend.Read("g2-preserved-player"), Is.EqualTo("original player bytes"));
        }
    }
}
