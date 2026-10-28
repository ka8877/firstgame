using System;
using System.Collections;
using System.Linq;
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
    /// <summary>Current 2D presentation: real scene, input, callbacks and save round-trip.</summary>
    public sealed class GospelLuminousSmokeTests
    {
        IDisposable storage;
        MemorySaveBackend backend;
        Keyboard keyboard;
        InputSettings previousInputs, testInputs;
        string previousSlot;
        bool hadSlot;
        string attempt;
        GospelLuminousApp App => UnityEngine.Object.FindFirstObjectByType<GospelLuminousApp>();

        [UnitySetUp]
        public IEnumerator Begin()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT") ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            backend = new MemorySaveBackend();
            storage = PersistentSaves.UseBackendForTesting(backend);
            hadSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot");
            previousSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            previousInputs = InputSystem.settings;
            testInputs = UnityEngine.Object.Instantiate(previousInputs);
            testInputs.hideFlags = HideFlags.HideAndDontSave;
            testInputs.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputs.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testInputs;
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SmokeUi.LoadBoot();
            yield return new WaitForSecondsRealtime(.5f);
        }

        [UnityTearDown]
        public IEnumerator Finish()
        {
            if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return SmokeUi.UnloadBoot();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousInputs != null) InputSystem.settings = previousInputs;
            if (testInputs != null) UnityEngine.Object.Destroy(testInputs);
            storage?.Dispose();
            if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", previousSlot);
            else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CurrentBootHasLayered2DWorldAndReadableTitle()
        {
            Assert.That(App, Is.Not.Null);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<GospelJourneyApp>(), Is.Null);
            Assert.That(App.SceneCamera.orthographic, Is.True);
            Assert.That(App.World, Is.Not.Null);
            Assert.That(App.Player.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(App.Player.GetComponent<Collider2D>(), Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length, Is.GreaterThan(12));
            var lightType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.2D.Runtime", true);
            Assert.That(UnityEngine.Object.FindObjectsByType(lightType, FindObjectsSortMode.None).Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(App.Ui.CurrentView, Is.EqualTo("title"));
            yield return SmokeUi.WaitForVisibleKorean("예수님의 길");
            yield return SmokeUi.Capture("PathOfDawn", "luminous-" + attempt + "-title", 1280, 720);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ActualWalkJumpInventoryAndSaved2DPositionRestore()
        {
            SmokeUi.Click("새 여정 시작");
            yield return null;
            Assert.That(App.InGame && !App.IsModal, Is.True);
            Assert.That(App.Journey.CollectedReadings.Count, Is.Zero, "The visual preview must not mark unread Scripture as read.");
            var start = App.Player.Position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return new WaitForSecondsRealtime(.8f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(App.Player.Position.x - start.x, Is.GreaterThan(.8f), "Actual keyboard input must move the physical actor.");
            yield return SmokeUi.Capture("PathOfDawn", "luminous-" + attempt + "-world", 1280, 720);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            yield return new WaitForSecondsRealtime(.25f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(App.Player.Facing, Is.EqualTo(-1));
            yield return SmokeUi.Capture("PathOfDawn", "luminous-" + attempt + "-world-left", 1280, 720);
            var groundedY = App.Player.Position.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(App.Player.Position.y - groundedY, Is.GreaterThan(.15f), "Jump input must leave the real 2D ground.");
            yield return SmokeUi.Capture("PathOfDawn", "luminous-" + attempt + "-jump", 1280, 720, settleSeconds: 0f);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(Mathf.Abs(App.Player.Position.y - groundedY), Is.LessThan(.15f), "The actual collider must land back on the path.");
            SmokeUi.Click("예수님"); yield return null;
            SmokeUi.Click("소지품"); yield return null;
            SmokeUi.Click("등잔 장착"); yield return null;
            Assert.That(App.Journey.LampEquipped, Is.True);
            yield return SmokeUi.Capture("PathOfDawn", "luminous-" + attempt + "-inventory", 1280, 720);
            SmokeUi.Click("여정으로"); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            SmokeUi.Click("현재 여정 저장"); yield return null;
            float savedX = App.Player.Position.x;
            Assert.That(App.Journey.PresentationSpace, Is.EqualTo(GospelJourney.LuminousSpace));
            Assert.That(backend.Exists(App.ActiveSaveSlot), Is.True);
            yield return SmokeUi.LoadBoot();
            SmokeUi.Click("이어하기"); yield return null;
            Assert.That(App.InGame && !App.IsModal, Is.True);
            Assert.That(App.Player.Position.x, Is.EqualTo(savedX).Within(.08f));
            Assert.That(App.Journey.LampEquipped, Is.True);
            Assert.That(App.Journey.CollectedReadings.Count, Is.Zero);
            yield return SmokeUi.Capture("PathOfDawn", "luminous-" + attempt + "-continued", 1280, 720);
            // Reach the actual authored gateway through ordinary input. The
            // player/scene transform is never assigned by this preview check.
            float runSeconds = Mathf.Clamp((23f - App.Player.Position.x) / App.Player.RunSpeed, 0f, 11f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift));
            yield return new WaitForSecondsRealtime(runSeconds);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(App.Player.Position.x, Is.GreaterThan(18f), "Normal right-run must reach the temple approach.");
            yield return SmokeUi.Capture("PathOfDawn", "luminous-" + attempt + "-temple-gateway", 1280, 720);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
