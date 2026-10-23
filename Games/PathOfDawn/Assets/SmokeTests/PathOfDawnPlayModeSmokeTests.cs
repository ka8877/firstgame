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
using UnityEngine.Playables;

namespace PathOfDawn.SmokeTests
{
    // Editor-only test discovery; the fixture explicitly runs the scene in PlayMode.
    public sealed class PathOfDawnPlayModeSmokeTests
    {
        IDisposable storageScope;
        Keyboard keyboard;
        InputSettings originalInputSettings, fixtureInputSettings;
        bool hadSlot, hadScale, hadVolume, preferencesCaptured;
        string originalSlot;
        float originalScale, originalVolume, originalListenerVolume;

        [UnitySetUp]
        public IEnumerator EnterGame()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            storageScope = PersistentSaves.UseBackendForTesting(new MemorySaveBackend());
            hadSlot = PlayerPrefs.HasKey("POD.activeSlot"); originalSlot = PlayerPrefs.GetString("POD.activeSlot", "");
            hadScale = PlayerPrefs.HasKey("POD.textScale"); originalScale = PlayerPrefs.GetFloat("POD.textScale", 1);
            hadVolume = PlayerPrefs.HasKey("POD.volume"); originalVolume = PlayerPrefs.GetFloat("POD.volume", .65f);
            originalListenerVolume = AudioListener.volume; preferencesCaptured = true;
            // A known slot/settings baseline, while preserving every original key for teardown.
            PlayerPrefs.DeleteKey("POD.activeSlot"); PlayerPrefs.SetFloat("POD.textScale", 1); PlayerPrefs.SetFloat("POD.volume", .65f);
            // Batch editor has no focused GameView. Use a disposable settings clone so queued
            // test keyboard events reach runtime actions without changing the project asset.
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
            // Stop scene scripts before returning either storage or preferences to the user.
            yield return SmokeUi.UnloadBoot();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
            if (fixtureInputSettings != null) UnityEngine.Object.Destroy(fixtureInputSettings);
            originalInputSettings = fixtureInputSettings = null;
            storageScope?.Dispose(); storageScope = null;
            if (preferencesCaptured)
            {
                if (hadSlot) PlayerPrefs.SetString("POD.activeSlot", originalSlot); else PlayerPrefs.DeleteKey("POD.activeSlot");
                if (hadScale) PlayerPrefs.SetFloat("POD.textScale", originalScale); else PlayerPrefs.DeleteKey("POD.textScale");
                if (hadVolume) PlayerPrefs.SetFloat("POD.volume", originalVolume); else PlayerPrefs.DeleteKey("POD.volume");
                PlayerPrefs.Save(); AudioListener.volume = originalListenerVolume; preferencesCaptured = false;
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RealDialogueRewindAlternateOutcomeAndContinuePreserveMemories()
        {
            var app = UnityEngine.Object.FindFirstObjectByType<PathOfDawnApp>();
            Assert.That(app, Is.Not.Null);
            Assert.That(app.EnvironmentPrefab, Is.Not.Null, "Generated environment is not connected.");
            Assert.That(app.JesusPrefab, Is.Not.Null);
            Assert.That(app.TravelerPrefab, Is.Not.Null);
            Assert.That(app.MiriamPrefab, Is.Not.Null);
            yield return SmokeUi.WaitForVisibleKorean("새벽의 길");
            var music = app.GetComponent<BgmPlayer>();
            Assert.That(music, Is.Not.Null, "The actual app must own its background music player.");
            var soundtrack = Resources.Load<AudioClip>("Audio/dawn-on-galilee");
            Assert.That(soundtrack, Is.Not.Null, "The shipped soundtrack must import as an AudioClip.");
            Assert.That(music.Clip, Is.SameAs(soundtrack));
            yield return SmokeUi.WaitUntil(() => soundtrack.loadState == AudioDataLoadState.Loaded,
                10, "The actual Galilee soundtrack did not load.");
            Assert.That(soundtrack.length, Is.InRange(90f, 120f));
            Assert.That(soundtrack.channels, Is.EqualTo(2));
            var musicSources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .Where(source => source.gameObject.activeInHierarchy && source.clip == soundtrack).ToArray();
            Assert.That(musicSources.Length, Is.EqualTo(1), "The soundtrack must not be doubled by duplicate sources.");
            Assert.That(musicSources[0].loop, Is.True);
            SmokeUi.AssertNoMissingScripts();
            yield return SmokeUi.Capture("PathOfDawn", "01-title");
            Assert.That(SmokeUi.FindButton("이어하기").interactable, Is.False);

            SmokeUi.Click("설정 · 저장 가져오기"); yield return null;
            SmokeUi.Click("크게"); yield return null;
            Assert.That(PlayerPrefs.GetFloat("POD.textScale"), Is.EqualTo(1.1f).Within(.001f));
            SmokeUi.Click("줄이기"); yield return null;
            Assert.That(PlayerPrefs.GetFloat("POD.volume"), Is.EqualTo(.55f).Within(.001f));
            yield return SmokeUi.Capture("PathOfDawn", "09-settings-music-1920x1080", 1920, 1080, AssertMusicSettingsLayout);
            yield return SmokeUi.Capture("PathOfDawn", "10-settings-music-693x390", 693, 390, AssertMusicSettingsLayout);
            SmokeUi.Click("타이틀로"); yield return null;
            SmokeUi.Click("새 게임");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("다음 대사 [E]") != null,
                30, "The six-second introduction did not reach dialogue within the software-rendering budget.", () =>
                {
                    var intro = app.GetComponent<PlayableDirector>();
                    return (intro == null ? "No PlayableDirector" :
                        "Timeline state=" + intro.state + " time=" + intro.time + " duration=" + intro.duration) +
                        " | Time.time=" + Time.time + " realTime=" + Time.realtimeSinceStartup + " delta=" + Time.deltaTime;
                });
            yield return DrainDialogueLines();
            Assert.That(ReadSave().world.pointId, Is.EqualTo("shore"));
            yield return WalkForwardWithKeyboard();
            yield return SmokeUi.Capture("PathOfDawn", "02-shore-gameplay");
            yield return CaptureSmallTouchLayout();

            MoveTravelerNear("jesus");
            yield return null;
            yield return PressInteract();
            yield return WaitForConversation();
            yield return DrainDialogueLines();
            SmokeUi.Click("미리암을 도와 빵과 물을 나른다."); yield return null;
            yield return DrainDialogueLines();
            var afterHelp = ReadSave();
            Assert.That(afterHelp.world.waterCarried, Is.True);
            Assert.That(afterHelp.world.inventory, Does.Contain("bread_miriam"));
            Assert.That(afterHelp.world.pointId, Is.EqualTo("room"));
            Assert.That(SmokeUi.HasText("신뢰 2"), Is.True);

            MoveTravelerNear("miriam");
            yield return null;
            yield return PressInteract();
            yield return WaitForConversation();
            yield return DrainDialogueLines();
            SmokeUi.Click("바구니를 가지고 나가 함께 나눌 준비를 한다."); yield return null;
            yield return DrainDialogueLines();
            yield return SmokeUi.WaitForVisibleKorean("나눔의 목격자");
            yield return SmokeUi.Capture("PathOfDawn", "03-witness");
            var witness = ReadSave();
            Assert.That(witness.world.basketShared, Is.True);
            Assert.That(witness.world.inventory, Does.Not.Contain("bread_miriam"));
            Assert.That(witness.meta.routes, Is.EquivalentTo(new[] { "witness" }));
            Assert.That(witness.checkpoints.Count, Is.EqualTo(2));
            Assert.That(GameObject.Find("Galilee Dawn").transform.Find("Shared basket").gameObject.activeSelf, Is.True);
            Assert.That(SmokeUi.HasText("신뢰 3"), Is.True);

            SmokeUi.Click("기억을 품고 과거로"); yield return null;
            SmokeUi.Click("두 번째 선택 전 · 석조 방으로 회귀"); yield return null;
            var rewound = ReadSave();
            Assert.That(rewound.world.pointId, Is.EqualTo("room"));
            Assert.That(rewound.world.inventory, Does.Contain("bread_miriam"), "Checkpoint must restore inventory, not only dialogue.");
            Assert.That(rewound.world.basketShared, Is.False);
            Assert.That(rewound.meta.routes, Is.EquivalentTo(new[] { "witness" }), "Discovered route must survive rewind.");
            Assert.That(SmokeUi.HasText("신뢰 2"), Is.True, "The Ink-owned relationship must roll back with the checkpoint.");
            Assert.That(GameObject.Find("Galilee Dawn").transform.Find("Shared basket").gameObject.activeSelf, Is.False);
            yield return DrainDialogueLines();
            SmokeUi.Click("석조 방을 쉼터로 준비하고 돌아갈 이들을 기다린다."); yield return null;
            yield return DrainDialogueLines();
            yield return SmokeUi.WaitForVisibleKorean("문턱의 등불");
            yield return SmokeUi.Capture("PathOfDawn", "04-shelter");
            var shelter = ReadSave();
            Assert.That(shelter.world.shelterPrepared, Is.True);
            Assert.That(shelter.world.basketShared, Is.False, "The abandoned witness branch leaked into the shelter branch.");
            Assert.That(shelter.world.peopleConnected, Is.False);
            Assert.That(shelter.world.inventory, Does.Contain("bread_miriam"));
            Assert.That(shelter.meta.routes, Is.EquivalentTo(new[] { "witness", "shelter" }));
            Assert.That(GameObject.Find("Galilee Dawn").transform.Find("Shelter rug").gameObject.activeSelf, Is.True);

            SmokeUi.Click("저장하고 타이틀로"); yield return null;
            yield return SmokeUi.WaitForVisibleKorean("새벽의 길");
            yield return SmokeUi.Capture("PathOfDawn", "06-returned-title");
            var snapshotBeforeReload = ReadSave();
            var sourceStory = Resources.Load<Ink.UnityIntegration.InkFile>("Story/galilee");
            Assert.That(sourceStory, Is.Not.Null);
            Assert.DoesNotThrow(() => new NarrativeSession(sourceStory.storyJson).ValidateSave(snapshotBeforeReload),
                "The actual JsonUtility save payload must validate before the fresh app reads it.");
            var oldAppId = UnityEngine.Object.FindFirstObjectByType<PathOfDawnApp>().GetInstanceID();
            yield return SmokeUi.LoadBoot(); // A fresh app instance must read the same isolated stored slot.
            Assert.That(UnityEngine.Object.FindFirstObjectByType<PathOfDawnApp>().GetInstanceID(), Is.Not.EqualTo(oldAppId),
                "The continuation smoke must destroy and recreate the actual scene app.");
            SmokeUi.Click("이어하기"); yield return null;
            yield return SmokeUi.Capture("PathOfDawn", "07-continuing-state");
            yield return DrainDialogueLines();
            yield return SmokeUi.WaitForVisibleKorean("문턱의 등불");
            yield return SmokeUi.Capture("PathOfDawn", "05-continued-shelter");
            Assert.That(GameObject.Find("Galilee Dawn").transform.Find("Shelter rug").gameObject.activeSelf, Is.True);
            Assert.That(GameObject.Find("Galilee Dawn").transform.Find("Shared basket").gameObject.activeSelf, Is.False);
            SmokeUi.Click("저장하고 타이틀로"); yield return null;
            var continued = ReadSave();
            Assert.That(continued.randomState, Is.EqualTo(shelter.randomState));
            Assert.That(continued.world.inventory, Is.EquivalentTo(shelter.world.inventory));
            Assert.That(continued.meta.routes, Is.EquivalentTo(shelter.meta.routes));
            Assert.That(continued.meta.choiceHistory, Is.EqualTo(shelter.meta.choiceHistory), "Loading must not apply choices twice.");
            Assert.That(continued.world.shelterPrepared && !continued.world.basketShared, Is.True);
            SmokeUi.AssertNoMissingScripts();
            LogAssert.NoUnexpectedReceived();
        }

        static void AssertMusicSettingsLayout()
        {
            var texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Where(text => text.gameObject.activeInHierarchy).ToArray();
            var track = texts.Single(text => text.text == "배경 음악 · 갈릴리의 새벽");
            var toggle = texts.Single(text => text.text == "배경 음악 켜기" || text.text == "배경 음악 끄기");
            var storage = texts.Single(text => text.text == "저장 옮기기 · 다른 기기에서도 같은 여정을 이어갈 수 있습니다.");
            var panel = track.rectTransform.parent;
            Rect GlyphBounds(TMP_Text text)
            {
                text.ForceMeshUpdate();
                SmokeUi.AssertVisibleKorean(text.text);
                Assert.That(text.isTextOverflowing, Is.False, "Settings text overflow: " + text.text);
                Assert.That(text.textInfo.characterCount, Is.EqualTo(text.text.Length), "Settings text was truncated: " + text.text);
                var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (var character in text.textInfo.characterInfo.Take(text.textInfo.characterCount))
                {
                    if (char.IsWhiteSpace(character.character)) continue;
                    Assert.That(character.isVisible, Is.True, "Hidden settings glyph: " + character.character);
                    foreach (var corner in new[] { character.bottomLeft, character.topLeft, character.topRight, character.bottomRight })
                    {
                        Vector2 position = panel.InverseTransformPoint(text.rectTransform.TransformPoint(corner));
                        min = Vector2.Min(min, position); max = Vector2.Max(max, position);
                    }
                }
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            var trackBounds = GlyphBounds(track);
            var toggleBounds = GlyphBounds(toggle);
            var storageBounds = GlyphBounds(storage);
            Assert.That(trackBounds.Overlaps(toggleBounds), Is.False, "Track name overlaps the music button.");
            Assert.That(trackBounds.Overlaps(storageBounds) || toggleBounds.Overlaps(storageBounds), Is.False,
                "Music controls overlap the save-transfer heading.");
        }

        static IEnumerator CaptureSmallTouchLayout()
        {
            var touch = InputSystem.AddDevice<Touchscreen>();
            try
            {
                // Rebuild through real settings callbacks, preserving the 110% preference.
                SmokeUi.Click("설정 [Esc]"); yield return null;
                SmokeUi.Click("작게"); yield return null;
                SmokeUi.Click("크게"); yield return null;
                SmokeUi.Click("돌아가기"); yield return null;
                yield return SmokeUi.Capture("PathOfDawn", "08-touch-layout-693x390", 693, 390, () =>
                {
                    foreach (var label in new[] { "왼쪽 패드로 걷기 · 오른쪽 빈 화면을 밀어 둘러보기", "이동" })
                    {
                        var text = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                            .Single(t => t.gameObject.activeInHierarchy && t.text == label);
                        text.ForceMeshUpdate();
                        Assert.That(text.textInfo.characterCount, Is.GreaterThan(0),
                            "Missing touch text: " + label + " font=" + text.fontSize +
                            " rect=" + text.rectTransform.rect + " preferred=" + text.GetPreferredValues(label));
                        SmokeUi.AssertVisibleKorean(label);
                        Assert.That(text.isTextOverflowing, Is.False, "Small touch label overflows: " + label);
                        Assert.That(text.textInfo.lineCount, Is.EqualTo(1), "Touch controls should use one line: " + label);
                    }
                    Assert.That(SmokeUi.FindButton("대화 · 조사"), Is.Not.Null);
                });
            }
            finally
            {
                if (touch.added) InputSystem.RemoveDevice(touch);
            }
            SmokeUi.Click("설정 [Esc]"); yield return null;
            SmokeUi.Click("작게"); yield return null;
            SmokeUi.Click("크게"); yield return null;
            SmokeUi.Click("돌아가기"); yield return null;
        }

        static IEnumerator DrainDialogueLines()
        {
            // Follow the actual generated dialogue buttons, one UI transition per frame.
            for (var i = 0; i < 60; i++)
            {
                var label = new[] { "다음 대사 [E]", "선택하기 [E]", "계속 [E]" }
                    .FirstOrDefault(candidate => SmokeUi.FindButton(candidate) != null);
                if (label == null) yield break;
                SmokeUi.Click(label);
                yield return null;
            }
            Assert.Fail("Dialogue did not settle at a choice, movement, or outcome within 60 lines.");
        }
        static void MoveTravelerNear(string actorName)
        {
            var traveler = GameObject.Find("traveler"); var actor = GameObject.Find(actorName);
            Assert.That(traveler, Is.Not.Null); Assert.That(actor, Is.Not.Null);
            var controller = traveler.GetComponent<CharacterController>();
            controller.enabled = false;
            traveler.transform.position = actor.transform.position + Vector3.back * 1.35f;
            controller.enabled = true;
            Physics.SyncTransforms();
        }
        IEnumerator WalkForwardWithKeyboard()
        {
            var traveler = GameObject.Find("traveler");
            Assert.That(traveler, Is.Not.Null);
            var origin = traveler.transform.position;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return SmokeUi.WaitUntil(() => traveler.transform.position.z - origin.z > .3f,
                    3, "Holding W did not move the actual CharacterController forward.", () =>
                        "Start=" + origin + " Current=" + traveler.transform.position + " keyboardEnabled=" + keyboard.enabled);
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
            yield return null;
            Assert.That(traveler.transform.position.z - origin.z, Is.GreaterThan(.3f));
        }
        IEnumerator WaitForConversation()
        {
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("다음 대사 [E]") != null || SmokeUi.FindButton("선택하기 [E]") != null,
                3, "Virtual E input did not open the nearby NPC conversation.", () =>
                {
                    var traveler = GameObject.Find("traveler"); var jesus = GameObject.Find("jesus"); var miriam = GameObject.Find("miriam");
                    return "Traveler=" + (traveler == null ? "missing" : traveler.transform.position.ToString()) +
                        " Jesus=" + (jesus == null ? "missing" : jesus.transform.position.ToString()) +
                        " Miriam=" + (miriam == null ? "missing" : miriam.transform.position.ToString()) +
                        " keyboardEnabled=" + (keyboard != null && keyboard.enabled) + " focused=" + Application.isFocused +
                        " | " + SmokeUi.VisibleUi();
                });
        }
        IEnumerator PressInteract()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
        static NarrativeSave ReadSave()
        {
            var slot = PlayerPrefs.GetString("POD.activeSlot", "path-of-dawn");
            Assert.That(PersistentSaves.TryLoad(slot, "path-of-dawn", 1, "portable-1", out var payload, out var error), Is.True, error);
            var save = JsonUtility.FromJson<NarrativeSave>(payload);
            Assert.That(save, Is.Not.Null);
            return save;
        }
    }
}
