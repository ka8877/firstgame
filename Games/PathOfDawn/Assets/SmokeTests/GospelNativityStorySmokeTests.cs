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
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Actual App/UI, physical NPC travel, new detail cursor and complete retained Bible.</summary>
    public sealed class GospelNativityStorySmokeTests
    {
        IDisposable storage;
        MemorySaveBackend backend;
        Keyboard keyboard;
        InputSettings previousInputs, testInputs;
        bool hadSlot, hadMute, hadVolume, hadVoice;
        string previousSlot, attempt;
        int previousMute, previousVoice;
        float previousVolume;
        GospelPilgrimageApp App => UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageApp>();
        GospelStorybookAudio Audio => App.GetComponent<GospelStorybookAudio>();
        IEnumerator Click(string label) { SmokeUi.Click(label); yield return null; yield return null; }
        IEnumerator Shot(string name, int width = 1280, int height = 720) =>
            SmokeUi.Capture("PathOfDawn", "nativity-" + attempt + "-" + name, width, height, settleSeconds: .35f);
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static float Planar(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        void AssertCarriedLampBounds()
        {
            var carried = GameObject.Find("Equipped travel lamp · preserved authored mesh and light");
            Assert.That(carried, Is.Not.Null); Assert.That(carried.activeInHierarchy, Is.True);
            var meshes = carried.GetComponentsInChildren<MeshRenderer>(); Assert.That(meshes.Length, Is.GreaterThan(0));
            var bounds = meshes[0].bounds; foreach (var renderer in meshes.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            var body = App.Actor.GetComponentInChildren<SpriteRenderer>(); Assert.That(body, Is.Not.Null);
            Assert.That(longest, Is.InRange(.15f, .23f)); Assert.That(longest, Is.LessThan(body.bounds.size.y * .25f));
            Assert.That(Vector3.Dot(carried.transform.up, Vector3.up), Is.GreaterThan(.999f));
            var flame = carried.GetComponentInChildren<Light>(); Assert.That(flame, Is.Not.Null);
            Assert.That(flame.transform.localPosition.y, Is.InRange(.03f, .12f));
        }

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend = new MemorySaveBackend(); backend.WriteAtomic("preserved-player", "original player bytes");
            storage = PersistentSaves.UseBackendForTesting(backend);
            hadSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot"); previousSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            hadMute = PlayerPrefs.HasKey("GOSPEL.music.muted"); previousMute = PlayerPrefs.GetInt("GOSPEL.music.muted", 0);
            hadVolume = PlayerPrefs.HasKey("GOSPEL.music.volume"); previousVolume = PlayerPrefs.GetFloat("GOSPEL.music.volume", .5f);
            hadVoice = PlayerPrefs.HasKey("GOSPEL.voice.enabled"); previousVoice = PlayerPrefs.GetInt("GOSPEL.voice.enabled", 1);
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot"); PlayerPrefs.SetInt("GOSPEL.music.muted", 0); PlayerPrefs.SetFloat("GOSPEL.music.volume", .5f);
            PlayerPrefs.SetInt("GOSPEL.voice.enabled", 1);
            previousInputs = InputSystem.settings; testInputs = UnityEngine.Object.Instantiate(previousInputs);
            testInputs.hideFlags = HideFlags.HideAndDontSave;
            testInputs.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputs.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings = testInputs;
            keyboard = InputSystem.AddDevice<Keyboard>(); yield return SmokeUi.LoadBoot(); yield return new WaitForSecondsRealtime(.25f);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Keys(); yield return SmokeUi.UnloadBoot();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousInputs != null) InputSystem.settings = previousInputs;
            storage?.Dispose();
            if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", previousSlot); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            if (hadMute) PlayerPrefs.SetInt("GOSPEL.music.muted", previousMute); else PlayerPrefs.DeleteKey("GOSPEL.music.muted");
            if (hadVolume) PlayerPrefs.SetFloat("GOSPEL.music.volume", previousVolume); else PlayerPrefs.DeleteKey("GOSPEL.music.volume");
            if (hadVoice) PlayerPrefs.SetInt("GOSPEL.voice.enabled", previousVoice); else PlayerPrefs.DeleteKey("GOSPEL.voice.enabled");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (testInputs != null && InputSystem.settings != testInputs) UnityEngine.Object.DestroyImmediate(testInputs);
        }

        string VisibleText() => string.Join("\n", UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Where(t => t.gameObject.activeInHierarchy).Select(t => t.text));

        IEnumerator ObserveActualAppVoice(string key)
        {
            Assert.That(App.Voice, Is.Not.Null);
            Assert.That(App.Voice.ManifestLineCount, Is.EqualTo(28));
            yield return SmokeUi.WaitUntil(() => App.Voice.IsPlaying && !App.Voice.IsPending &&
                App.Voice.CurrentKey == key && App.Voice.LastStartedKey == key &&
                App.Voice.Source != null && App.Voice.Source.isPlaying && App.Voice.Source.time > .03f,
                8, "The actual visible App story must start its matching voice clip.",
                () => "view=" + App.Ui.CurrentView + " requested=" + App.Voice.CurrentKey +
                    " started=" + App.Voice.LastStartedKey + " pending=" + App.Voice.IsPending);
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key));
            Assert.That(App.Voice.Source.clip, Is.Not.Null);
            Assert.That(App.Voice.Source.pitch, Is.EqualTo(1).Within(.0001f));
        }

        void AssertActualNativityPanel(string key)
        {
            Assert.That(App.Chronicle.CurrentStoryScene.ArtKey, Does.EndWith(key));
            var illustrated = GameObject.Find("Story scene illustration"); Assert.That(illustrated, Is.Not.Null);
            var image = illustrated.GetComponent<RawImage>(); Assert.That(image, Is.Not.Null);
            var atlas = Resources.Load<Texture2D>("Art/GospelNativity/opening-scenes");
            Assert.That(atlas, Is.Not.Null); Assert.That(image.texture, Is.SameAs(atlas));
            float x = key == "nativity-visitation" || key == "nativity-magi" ? .5f : 0;
            float y = key == "nativity-annunciation" || key == "nativity-visitation" ? .5f : 0;
            Assert.That(image.uvRect.xMin, Is.GreaterThanOrEqualTo(x));
            Assert.That(image.uvRect.xMax, Is.LessThanOrEqualTo(x + .5f));
            Assert.That(image.uvRect.yMin, Is.GreaterThanOrEqualTo(y));
            Assert.That(image.uvRect.yMax, Is.LessThanOrEqualTo(y + .5f));
        }

        IEnumerator ShowActualAcquiredPage(GospelReading reading, bool capture = false)
        {
            int count = App.Chronicle.DiscoveredCount;
            string cursor = App.Chronicle.StoryVoiceKey;
            int index = App.Chronicle.BookPages.Select((p, i) => new { p, i }).Single(x => x.p.ReadingId == reading.Id).i;
            App.Ui.FocusBiblePage(index); App.ShowBible(); yield return null;
            Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"));
            var leaves = new[] { GameObject.Find("Left Scripture leaf"), GameObject.Find("Right Scripture leaf") };
            Assert.That(leaves.All(l => l != null), Is.True);
            Assert.That(leaves.SelectMany(l => l.GetComponentsInChildren<TMP_Text>()).Any(t => t.text == reading.Text), Is.True,
                "The actual physical leaf must retain the exact complete verified passage: " + reading.Id);
            foreach (var future in GospelChronicleCatalog.Readings.Where(r => !App.Chronicle.HasReading(r.Id)))
                Assert.That(VisibleText(), Does.Not.Contain(future.Text), "A later full passage leaked into the current Bible.");
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(count));
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(cursor), "Opening the Bible cannot consume a story detail.");
            if (capture)
            {
                yield return Shot("first-inscribed-second-blank"); yield return Shot("first-inscribed-second-blank-mobile", 844, 390);
                yield return Click("차례"); Assert.That(App.Ui.CurrentView, Is.EqualTo("bible-contents"));
                Assert.That(SmokeUi.HasText(reading.Reference), Is.True);
                var link = GameObject.Find("Discovered contents link " + reading.Id);
                Assert.That(link, Is.Not.Null); var button = link.GetComponent<Button>();
                Assert.That(button, Is.Not.Null); Assert.That(button.IsInteractable(), Is.True);
                foreach (var future in GospelChronicleCatalog.Readings.Where(r => !App.Chronicle.HasReading(r.Id)))
                { Assert.That(VisibleText(), Does.Not.Contain(future.Title)); Assert.That(VisibleText(), Does.Not.Contain(future.Reference)); }
                yield return Shot("contents-first-actual-reference-mobile", 844, 390);
                button.onClick.Invoke(); yield return null; yield return null;
                Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"));
                Assert.That(SmokeUi.HasText(reading.Text), Is.True);
                Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(count));
                Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(cursor));
            }
            yield return Click("여정으로");
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(count));
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(cursor));
        }

        IEnumerator FinishStory(bool captureFirst = false)
        {
            int index = App.Chronicle.QuestIndex, found = App.Chronicle.DiscoveredCount, guard = 0;
            bool earns = App.Chronicle.CurrentQuest.Reading != null, opening = App.Chronicle.CurrentQuest.IsPrologue;
            int noticeBefore = App.Ui.ScriptureNoticeCount, soundBefore = Audio.PlayedCount;
            int count = App.Chronicle.StoryBeatCount;
            while (!App.Chronicle.IsComplete && App.Chronicle.QuestIndex == index && guard++ <= count)
            {
                Assert.That(App.Ui.CurrentView, Is.EqualTo("story"));
                Assert.That(SmokeUi.HasText(App.Chronicle.CurrentStoryBeat.Text), Is.True);
                Assert.That(SmokeUi.HasText(App.Chronicle.CurrentStoryReference), Is.True);
                string key = App.Chronicle.CurrentQuest.Id + ":" + App.Chronicle.StoryBeatIndex;
                Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key));
                if (key == "birth-road-bethlehem:3")
                { AssertActualNativityPanel("nativity-visitation"); yield return Shot("visitation-current-story"); }
                else if (key == "birth-road-bethlehem:4")
                { AssertActualNativityPanel("nativity-joseph"); yield return Shot("joseph-dream-current-story"); }
                else if (key == "birth-nazareth-growth:0")
                { AssertActualNativityPanel("nativity-magi"); yield return Shot("magi-house-current-story"); }
                bool last = App.Chronicle.StoryBeatIndex + 1 == count;
                yield return Click("이야기 계속");
                Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(found + (last && earns ? 1 : 0)),
                    "Only the last actual detail of an event may award its retained passage.");
            }
            Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(index + 1));
            if (!earns) yield break;
            Assert.That(App.LastAcquiredReading, Is.Not.Null);
            var acquired = App.LastAcquiredReading;
            Assert.That(App.Chronicle.HasReading(acquired.Id), Is.True);
            Assert.That(App.Save(false), Is.True);
            if (opening)
            {
                Assert.That(App.Ui.CurrentView, Is.EqualTo("story").Or.EqualTo("chapter"), "Opening discovery must resume the next story, not an acquired-page modal.");
                Assert.That(App.Ui.CurrentView, Is.Not.EqualTo("scripture-acquired"));
                Assert.That(App.Ui.ScriptureNoticeCount, Is.EqualTo(noticeBefore + 1));
                Assert.That(App.Ui.IsScriptureNoticeVisible, Is.True);
                Assert.That(SmokeUi.HasText("성경에 말씀이 추가되었습니다."), Is.True);
                Assert.That(Audio.History.Where(h => h.sequence > soundBefore).Any(h => h.cue == "scripture"), Is.False,
                    "The opening award must not play its former scripture chime.");
                if (captureFirst) { yield return Shot("quiet-first-acquisition-next-story"); yield return Shot("quiet-first-acquisition-mobile", 844, 390); }
                if (App.Chronicle.ChapterId == "temple")
                {
                    Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(11));
                    Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(11));
                    Assert.That(App.Chronicle.Stage, Is.EqualTo(GospelQuestStage.Travel));
                    Assert.That(App.Ui.CurrentView, Is.EqualTo("chapter"), "The actual opening boundary presents the child chapter before any later book navigation.");
                    yield return Shot("child-chapter");
                }
            }
            else
            {
                Assert.That(App.Ui.CurrentView, Is.EqualTo("scripture-acquired"));
                Assert.That(SmokeUi.HasText(acquired.Text), Is.True);
                yield return Click("이야기 계속");
                if (App.Ui.CurrentView == "chapter")
                {
                    Assert.That(App.Chronicle.ChapterId, Is.EqualTo("baptism"));
                    Assert.That(App.Actor.IsAdult, Is.True);
                    yield return Shot("adult-chapter");
                }
            }
            yield return ShowActualAcquiredPage(acquired, captureFirst);
        }

        IEnumerator MoveTo(Vector3 target)
        {
            Assert.That(App.IsModal, Is.False); yield return null;
            float deadline = Time.realtimeSinceStartup + 35, stuck = Time.realtimeSinceStartup;
            Vector3 previous = App.Player.Position;
            while (Planar(App.Player.Position, target) > .78f && Time.realtimeSinceStartup < deadline)
            {
                Vector3 d = target - App.Player.Position;
                var keys = new System.Collections.Generic.List<Key> { Key.LeftShift };
                if (Mathf.Abs(d.x) > .32f) keys.Add(d.x > 0 ? Key.D : Key.A);
                if (Mathf.Abs(d.z) > .32f) keys.Add(d.z > 0 ? Key.W : Key.S);
                Keys(keys.ToArray()); yield return new WaitForSecondsRealtime(.075f);
                if (Planar(previous, App.Player.Position) > .1f) { stuck = Time.realtimeSinceStartup; previous = App.Player.Position; }
                else if (Time.realtimeSinceStartup - stuck > 1.3f)
                {
                    Keys(Key.LeftShift, Mathf.Abs(d.x) > Mathf.Abs(d.z) ? Key.W : Key.D);
                    yield return new WaitForSecondsRealtime(.45f); Keys(); yield return null; stuck = Time.realtimeSinceStartup;
                }
            }
            Keys(); yield return new WaitForSecondsRealtime(.14f);
            Assert.That(Planar(App.Player.Position, target), Is.LessThan(1.5f), "Actual XZ steering did not reach the physical NPC.");
        }

        IEnumerator Hold(Key key, string face, string name)
        {
            var before = App.Player.Position; Keys(key); yield return new WaitForSecondsRealtime(.65f);
            Assert.That(Planar(before, App.Player.Position), Is.GreaterThan(.6f));
            Assert.That(App.Actor.CurrentFacing, Is.EqualTo(face)); Assert.That(App.Actor.CurrentState, Is.EqualTo("Walk"));
            yield return Shot(name); Keys(); yield return new WaitForSecondsRealtime(.16f);
        }

        [UnityTest] public IEnumerator DetailedOpeningResumesQuietlyAndTheEntirePhysicalBibleStillAcquiresNineteenTimes()
        {
            Assert.That(App, Is.Not.Null); SmokeUi.AssertNoMissingScripts();
            Assert.That(App.SceneCamera.GetUniversalAdditionalCameraData().scriptableRenderer.GetType().Name, Is.EqualTo("UniversalRenderer"));
            Assert.That(App.World.GroundTriangleCount, Is.GreaterThan(1000));
            Assert.That(App.World.AuthoredPropCount, Is.GreaterThan(12)); Assert.That(App.World.ArchitecturalMeshCount, Is.GreaterThan(12));
            Assert.That(App.Actor.AnimationFrameCount, Is.EqualTo(4 * App.Actor.FramesPerDirectionCount));
            Assert.That(App.Actor.UsesMeasuredLayout, Is.True);
            yield return Shot("title");
            // Native callback fixtures explicitly unlock audio; real pointer/key
            // browser gesture handling is verified separately in Web runs.
            Audio.NotifyUserGesture(); yield return Click("새 여정 시작");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("story"));
            Assert.That(App.Chronicle.CurrentStoryScene.Title, Is.EqualTo("약속을 들은 마리아"));
            Assert.That(App.Chronicle.StoryBeatCount, Is.EqualTo(10));
            Assert.That(App.Chronicle.CurrentStoryReference, Does.StartWith("누가복음 1:"));
            Assert.That(SmokeUi.HasText(App.Chronicle.CurrentReading.Text), Is.False);
            yield return ObserveActualAppVoice("birth-road-bethlehem:0");
            AssertActualNativityPanel("nativity-annunciation"); yield return Shot("annunciation-opening");
            int firstStart = App.Voice.StartedCount; var firstClip = App.Voice.Source.clip;
            App.ShowBible(); yield return null;
            Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"));
            Assert.That(App.Voice.IsPlaying, Is.False); Assert.That(App.Voice.IsPending, Is.False);
            Assert.That(App.Voice.Source.isPlaying, Is.False);
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo("birth-road-bethlehem:0"));
            yield return Click("여정으로");
            yield return ObserveActualAppVoice("birth-road-bethlehem:0");
            Assert.That(App.Voice.StartedCount, Is.GreaterThan(firstStart), "Returning from the book must actually restart the visible line.");
            var staleButton = SmokeUi.FindButton("이야기 계속");
            staleButton.onClick.Invoke(); staleButton.onClick.Invoke(); yield return null;
            Assert.That(App.Chronicle.StoryBeatIndex, Is.EqualTo(1), "A stale callback cannot consume a second detail inside the same legacy page.");
            Assert.That(App.Chronicle.DialogueIndex, Is.Zero); Assert.That(App.Chronicle.StoryDetailIndex, Is.EqualTo(1));
            Assert.That(App.Chronicle.DiscoveredCount, Is.Zero);
            yield return ObserveActualAppVoice("birth-road-bethlehem:1");
            Assert.That(App.Voice.Source.clip, Is.Not.SameAs(firstClip), "The previous visible line cannot keep playing under the new one.");
            string key = App.Chronicle.StoryVoiceKey, text = App.Chronicle.CurrentStoryBeat.Text;
            Assert.That(App.Save(false), Is.True); string slot = App.ActiveSaveSlot;
            yield return SmokeUi.LoadBoot(); Audio.NotifyUserGesture(); yield return Click("이어하기");
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(slot));
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key)); Assert.That(App.Chronicle.CurrentStoryBeat.Text, Is.EqualTo(text));
            Assert.That(App.Chronicle.StoryDetailIndex, Is.EqualTo(1)); Assert.That(App.Chronicle.DiscoveredCount, Is.Zero);
            yield return Shot("real-mid-detail-continued");
            var hiddenStoryButton = SmokeUi.FindButton("이야기 계속");
            Assert.That(hiddenStoryButton, Is.Not.Null);
            // Keep the managed event so the discarded UI object's destruction
            // cannot hide a stale callback that still targets this same beat.
            var hiddenStoryClick = hiddenStoryButton.onClick;
            int hiddenCount = App.Chronicle.DiscoveredCount;
            App.ShowBible(); yield return null;
            Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"));
            hiddenStoryClick.Invoke(); yield return null;
            Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"), "A discarded story callback must not close the visible Bible.");
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key), "A hidden same-beat story callback must not advance its cursor.");
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(hiddenCount));
            string visible = VisibleText();
            foreach (var reading in GospelChronicleCatalog.Readings)
            { Assert.That(visible, Does.Not.Contain(reading.Title)); Assert.That(visible, Does.Not.Contain(reading.Reference)); Assert.That(visible, Does.Not.Contain(reading.Text)); }
            Assert.That(GameObject.Find("Left Scripture leaf"), Is.Not.Null); Assert.That(GameObject.Find("Right Scripture leaf"), Is.Not.Null);
            yield return Shot("bible-entirely-unwritten"); yield return Shot("bible-entirely-unwritten-mobile", 844, 390);
            yield return Click("표지"); Assert.That(App.Ui.CurrentView, Is.EqualTo("bible-cover"));
            yield return Shot("bible-cover-mobile", 844, 390);
            yield return Click("차례"); Assert.That(App.Ui.CurrentView, Is.EqualTo("bible-contents"));
            foreach (var reading in GospelChronicleCatalog.Readings)
            {
                Assert.That(VisibleText(), Does.Not.Contain(reading.Title)); Assert.That(VisibleText(), Does.Not.Contain(reading.Reference));
                Assert.That(VisibleText(), Does.Not.Contain(reading.Text)); Assert.That(GameObject.Find("Discovered contents link " + reading.Id), Is.Null);
            }
            Assert.That(App.Chronicle.DiscoveredCount, Is.Zero); Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key));
            yield return Shot("contents-hidden-all-nineteen-mobile", 844, 390);
            yield return Click("성경 펼치기"); Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"));
            yield return Click("여정으로");
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(key));
            yield return FinishStory(true);
            int early = 1;
            while (App.Chronicle.CurrentQuest.IsPrologue) { yield return FinishStory(); early++; }
            Assert.That(early, Is.EqualTo(11)); Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(11));
            Assert.That(App.Ui.ScriptureNoticeCount, Is.EqualTo(11));
            // FinishStory verifies the actual chapter screen at its transition,
            // then deliberately opens the Bible. Returning from that book resumes
            // the already configured chapter directly in the playable field.
            Assert.That(App.Ui.CurrentView, Is.EqualTo("journey"));
            Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(11));
            Assert.That(App.Chronicle.CurrentQuest.Id, Is.EqualTo("nazareth-departure"));
            Assert.That(App.Chronicle.ChapterId, Is.EqualTo("temple"));
            Assert.That(App.Chronicle.Stage, Is.EqualTo(GospelQuestStage.Travel));
            Assert.That(App.Chronicle.DialogueIndex, Is.Zero); Assert.That(App.Chronicle.StoryDetailIndex, Is.Zero);
            Assert.That(App.Player.Enabled, Is.True); Assert.That(App.Save(false), Is.True);
            Assert.That(App.Actor.IsAdult, Is.False); Assert.That(App.Journey.PresentationSpace, Is.EqualTo(GospelJourney.PilgrimageSpace));
            yield return Shot("child-field"); yield return Shot("child-field-mobile", 844, 390);
            yield return Hold(Key.D, "Right", "child-walk-right"); yield return Hold(Key.A, "Left", "child-walk-left");
            yield return Hold(Key.W, "Back", "child-walk-back"); yield return Hold(Key.S, "Front", "child-walk-front");
            Vector3 diagonal = App.Player.Position; Keys(Key.W, Key.D, Key.LeftShift); yield return new WaitForSecondsRealtime(.6f);
            Assert.That(App.Player.Position.x - diagonal.x, Is.GreaterThan(.5f)); Assert.That(App.Player.Position.z - diagonal.z, Is.GreaterThan(.5f));
            Assert.That(App.Actor.CurrentState, Is.EqualTo("Run")); yield return Shot("child-diagonal-run"); Keys(); yield return new WaitForSecondsRealtime(.2f);
            var goal = App.ObjectivePosition; var wrongDepth = new Vector3(goal.x, goal.y, goal.z - 5.5f);
            yield return MoveTo(wrongDepth); int originalQuest = App.Chronicle.QuestIndex;
            Assert.That(Mathf.Abs(App.Player.Position.x - goal.x), Is.LessThan(1.5f)); Assert.That(App.CanInteract, Is.False);
            App.Interact(); yield return null; Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(originalQuest)); Assert.That(App.Ui.CurrentView, Is.EqualTo("journey"));
            App.ShowStatus(); yield return null; Assert.That(SmokeUi.HasText("발견한 말씀"), Is.True);
            yield return Shot("actual-character-status-mobile", 844, 390); App.Resume(); yield return null;
            App.ShowInventory(); yield return null; yield return Click("등잔 장착");
            Assert.That(App.Actor.LampEquipped, Is.True); yield return Click("여정으로");
            var lamp = GameObject.Find("Equipped travel lamp · preserved authored mesh and light");
            Assert.That(lamp, Is.Not.Null); Assert.That(Vector3.Dot(lamp.transform.up, Vector3.up), Is.GreaterThan(.999f));
            AssertCarriedLampBounds(); yield return Shot("lamp-equipped-field-mobile", 844, 390);
            bool adultCaptured = false, dialogueResumed = false; int guard = 0;
            while (!App.Chronicle.IsComplete && guard++ < 18)
            {
                if (App.Ui.CurrentView == "chapter") { Assert.That(App.Actor.IsAdult, Is.True); yield return Click("이야기 속으로"); }
                yield return MoveTo(App.ObjectivePosition); Assert.That(App.CanInteract, Is.True);
                if (App.Actor.IsAdult && !adultCaptured) { adultCaptured = true; yield return Shot("adult-field"); }
                App.Interact(); yield return null; Assert.That(App.Ui.CurrentView, Is.EqualTo("story"));
                if (!dialogueResumed)
                {
                    dialogueResumed = true; int quest = App.Chronicle.QuestIndex; string beat = App.Chronicle.StoryVoiceKey;
                    Vector3 saved = App.Player.Position; Assert.That(App.Save(false), Is.True); string savedSlot = App.ActiveSaveSlot;
                    yield return SmokeUi.LoadBoot(); Audio.NotifyUserGesture(); yield return Click("이어하기");
                    Assert.That(App.ActiveSaveSlot, Is.EqualTo(savedSlot)); Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(quest));
                    Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(beat)); Assert.That(Planar(App.Player.Position, saved), Is.LessThan(.1f));
                    yield return Shot("actual-npc-dialogue-xyz-continued");
                }
                yield return FinishStory();
            }
            Assert.That(App.Chronicle.IsComplete, Is.True); Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(19));
            Assert.That(App.Actor.IsAdult, Is.True); Assert.That(App.Journey.LampEquipped, Is.True);
            AssertCarriedLampBounds(); yield return Shot("adult-lamp-equipped-field");
            Assert.That(App.Save(false), Is.True); var pose = App.Player.Position; string finalSlot = App.ActiveSaveSlot;
            yield return Shot("completed-adult-field");
            yield return SmokeUi.LoadBoot(); yield return Click("이어하기");
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(finalSlot)); Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(19));
            Assert.That(App.Actor.IsAdult, Is.True); Assert.That(Planar(App.Player.Position, pose), Is.LessThan(.12f));
            Assert.That(App.Journey.LampEquipped, Is.True); yield return Shot("completed-xyz-continued");
            App.ShowBible(); yield return null; yield return Shot("bible-nineteen-discovered");
            Assert.That(backend.Read("preserved-player"), Is.EqualTo("original player bytes"));
        }

        [UnityTest] public IEnumerator RealPreviousLateralSaveForksWithoutTouchingItsOriginalBytes()
        {
            var legacy = GospelJourney.NewChronicleJourney();
            while (legacy.Chronicle.CurrentQuest.IsPrologue) legacy.Chronicle.AdvanceDialogue();
            legacy.SetPresentationSpace(GospelJourney.LuminousSpace); legacy.SetPlayerPose(-27, .1f, 0, 180);
            const string oldSlot = "old-chronicle-player";
            Assert.That(PersistentSaves.TrySave(oldSlot, GospelJourney.GameId, GospelJourney.SchemaVersion, GospelJourney.ContentVersion,
                GospelSaveCodec.Encode(legacy.CaptureSave()), out var error), Is.True, error);
            string original = backend.Read(oldSlot); int found = legacy.CodexCount;
            PlayerPrefs.SetString("GOSPEL.activeSlot", oldSlot); PlayerPrefs.Save(); yield return SmokeUi.LoadBoot(); yield return Click("이어하기");
            Assert.That(App.MigratedSourceSlot, Is.EqualTo(oldSlot)); Assert.That(App.ActiveSaveSlot, Is.Not.EqualTo(oldSlot));
            Assert.That(backend.Read(oldSlot), Is.EqualTo(original)); Assert.That(App.Journey.CodexCount, Is.EqualTo(found));
            Assert.That(App.Journey.PresentationSpace, Is.EqualTo(GospelJourney.PilgrimageSpace));
            Assert.That(App.World.IsInside(App.Player.Position), Is.True); Assert.That(Mathf.Abs(App.Player.Position.z), Is.GreaterThan(1));
            yield return Shot("legacy-save-preserved-new-free-field");
            Assert.That(App.Save(false), Is.True); Assert.That(backend.Read(oldSlot), Is.EqualTo(original));
        }
    }
}
