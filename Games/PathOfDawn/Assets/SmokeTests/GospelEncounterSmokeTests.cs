using System;
using System.Collections;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Live-world dialogue, unchanged opening, ordinary input and durable dialogue cursor.</summary>
    public sealed class GospelEncounterSmokeTests
    {
        IDisposable storage;
        MemorySaveBackend backend;
        Keyboard keyboard;
        Touchscreen addedTouch;
        InputSettings previousInputs, testInputs;
        bool hadSlot, hadMute, hadVolume, hadVoice;
        string oldSlot, attempt;
        int oldMute, oldVoice;
        float oldVolume;
        GospelPilgrimageApp App => UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageApp>();
        GospelStorybookAudio Audio => App.GetComponent<GospelStorybookAudio>();

        IEnumerator Click(string text) { SmokeUi.Click(text); yield return null; yield return null; }
        IEnumerator Shot(string name, int width = 1280, int height = 720) =>
            SmokeUi.Capture("PathOfDawn", "g1-" + attempt + "-" + name, width, height, settleSeconds: .35f);
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static float Planar(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        string VisibleText() => string.Join("\n", UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Where(t => t.gameObject.activeInHierarchy).Select(t => t.text));

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend = new MemorySaveBackend(); backend.WriteAtomic("g1-preserved-player", "original player bytes");
            storage = PersistentSaves.UseBackendForTesting(backend);
            hadSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot"); oldSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            hadMute = PlayerPrefs.HasKey("GOSPEL.music.muted"); oldMute = PlayerPrefs.GetInt("GOSPEL.music.muted", 0);
            hadVolume = PlayerPrefs.HasKey("GOSPEL.music.volume"); oldVolume = PlayerPrefs.GetFloat("GOSPEL.music.volume", .5f);
            hadVoice = PlayerPrefs.HasKey("GOSPEL.voice.enabled"); oldVoice = PlayerPrefs.GetInt("GOSPEL.voice.enabled", 1);
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot"); PlayerPrefs.SetInt("GOSPEL.music.muted", 0);
            PlayerPrefs.SetFloat("GOSPEL.music.volume", .5f); PlayerPrefs.SetInt("GOSPEL.voice.enabled", 1);
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
            if (addedTouch != null && addedTouch.added) InputSystem.RemoveDevice(addedTouch);
            if (previousInputs != null) InputSystem.settings = previousInputs;
            storage?.Dispose();
            if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", oldSlot); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            if (hadMute) PlayerPrefs.SetInt("GOSPEL.music.muted", oldMute); else PlayerPrefs.DeleteKey("GOSPEL.music.muted");
            if (hadVolume) PlayerPrefs.SetFloat("GOSPEL.music.volume", oldVolume); else PlayerPrefs.DeleteKey("GOSPEL.music.volume");
            if (hadVoice) PlayerPrefs.SetInt("GOSPEL.voice.enabled", oldVoice); else PlayerPrefs.DeleteKey("GOSPEL.voice.enabled");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (testInputs != null && InputSystem.settings != testInputs) UnityEngine.Object.DestroyImmediate(testInputs);
        }

        IEnumerator StartOpening()
        {
            Assert.That(App, Is.Not.Null); Audio.NotifyUserGesture();
            yield return Click("새 여정 시작");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("story"));
        }

        IEnumerator ReachFirstField()
        {
            yield return StartOpening();
            int guard = 0;
            while (App.Chronicle.CurrentQuest.IsPrologue && guard++ < 60)
            {
                Assert.That(App.Ui.CurrentView, Is.EqualTo("story"));
                yield return Click("이야기 계속");
            }
            Assert.That(guard, Is.EqualTo(51), "The unchanged opening still advances through its actual51 story turns.");
            Assert.That(App.Chronicle.CurrentQuest.Id, Is.EqualTo("nazareth-departure"));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(11));
            Assert.That(App.Ui.CurrentView, Is.EqualTo("chapter"));
            yield return Click("이야기 속으로");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("journey"));
        }

        IEnumerator ReachNpc()
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (!App.CanInteract && Time.realtimeSinceStartup < deadline)
            {
                var delta = App.ObjectivePosition - App.Player.Position;
                var keys = new System.Collections.Generic.List<Key> { Key.LeftShift };
                if (Mathf.Abs(delta.x) > .20f) keys.Add(delta.x > 0 ? Key.D : Key.A);
                if (Mathf.Abs(delta.z) > .20f) keys.Add(delta.z > 0 ? Key.W : Key.S);
                Keys(keys.ToArray()); yield return new WaitForSecondsRealtime(.06f);
            }
            Keys(); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(App.CanInteract, Is.True, "Real XZ steering must reach the current physical NPC.");
        }

        void AssertFieldPresentation()
        {
            Assert.That(App.Ui.CurrentView, Is.EqualTo("story"));
            Assert.That(App.Chronicle.CurrentQuest.IsPrologue, Is.False);
            Assert.That(App.IsModal, Is.True); Assert.That(App.Player.Enabled, Is.False);
            Assert.That(App.CameraRig.InputEnabled, Is.False);
            Assert.That(App.World.gameObject.activeInHierarchy, Is.True);
            Assert.That(App.SceneCamera.isActiveAndEnabled, Is.True);
            Assert.That(GameObject.Find("Field conversation context"), Is.Not.Null);
            Assert.That(GameObject.Find("Story scene illustration"), Is.Null);
            Assert.That(GameObject.Find("Story location gradient"), Is.Null);
            Assert.That(GameObject.Find("Atmospheric menu shade"), Is.Null);
            Assert.That(SmokeUi.HasText(App.Chronicle.CurrentStoryBeat.Text), Is.True);
            Assert.That(SmokeUi.HasText(App.Chronicle.CurrentStoryReference), Is.True);
            var fullCover = App.Ui.CanvasView.GetComponentsInChildren<Graphic>()
                .Where(g => g.gameObject.activeInHierarchy && g.color.a > .15f &&
                    g.rectTransform.rect.width >= 1100 && g.rectTransform.rect.height >= 600).ToArray();
            Assert.That(fullCover, Is.Empty, "The new field conversation cannot hide the live encounter under a replacement full-screen painting or veil.");
            var person = App.Actor.GetComponentInChildren<SpriteRenderer>();
            Assert.That(person, Is.Not.Null); Assert.That(person.enabled && person.gameObject.activeInHierarchy && person.sprite != null, Is.True);
            var npc = GameObject.Find("Encounter · " + App.Chronicle.CurrentQuest.NpcName);
            Assert.That(npc, Is.Not.Null);
            Assert.That(npc.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled && r.gameObject.activeInHierarchy && r.sprite != null), Is.True);
            Assert.That(App.SceneCamera.WorldToViewportPoint(person.bounds.center).z, Is.GreaterThan(0));
        }

        [UnityTest] public IEnumerator IllustratedBirthStillRetainsItsVoicedLineAcrossTheBible()
        {
            yield return StartOpening();
            var painting = GameObject.Find("Story scene illustration"); Assert.That(painting, Is.Not.Null);
            var raw = painting.GetComponent<RawImage>(); Assert.That(raw, Is.Not.Null); Assert.That(raw.texture, Is.Not.Null);
            Assert.That(raw.rectTransform.rect.size, Is.EqualTo(new Vector2(1280, 720)));
            Assert.That(GameObject.Find("Field conversation context"), Is.Null);
            var key = App.Chronicle.StoryVoiceKey; var text = App.Chronicle.CurrentStoryBeat.Text;
            yield return SmokeUi.WaitUntil(() => App.Voice.IsPlaying && App.Voice.CurrentKey == key &&
                App.Voice.Source != null && App.Voice.Source.isPlaying && App.Voice.Source.time > .03f,
                8, "Actual opening narration must still play its matching visible line.");
            yield return Shot("opening-cinematic-preserved");
            App.ShowBible(); yield return null;
            Assert.That(App.Voice.IsPlaying || App.Voice.IsPending, Is.False);
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key)); Assert.That(App.Chronicle.DiscoveredCount, Is.Zero);
            yield return Click("여정으로");
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key)); Assert.That(SmokeUi.HasText(text), Is.True);
            Assert.That(GameObject.Find("Story scene illustration"), Is.Not.Null);
        }

        [UnityTest] public IEnumerator FieldConversationPreservesItsPhysicalEncounterAndSaveCursorAcrossBookAndReload()
        {
            yield return ReachFirstField(); yield return ReachNpc();
            var before = App.Player.Position; App.Interact(); yield return null; yield return null;
            AssertFieldPresentation();
            string key = App.Chronicle.StoryVoiceKey, text = App.Chronicle.CurrentStoryBeat.Text;
            int found = App.Chronicle.DiscoveredCount;
            var stale = SmokeUi.FindButton("이야기 계속"); Assert.That(stale, Is.Not.Null);
            yield return Shot("joseph-field-dialogue"); yield return Shot("joseph-field-dialogue-touch-layout", 844, 390);
            Keys(Key.D); yield return new WaitForSecondsRealtime(.18f); Keys(); yield return null;
            Assert.That(Planar(App.Player.Position, before), Is.LessThan(.04f), "Conversation must block ordinary movement while showing the world.");
            App.ShowBible(); yield return null;
            Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"));
            Assert.That(App.Voice.IsPlaying || App.Voice.IsPending, Is.False);
            stale.onClick.Invoke(); yield return null;
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key), "A hidden old conversation callback cannot advance the book's story.");
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(found));
            foreach (var future in GospelChronicleCatalog.Readings.Where(r => !App.Chronicle.HasReading(r.Id)))
                Assert.That(VisibleText(), Does.Not.Contain(future.Text));
            yield return Click("여정으로"); AssertFieldPresentation();
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key));
            Assert.That(App.Save(false), Is.True); string slot = App.ActiveSaveSlot;
            yield return SmokeUi.LoadBoot(); Audio.NotifyUserGesture(); yield return Click("이어하기");
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(slot)); Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(found));
            Assert.That(Planar(App.Player.Position, before), Is.LessThan(.10f));
            Assert.That(SmokeUi.HasText(text), Is.True); AssertFieldPresentation();
            yield return Shot("joseph-field-dialogue-reloaded");
            yield return Click("이야기 계속");
            Assert.That(App.Chronicle.StoryVoiceKey, Is.Not.EqualTo(key));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(found), "Joseph's non-awarding event cannot invent a Scripture acquisition.");
            Assert.That(backend.Read("g1-preserved-player"), Is.EqualTo("original player bytes"));
        }

        [UnityTest] public IEnumerator EncounterHudDirectionUsesTheActualCameraAndKeepsInteractionState()
        {
            var journey = GospelJourney.NewChronicleJourney();
            App.Ui.ShowHud(journey, App.Actor.CurrentSprite);
            var player = App.Player.Position;
            var right = Vector3.ProjectOnPlane(App.SceneCamera.transform.right, Vector3.up).normalized;
            var target = player + right * 6;
            yield return new WaitForSecondsRealtime(.12f);
            App.Ui.UpdateHud(journey, player, target, false, App.SceneCamera);
            Assert.That(VisibleText(), Does.Contain("화면 오른쪽"));
            Assert.That(SmokeUi.FindButton("대화").interactable, Is.False);
            yield return new WaitForSecondsRealtime(.12f);
            var cameraPosition = App.SceneCamera.transform.position; var cameraRotation = App.SceneCamera.transform.rotation;
            try
            {
                App.SceneCamera.transform.RotateAround(player, Vector3.up, 180);
                App.Ui.UpdateHud(journey, player, target, false, App.SceneCamera);
                Assert.That(VisibleText(), Does.Contain("화면 왼쪽"), "Direction must follow the visible camera rather than fixed world X.");
            }
            finally { App.SceneCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation); }
            yield return new WaitForSecondsRealtime(.12f);
            App.Ui.UpdateHud(journey, player, player, true, App.SceneCamera);
            Assert.That(VisibleText(), Does.Contain("대화 가능"));
            Assert.That(VisibleText(), Does.Contain(Touchscreen.current == null ? "[E]" : "[대화]"));
            Assert.That(SmokeUi.FindButton("대화").interactable, Is.True);
            if (Touchscreen.current == null) addedTouch = InputSystem.AddDevice<Touchscreen>();
            App.Ui.ShowHud(journey, App.Actor.CurrentSprite); yield return new WaitForSecondsRealtime(.12f);
            App.Ui.UpdateHud(journey, player, player, true, App.SceneCamera);
            Assert.That(VisibleText(), Does.Contain("대화 가능").And.Contain("[대화]"));
            var pad = GameObject.Find("Two axis touch movement"); Assert.That(pad, Is.Not.Null); Assert.That(pad.activeInHierarchy, Is.True);
            Assert.That(SmokeUi.FindButton("대화").interactable, Is.True);
        }
    }
}
