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
using UnityEngine.UI;

namespace PathOfDawn.SmokeTests
{
    // Editor-only test discovery; the fixture explicitly runs the scene in PlayMode.
    public sealed class PathOfDawnPlayModeSmokeTests
    {
        IDisposable storageScope;
        Keyboard keyboard;
        InputSettings originalInputSettings, fixtureInputSettings;
        bool hadSlot, hadScale, hadVolume, hadVoiceMute, preferencesCaptured;
        int originalVoiceMute;
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
            hadVoiceMute = PlayerPrefs.HasKey("POD.voice.muted"); originalVoiceMute = PlayerPrefs.GetInt("POD.voice.muted", 0);
            originalListenerVolume = AudioListener.volume; preferencesCaptured = true;
            // A known slot/settings baseline, while preserving every original key for teardown.
            PlayerPrefs.DeleteKey("POD.activeSlot"); PlayerPrefs.SetFloat("POD.textScale", 1); PlayerPrefs.SetFloat("POD.volume", .65f);
            PlayerPrefs.SetInt("POD.voice.muted", 0);
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
                if (hadVoiceMute) PlayerPrefs.SetInt("POD.voice.muted", originalVoiceMute); else PlayerPrefs.DeleteKey("POD.voice.muted");
                PlayerPrefs.Save(); AudioListener.volume = originalListenerVolume; preferencesCaptured = false;
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RealDialogueRewindAlternateOutcomeAndContinuePreserveMemories()
        {
            var app = UnityEngine.Object.FindFirstObjectByType<PathOfDawnApp>();
            Assert.That(app, Is.Not.Null);
            Assert.That(app.EnvironmentPrefab, Is.Null, "The retired environment must not be packaged through the boot scene.");
            Assert.That(app.JesusPrefab, Is.Null);
            Assert.That(app.TravelerPrefab, Is.Null);
            Assert.That(app.MiriamPrefab, Is.Null);
            var stage = app.GetComponent<DawnIllustratedStage>();
            Assert.That(stage, Is.Not.Null, "The actual app must own the illustrated stage.");
            Assert.That(stage.IsReady, Is.True);
            Assert.That(stage.ActorCount, Is.EqualTo(3));
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
            var voice = app.GetComponent<DialogueVoicePlayer>();
            Assert.That(voice, Is.Not.Null, "The actual app must own the opening narration player.");
            Assert.That(voice.SupportedLineCount, Is.GreaterThanOrEqualTo(9), "The complete opening must have offline voice entries.");
            foreach (var actor in new[] { "traveler", "jesus", "miriam" })
            {
                Assert.That(stage.ActorVisual(actor), Is.Not.Null, actor + " needs a visible painted actor.");
                Assert.That(stage.ActorTexture(actor), Is.SameAs(Resources.Load<Texture2D>("Art/Painted/" + actor)));
                var physicalActor = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Single(transform => transform.name == actor && transform.parent != null && transform.parent.name == "Galilee interaction world").gameObject;
                Assert.That(physicalActor, Is.Not.Null);
                Assert.That(physicalActor.GetComponentsInChildren<Renderer>(), Is.Empty,
                    "Retired meshes must not appear beneath the illustrated actor: " + actor);
            }
            Assert.That(stage.StageRoot.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True,
                "Painted scenery must not intercept dialogue, settings, or touch controls.");
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
            SmokeUi.Click("대사 음성 끄기"); yield return null;
            Assert.That(voice.Muted, Is.True);
            SmokeUi.Click("대사 음성 켜기"); yield return null;
            Assert.That(voice.Muted, Is.False);
            SmokeUi.Click("크게"); yield return null;
            SmokeUi.Click("크게"); yield return null;
            Assert.That(PlayerPrefs.GetFloat("POD.textScale"), Is.EqualTo(1.3f).Within(.001f));
            SmokeUi.Click("타이틀로"); yield return null;
            SmokeUi.Click("새 게임");
            var gameplayHud = stage.StageRoot.parent.Find("HUD");
            Assert.That(gameplayHud, Is.Not.Null);
            Assert.That(stage.CurrentMode, Is.EqualTo(DawnIllustratedStage.ViewMode.Intro));
            Assert.That(gameplayHud.gameObject.activeInHierarchy, Is.False,
                "The exploration HUD must not peek below the introduction's letterbox.");
            yield return SmokeUi.Capture("PathOfDawn", "17-intro-letterbox-no-hud", 1920, 1080, () =>
            {
                Assert.That(stage.CurrentMode, Is.EqualTo(DawnIllustratedStage.ViewMode.Intro));
                Assert.That(gameplayHud.gameObject.activeInHierarchy, Is.False);
                SmokeUi.AssertVisibleKorean("갈릴리 호숫가 · 낯선 새벽");
            });
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("다음 대사 [E]") != null,
                30, "The six-second introduction did not reach dialogue within the software-rendering budget.", () =>
                {
                    var intro = app.GetComponent<PlayableDirector>();
                    return (intro == null ? "No PlayableDirector" :
                        "Timeline state=" + intro.state + " time=" + intro.time + " duration=" + intro.duration) +
                        " | Time.time=" + Time.time + " realTime=" + Time.realtimeSinceStartup + " delta=" + Time.deltaTime;
                });
            Assert.That(gameplayHud.gameObject.activeInHierarchy, Is.True,
                "The chapter HUD must return when the opening dialogue begins.");
            Assert.That(voice.HasCurrentLine, Is.True, "The displayed opening line must select its exact offline voice.");
            Assert.That(voice.CurrentLineText, Is.EqualTo(new NarrativeSession(Resources.Load<Ink.UnityIntegration.InkFile>("Story/galilee").storyJson).Lines[0]));
            var openingClip = voice.CurrentClip;
            yield return SmokeUi.WaitUntil(() => openingClip.loadState == AudioDataLoadState.Loaded,
                10, "The actual opening voice clip did not decode.");
            Assert.That(openingClip.length, Is.GreaterThan(.5f));
            SmokeUi.Click("다시 듣기"); yield return null;
            Assert.That(voice.CurrentClip, Is.SameAs(openingClip), "Replay must keep the displayed line.");
            yield return SmokeUi.Capture("PathOfDawn", "11-opening-voiced-dialogue", 1920, 1080, AssertDialogueLayout);
            yield return DrainDialogueLines();
            Assert.That(voice.HasCurrentLine, Is.False, "Leaving dialogue must clear the voice and queued playback.");
            Assert.That(ReadSave().world.pointId, Is.EqualTo("shore"));
            AssertStageScene(stage, "shore");
            yield return WaitForExplorationActors(stage);
            AssertExplorationActors(stage);
            yield return WalkForwardWithKeyboard();
            yield return SmokeUi.Capture("PathOfDawn", "02-shore-gameplay");
            yield return CaptureSmallTouchLayout();

            MoveTravelerNear("jesus");
            yield return null;
            yield return PressInteract();
            yield return WaitForConversation();
            yield return DrainDialogueLines();
            SmokeUi.Click("미리암을 도와 빵과 물을 나른다."); yield return null;
            yield return AdvanceToSpeaker("예수님");
            Assert.That(stage.CurrentMode, Is.EqualTo(DawnIllustratedStage.ViewMode.Dialogue));
            yield return SmokeUi.Capture("PathOfDawn", "14-jesus-painted-dialogue", 1920, 1080, AssertDialogueLayout);
            yield return SmokeUi.Capture("PathOfDawn", "16-jesus-dialogue-130percent-693x390", 693, 390, AssertDialogueLayout);
            yield return DrainDialogueLines();
            var afterHelp = ReadSave();
            Assert.That(afterHelp.world.waterCarried, Is.True);
            Assert.That(afterHelp.world.inventory, Does.Contain("bread_miriam"));
            Assert.That(afterHelp.world.pointId, Is.EqualTo("room"));
            Assert.That(SmokeUi.HasText("신뢰 2"), Is.True);
            AssertStageScene(stage, "room");
            yield return WaitForExplorationActors(stage);
            AssertExplorationActors(stage);
            yield return SmokeUi.Capture("PathOfDawn", "15-room-painted-gameplay");

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
            AssertWorldMarker("Shared basket state", true);
            AssertStageScene(stage, "shore");
            Assert.That(SmokeUi.HasText("신뢰 3"), Is.True);

            SmokeUi.Click("기억을 품고 과거로"); yield return null;
            SmokeUi.Click("두 번째 선택 전 · 석조 방으로 회귀"); yield return null;
            var rewound = ReadSave();
            Assert.That(rewound.world.pointId, Is.EqualTo("room"));
            Assert.That(rewound.world.inventory, Does.Contain("bread_miriam"), "Checkpoint must restore inventory, not only dialogue.");
            Assert.That(rewound.world.basketShared, Is.False);
            Assert.That(rewound.meta.routes, Is.EquivalentTo(new[] { "witness" }), "Discovered route must survive rewind.");
            Assert.That(SmokeUi.HasText("신뢰 2"), Is.True, "The Ink-owned relationship must roll back with the checkpoint.");
            AssertWorldMarker("Shared basket state", false);
            AssertStageScene(stage, "room");
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
            AssertWorldMarker("Shelter rug state", true);
            AssertStageScene(stage, "room");

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
            stage = UnityEngine.Object.FindFirstObjectByType<PathOfDawnApp>().GetComponent<DawnIllustratedStage>();
            SmokeUi.Click("이어하기"); yield return null;
            yield return SmokeUi.Capture("PathOfDawn", "07-continuing-state");
            yield return DrainDialogueLines();
            yield return SmokeUi.WaitForVisibleKorean("문턱의 등불");
            yield return SmokeUi.Capture("PathOfDawn", "05-continued-shelter");
            AssertWorldMarker("Shelter rug state", true);
            AssertWorldMarker("Shared basket state", false);
            AssertStageScene(stage, "room");
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
            var voice = texts.Single(text => text.text == "초반 대사 음성");
            var voiceToggle = texts.Single(text => text.text == "대사 음성 켜기" || text.text == "대사 음성 끄기");
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
            var voiceBounds = GlyphBounds(voice);
            var voiceToggleBounds = GlyphBounds(voiceToggle);
            Assert.That(trackBounds.Overlaps(toggleBounds), Is.False, "Track name overlaps the music button.");
            Assert.That(trackBounds.Overlaps(storageBounds) || toggleBounds.Overlaps(storageBounds), Is.False,
                "Music controls overlap the save-transfer heading.");
            Assert.That(voiceBounds.Overlaps(voiceToggleBounds), Is.False, "Voice label overlaps its setting.");
            Assert.That(voiceBounds.Overlaps(storageBounds) || voiceToggleBounds.Overlaps(storageBounds) ||
                voiceBounds.Overlaps(trackBounds) || voiceToggleBounds.Overlaps(toggleBounds), Is.False,
                "Voice controls overlap other settings rows.");
        }

        static void AssertStageScene(DawnIllustratedStage stage, string scene)
        {
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.IsReady, Is.True);
            Assert.That(stage.StageRoot.gameObject.activeInHierarchy, Is.True,
                "The painted stage disappeared while the story was visible.");
            Assert.That(stage.CurrentScene, Is.EqualTo(scene), "The illustration does not match the restored story location.");
            Assert.That(stage.BackgroundTexture, Is.SameAs(Resources.Load<Texture2D>("Art/Painted/" + scene)));
        }

        static void AssertExplorationActors(DawnIllustratedStage stage)
        {
            Assert.That(stage.CurrentMode, Is.EqualTo(DawnIllustratedStage.ViewMode.Exploration));
            foreach (string id in stage.CurrentScene == "shore" ? new[] { "traveler", "jesus", "miriam" } : new[] { "traveler", "miriam" })
            {
                var actor = stage.ActorVisual(id);
                Assert.That(actor.gameObject.activeInHierarchy, Is.True, "Invisible exploration actor: " + id);
                Assert.That(actor.GetComponentInParent<CanvasGroup>().alpha, Is.GreaterThan(.99f));
                Assert.That(actor.rect.height * actor.localScale.y, Is.GreaterThan(100f),
                    "The full-body painting is too small to read: " + id);
                Assert.That(actor.GetComponentsInChildren<RawImage>().Any(image => image.texture != null && image.color.a > .5f),
                    Is.True, "The actor rectangle has no visible illustration: " + id);
            }
            if (stage.CurrentScene == "shore")
            {
                Assert.That(stage.ActorStagePosition("traveler").y, Is.LessThan(stage.ActorStagePosition("jesus").y));
                Assert.That(stage.ActorVisual("traveler").parent.GetSiblingIndex(),
                    Is.GreaterThan(stage.ActorVisual("jesus").parent.GetSiblingIndex()),
                    "The nearer traveler must draw in front of the distant speaker.");
            }
            else
                Assert.That(stage.ActorVisual("jesus").GetComponentInParent<CanvasGroup>().alpha, Is.LessThan(.01f),
                    "The shore actor must not remain visible during room exploration.");
        }

        static IEnumerator WaitForExplorationActors(DawnIllustratedStage stage)
        {
            yield return SmokeUi.WaitUntil(() => stage.CurrentMode == DawnIllustratedStage.ViewMode.Exploration &&
                new[] { "traveler", "miriam" }.All(id => stage.ActorVisual(id).GetComponentInParent<CanvasGroup>().alpha > .99f &&
                    stage.ActorVisual(id).rect.height < 600f) &&
                (stage.CurrentScene == "shore" || stage.ActorVisual("jesus").GetComponentInParent<CanvasGroup>().alpha < .01f),
                8, "The painted scene did not settle from dialogue into visible exploration actors.");
        }

        static void AssertWorldMarker(string marker, bool active)
        {
            var world = GameObject.Find("Galilee interaction world");
            Assert.That(world, Is.Not.Null);
            var state = world.transform.Find(marker);
            Assert.That(state, Is.Not.Null);
            Assert.That(state.gameObject.activeSelf, Is.EqualTo(active));
        }

        static void AssertDialogueLayout()
        {
            var panel = GameObject.Find("Dialogue");
            Assert.That(panel, Is.Not.Null, "The actual dialogue panel must be rendered.");
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.That(text.isTextOverflowing, Is.False,
                    "Dialogue overflows at " + PlayerPrefs.GetFloat("POD.textScale") + " text scale: " + text.text);
                Assert.That(text.textInfo.characterCount, Is.EqualTo(text.text.Length),
                    "Dialogue is truncated or replaced by an ellipsis: " + text.text);
                foreach (var character in text.textInfo.characterInfo.Take(text.textInfo.characterCount))
                    if (!char.IsWhiteSpace(character.character))
                        Assert.That(character.isVisible, Is.True, "Hidden dialogue glyph: " + character.character);
            }
        }

        static IEnumerator AdvanceToSpeaker(string speaker)
        {
            for (int line = 0; line < 30; ++line)
            {
                AssertDialogueLayout();
                if (UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                    .Any(text => text.gameObject.activeInHierarchy && text.text == speaker)) yield break;
                SmokeUi.Click("다음 대사 [E]");
                yield return null;
            }
            Assert.Fail("The authored conversation did not reach speaker: " + speaker);
        }

        static IEnumerator CaptureSmallTouchLayout()
        {
            var touch = InputSystem.AddDevice<Touchscreen>();
            try
            {
                // Rebuild through real settings callbacks, preserving the 130% preference.
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
                AssertDialogueLayout();
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
            var stage = UnityEngine.Object.FindFirstObjectByType<PathOfDawnApp>().GetComponent<DawnIllustratedStage>();
            var visual = stage.ActorVisual("traveler");
            var originOnStage = stage.ActorStagePosition("traveler");
            var originalHeight = visual.rect.height * visual.localScale.y;
            Assert.That(stage.TravelerWalkFrame, Is.EqualTo(-1));
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                // The cloud software renderer can advance few game frames per wall-clock
                // second during builds. Keep movement/pose thresholds, not a hardware FPS gate.
                yield return SmokeUi.WaitUntil(() => traveler.transform.position.z - origin.z > .3f,
                    8, "Holding W did not move the actual CharacterController forward.", () =>
                        "Start=" + origin + " Current=" + traveler.transform.position + " keyboardEnabled=" + keyboard.enabled);
                yield return SmokeUi.WaitUntil(() => stage.TravelerIsMoving && stage.TravelerNormalizedSpeed > .4f,
                    8, "Actual displacement must drive the painted walking sequence.");
                Assert.That(stage.TravelerWalkFrame, Is.InRange(0, 3));
                Assert.That(stage.ActorTexture("traveler"), Is.SameAs(Resources.Load<Texture2D>("Art/Painted/traveler-walk")));
                Assert.That(Vector2.Distance(originOnStage, stage.ActorStagePosition("traveler")), Is.GreaterThan(2f),
                    "The displayed traveler must follow the CharacterController's world position.");
                Assert.That(visual.rect.height * visual.localScale.y, Is.LessThan(originalHeight),
                    "Walking deeper into the painted scene must reduce the traveler's perspective scale.");
                yield return SmokeUi.Capture("PathOfDawn", "12-walking-animation-a");
                var firstStride = stage.TravelerWalkFrame;
                yield return SmokeUi.WaitUntil(() => stage.TravelerWalkFrame >= 0 && stage.TravelerWalkFrame != firstStride,
                    8, "The walking illustration must advance through different authored stride frames.");
                yield return SmokeUi.Capture("PathOfDawn", "13-walking-animation-b");
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
            yield return null;
            Assert.That(traveler.transform.position.z - origin.z, Is.GreaterThan(.3f));
            yield return SmokeUi.WaitUntil(() => !stage.TravelerIsMoving && stage.TravelerWalkFrame == -1 && stage.TravelerNormalizedSpeed < .02f,
                8, "Releasing movement must return the character to idle without sliding.");
            var rearIdle = Resources.Load<Texture2D>("Art/Painted/traveler-back");
            Assert.That(rearIdle, Is.Not.Null, "A rear-facing stop requires the authored feet-planted idle illustration.");
            Assert.That(stage.ActorTexture("traveler"), Is.SameAs(rearIdle),
                "Stopping after walking away must show the planted rear idle, rather than freezing a walking pose.");
            var stopped = traveler.transform.position;
            yield return new WaitForSeconds(.2f);
            Assert.That(Vector2.Distance(new Vector2(stopped.x, stopped.z), new Vector2(traveler.transform.position.x, traveler.transform.position.z)),
                Is.LessThan(.025f), "The stationary character drifted after braking.");
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
