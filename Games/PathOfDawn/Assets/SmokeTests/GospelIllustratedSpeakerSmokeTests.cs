using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FirstGame.Shared;
using NUnit.Framework;
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
    /// <summary>Actual illustrated-speaker UI, ordinary book return and persisted
    /// story reload. Real model/skin/portrait-backend checks remain in retained cases.</summary>
    public sealed class GospelIllustratedSpeakerSmokeTests
    {
        const string PreservedSlot = "illustrated-untouched-player-original";
        IDisposable storage;
        MemorySaveBackend backend;
        Keyboard keyboard;
        Touchscreen touchscreen;
        InputSettings previousInputs, testInputs;
        GameObject expressionFixture;
        bool hadSlot, hadMuted, hadVolume, hadVoice;
        string oldSlot, attempt;
        int oldMuted, oldVoice;
        float oldVolume;
        GospelPilgrimageApp App => UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageApp>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend = new MemorySaveBackend(); backend.WriteAtomic(PreservedSlot, "original player bytes");
            storage = PersistentSaves.UseBackendForTesting(backend);
            hadSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot"); oldSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            hadMuted = PlayerPrefs.HasKey("GOSPEL.music.muted"); oldMuted = PlayerPrefs.GetInt("GOSPEL.music.muted", 0);
            hadVolume = PlayerPrefs.HasKey("GOSPEL.music.volume"); oldVolume = PlayerPrefs.GetFloat("GOSPEL.music.volume", .5f);
            hadVoice = PlayerPrefs.HasKey("GOSPEL.voice.enabled"); oldVoice = PlayerPrefs.GetInt("GOSPEL.voice.enabled", 1);
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot"); PlayerPrefs.SetInt("GOSPEL.music.muted", 0);
            PlayerPrefs.SetFloat("GOSPEL.music.volume", .5f); PlayerPrefs.SetInt("GOSPEL.voice.enabled", 1);
            previousInputs = InputSystem.settings; testInputs = UnityEngine.Object.Instantiate(previousInputs);
            testInputs.hideFlags = HideFlags.HideAndDontSave;
            testInputs.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputs.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testInputs; keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SmokeUi.LoadBoot();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(App, Is.Not.Null); Assert.That(App.SceneCamera, Is.SameAs(Camera.main));
            SmokeUi.AssertNoMissingScripts();
            Assert.That(App.SceneCamera.GetUniversalAdditionalCameraData().scriptableRenderer.GetType().Name, Is.EqualTo("UniversalRenderer"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Keys();
            if (expressionFixture != null) UnityEngine.Object.Destroy(expressionFixture);
            ExpectNarrationStop("component-disabled");
            yield return SmokeUi.UnloadBoot();
            if (touchscreen != null && touchscreen.added) InputSystem.RemoveDevice(touchscreen);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousInputs != null) InputSystem.settings = previousInputs;
            if (backend != null) Assert.That(backend.Read(PreservedSlot), Is.EqualTo("original player bytes"));
            storage?.Dispose();
            if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", oldSlot); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            if (hadMuted) PlayerPrefs.SetInt("GOSPEL.music.muted", oldMuted); else PlayerPrefs.DeleteKey("GOSPEL.music.muted");
            if (hadVolume) PlayerPrefs.SetFloat("GOSPEL.music.volume", oldVolume); else PlayerPrefs.DeleteKey("GOSPEL.music.volume");
            if (hadVoice) PlayerPrefs.SetInt("GOSPEL.voice.enabled", oldVoice); else PlayerPrefs.DeleteKey("GOSPEL.voice.enabled");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (testInputs != null && InputSystem.settings != testInputs) UnityEngine.Object.DestroyImmediate(testInputs);
        }

        void Keys(params Key[] keys)
        {
            if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }
        IEnumerator Click(string label) { SmokeUi.Click(label); yield return null; yield return null; }
        IEnumerator Shot(string suffix) => SmokeUi.Capture("PathOfDawn", "sharp-illustration-" + attempt + "-" + suffix, 1280, 720, settleSeconds: .15f);
        int ExpectNarrationStart(string key)
        {
            var voice = App.Voice;
            var line = voice.ManifestLines.Single(item => item.key == key);
            string clip = line.resourcePath.Split('/').Last();
            const string number = @"[0-9]+\.[0-9]{3}";
            LogAssert.Expect(LogType.Log, new Regex("^GOSPEL_NATIVITY_NARRATION actual-start key=" + Regex.Escape(key)
                + " clip=" + Regex.Escape(clip) + " textSha256=" + Regex.Escape(line.textSha256)
                + " seconds=" + number + " sourceTime=" + number + " sourceGain=" + number + "$"));
            return voice.ObservedPlaybackCount;
        }
        IEnumerator ObserveNarration(string key, int previousObservations) => SmokeUi.WaitUntil(() => App.Voice.CurrentKey == key
            && App.Voice.LastObservedKey == key && App.Voice.ObservedPlaybackCount > previousObservations
            && App.Voice.IsPlaying && App.Voice.Source.clip != null && App.Voice.Source.time > 0 && App.Voice.Source.volume > 0,
            8, "The expected exact visible narration did not produce its actual playback observation: " + key);
        void ExpectNarrationStop(string reason)
        {
            var voice = App != null ? App.Voice : null;
            if (voice == null || (!voice.IsPlaying && !voice.IsPending) || string.IsNullOrEmpty(voice.CurrentKey)) return;
            LogAssert.Expect(LogType.Log, new Regex("^GOSPEL_NATIVITY_NARRATION stop key=" + Regex.Escape(voice.CurrentKey)
                + " reason=" + Regex.Escape(reason) + " hadPlaying=" + voice.IsPlaying + " hadPending=" + voice.IsPending
                + @" sourceTime=[0-9]+\.[0-9]{3}$"));
        }
        string VisibleText() => string.Join("\n", App.Ui.CanvasView.GetComponentsInChildren<TMP_Text>()
            .Where(text => text.gameObject.activeInHierarchy).Select(text => text.text));

        RawImage VisibleMaryIllustration(Texture2D expected)
        {
            var image = App.Ui.CanvasView.GetComponentsInChildren<RawImage>()
                .Single(item => item.gameObject.activeInHierarchy && item.gameObject.name == "Illustrated story speaker portrait");
            Assert.That(image.enabled && image.canvasRenderer.GetAlpha() > .1f, Is.True);
            Assert.That(image.texture, Is.SameAs(expected), "The actual Mary line must display the authored Mary illustration, not another speaker or model RT.");
            Assert.That(image.texture, Is.TypeOf<Texture2D>());
            var uv = image.uvRect;
            Assert.That(uv.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(uv.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(uv.xMax, Is.LessThanOrEqualTo(1.00001f)); Assert.That(uv.yMax, Is.LessThanOrEqualTo(1.00001f));
            Assert.That(uv.width * uv.height, Is.GreaterThan(.1f));
            Canvas.ForceUpdateCanvases();
            var size = image.rectTransform.rect.size;
            Assert.That(size.x, Is.GreaterThan(100)); Assert.That(size.y, Is.GreaterThan(100));
            Assert.That(expected.width * uv.width / (expected.height * uv.height), Is.EqualTo(size.x / size.y).Within(.01f),
                "The actually displayed illustrated face must retain its aspect, not stretch into the UI slot.");
            Assert.That(image.canvasRenderer.cull, Is.False);
            return image;
        }

        static void AssertNonblankIllustration(Texture2D source)
        {
            Assert.That(source, Is.Not.Null, "Root must import the authored illustrated Mary asset before the actual UI run.");
            Assert.That(source.width, Is.GreaterThanOrEqualTo(512)); Assert.That(source.height, Is.GreaterThanOrEqualTo(512));
            var old = RenderTexture.active;
            var target = RenderTexture.GetTemporary(64, 64, 0, RenderTextureFormat.ARGB32);
            var read = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(source, target); RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); read.Apply();
                var pixels = read.GetPixels32();
                Assert.That(pixels.Count(pixel => pixel.a > 64), Is.GreaterThan(512));
                Assert.That(pixels.Where(pixel => pixel.a > 64).Select(pixel =>
                    ((pixel.r >> 3) << 10) | ((pixel.g >> 3) << 5) | (pixel.b >> 3)).Distinct().Count(), Is.GreaterThan(32),
                    "A blank or uniform portrait does not satisfy the actual illustration contract.");
            }
            finally
            {
                RenderTexture.active = old; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(read);
            }
        }

        [UnityTest]
        public IEnumerator IllustratedMarySpeakerSurvivesBibleAndActualReloadWithoutMovingPhysicalStory()
        {
            int observed = ExpectNarrationStart("birth-road-bethlehem:0");
            yield return Click("새 여정 시작");
            yield return ObserveNarration("birth-road-bethlehem:0", observed); int guard = 0;
            while (App.Chronicle.CurrentStoryBeat.Speaker != "마리아" && guard++ < 10)
            {
                string next = App.Chronicle.CurrentQuest.Id + ":" + (App.Chronicle.StoryBeatIndex + 1);
                ExpectNarrationStop("advance"); observed = ExpectNarrationStart(next);
                yield return Click("이야기 계속"); yield return ObserveNarration(next, observed);
            }
            Assert.That(App.Chronicle.CurrentStoryBeat.Speaker, Is.EqualTo("마리아"));
            Assert.That(App.OpeningStage, Is.Not.Null); Assert.That(App.OpeningStage.IsVisible, Is.True);
            Assert.That(App.OpeningStage.CurrentSet, Is.EqualTo("nazareth"));
            Assert.That(App.Actor.gameObject.activeInHierarchy, Is.False, "Do not show the future-age field traveler in the birth scene.");
            Assert.That(App.CameraRig.enabled, Is.False);
            Assert.That(App.Player.gameObject.activeInHierarchy && App.Player.Controller.enabled, Is.True);
            Assert.That(App.People.Count, Is.Zero, "Illustrated presentation must not create physical story people.");
            string beat = App.Chronicle.StoryVoiceKey, line = App.Chronicle.CurrentStoryBeat.Text;
            string reference = App.Chronicle.CurrentStoryReference;
            Assert.That(VisibleText(), Does.Contain(line)); Assert.That(VisibleText(), Does.Contain("마리아"));
            Assert.That(VisibleText(), Does.Not.Contain("극화 대화")); Assert.That(VisibleText(), Does.Not.Contain("성경 직접 인용"));
            Assert.That(VisibleText(), Does.Not.Contain(reference));
            var mary = Resources.Load<Texture2D>("Art/GospelIllustrated/Portraits/Mary");
            AssertNonblankIllustration(mary);
            yield return SmokeUi.WaitUntil(() => App.Ui.CanvasView.GetComponentsInChildren<RawImage>()
                .Any(image => image.gameObject.activeInHierarchy && image.name == "Illustrated story speaker portrait" && image.enabled && image.texture == mary),
                3, "The current Mary line did not display its actual illustrated source.");
            VisibleMaryIllustration(mary);
            var performer = App.OpeningStage.ModelFor("mary");
            Assert.That(performer, Is.Not.Null); Assert.That(performer.ModelIdentity, Is.EqualTo("Mary"));
            Assert.That(performer.ModelResource, Is.EqualTo("Art/GospelNpr/Characters/Mary"));
            yield return Shot("illustrated-mary-story");
            var physical = App.Player.transform; int physicalId = physical.GetInstanceID();
            Vector3 position = App.Player.Position; float yaw = App.Player.FacingYaw;
            string slot = App.ActiveSaveSlot, storedBeforeBook = backend.Read(App.ActiveSaveSlot);
            ExpectNarrationStop("bible"); App.ShowBible(); yield return null; yield return null;
            Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"));
            Assert.That(App.OpeningStage.IsVisible, Is.True); Assert.That(App.OpeningStage.CurrentSet, Is.EqualTo("nazareth"));
            Assert.That(App.Actor.gameObject.activeInHierarchy || App.CameraRig.enabled, Is.False);
            Assert.That(physical.gameObject.activeInHierarchy && App.Player.Controller.enabled, Is.True);
            Assert.That(Vector3.Distance(App.Player.Position, position), Is.LessThan(.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, yaw)), Is.LessThan(.01f));
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(beat)); Assert.That(App.Chronicle.DiscoveredCount, Is.Zero);
            Assert.That(App.Ui.CanvasView.GetComponentsInChildren<RawImage>()
                .Any(image => image.gameObject.activeInHierarchy && image.name == "Illustrated story speaker portrait"), Is.False);
            Assert.That(performer.PortraitCamera == null || !performer.PortraitCamera.enabled, Is.True,
                "The illustrated book overlay must not keep a separate model portrait camera rendering.");
            observed = ExpectNarrationStart(beat);
            yield return Click("여정으로"); yield return ObserveNarration(beat, observed);
            VisibleMaryIllustration(mary);
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(beat)); Assert.That(VisibleText(), Does.Contain(line));
            Assert.That(App.Player.transform.GetInstanceID(), Is.EqualTo(physicalId));
            Assert.That(Vector3.Distance(App.Player.Position, position), Is.LessThan(.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, yaw)), Is.LessThan(.01f));
            Assert.That(backend.Read(slot), Is.EqualTo(storedBeforeBook), "Portrait/book presentation must not write player data.");
            yield return Shot("illustrated-mary-book-return");
            Assert.That(App.Save(false), Is.True); string saved = backend.Read(slot);
            ExpectNarrationStop("component-disabled");
            yield return SmokeUi.LoadBoot();
            observed = ExpectNarrationStart(beat);
            yield return Click("이어하기"); yield return ObserveNarration(beat, observed);
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(slot)); Assert.That(backend.Read(slot), Is.EqualTo(saved));
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(beat)); Assert.That(App.Chronicle.CurrentStoryBeat.Text, Is.EqualTo(line));
            Assert.That(App.Chronicle.CurrentStoryBeat.Speaker, Is.EqualTo("마리아"));
            Assert.That(App.OpeningStage.IsVisible, Is.True); Assert.That(App.OpeningStage.CurrentSet, Is.EqualTo("nazareth"));
            Assert.That(App.People.Count, Is.Zero); Assert.That(App.Player.Controller.enabled, Is.True);
            Assert.That(Vector3.Distance(App.Player.Position, position), Is.LessThan(.12f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, yaw)), Is.LessThan(.01f));
            VisibleMaryIllustration(mary);
            Assert.That(VisibleText(), Does.Contain(line)); Assert.That(VisibleText(), Does.Not.Contain(reference));
            yield return Shot("illustrated-mary-reloaded");
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FiveActualWorldActorsUseSourceMappedAioVertexDeformationsWithoutMovingOwners()
        {
            App.Ui.CanvasView.gameObject.SetActive(false); App.Actor.gameObject.SetActive(false);
            expressionFixture = new GameObject("Five actual source-mapped world faces");
            expressionFixture.transform.position = App.Player.Position;
            string[] identities = { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" };
            var actors = new List<GospelNprCharacterView>();
            var feet = new List<Vector3>();
            var bindings = new List<Dictionary<string, List<Tuple<SkinnedMeshRenderer, int>>>>();
            foreach (string identity in identities)
            {
                var owner = new GameObject("Actual AIO world owner " + identity);
                owner.transform.SetParent(expressionFixture.transform, false);
                owner.transform.localPosition = new Vector3((actors.Count - 2) * 1.12f, 0, 0);
                owner.transform.position = new Vector3(owner.transform.position.x,
                    App.World.HeightAt(owner.transform.position.x, owner.transform.position.z) + .035f, owner.transform.position.z);
                var actor = owner.AddComponent<GospelNprCharacterView>();
                actor.LoadModel(identity, GospelNprCharacterView.HeightFor(identity), Color.white);
                actor.SetMotion(Vector3.zero, true, 180); actor.Tick(0);
                Assert.That(actor.HasSourceExpressionMap, Is.True, identity + " must load its actual source expression map.");
                Assert.That(actor.ExpressionBindingCount, Is.EqualTo(5), identity);
                Assert.That(actor.SkinnedRenderers.Count, Is.GreaterThan(0));
                var channels = new Dictionary<string, List<Tuple<SkinnedMeshRenderer, int>>> {
                    { "A", new List<Tuple<SkinnedMeshRenderer, int>>() },
                    { "I", new List<Tuple<SkinnedMeshRenderer, int>>() },
                    { "O", new List<Tuple<SkinnedMeshRenderer, int>>() } };
                foreach (var renderer in actor.SkinnedRenderers)
                    for (int shape = 0; shape < renderer.sharedMesh.blendShapeCount; shape++)
                    {
                        string leaf = renderer.sharedMesh.GetBlendShapeName(shape).ToLowerInvariant().Split('.').Last();
                        // All five prepared FBXs retain the actual baked source leaves mouthA/I/O.
                        string channel = leaf == "aa" || leaf == "moutha" || leaf.EndsWith("fcl_mth_a", StringComparison.Ordinal) ? "A"
                            : leaf == "ih" || leaf == "mouthi" || leaf.EndsWith("fcl_mth_i", StringComparison.Ordinal) ? "I"
                            : leaf == "oh" || leaf == "moutho" || leaf.EndsWith("fcl_mth_o", StringComparison.Ordinal) ? "O" : null;
                        if (channel == null) continue;
                        var mesh = renderer.sharedMesh;
                        Assert.That(mesh.GetBlendShapeFrameCount(shape), Is.GreaterThan(0));
                        var deltas = new Vector3[mesh.vertexCount];
                        mesh.GetBlendShapeFrameVertices(shape, mesh.GetBlendShapeFrameCount(shape) - 1, deltas, null, null);
                        Assert.That(deltas.Any(delta => delta.sqrMagnitude > .0000000001f), Is.True,
                            identity + ": " + channel + " must have actual authored vertex displacement.");
                        channels[channel].Add(Tuple.Create(renderer, shape));
                    }
                foreach (var channel in channels) Assert.That(channel.Value.Count, Is.GreaterThan(0), identity + ": " + channel.Key);
                actors.Add(actor); feet.Add(owner.transform.position); bindings.Add(channels);
            }
            yield return null; yield return null;
            var maxima = actors.Select(_ => new Dictionary<string, float> { { "A", 0 }, { "I", 0 }, { "O", 0 } }).ToArray();
            foreach (var actor in actors) actor.SetSpeaking(true, "actual-source-expression-inspection");
            float until = Time.realtimeSinceStartup + 1.18f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                for (int index = 0; index < actors.Count; index++)
                {
                    actors[index].Tick(Time.unscaledDeltaTime);
                    foreach (var channel in bindings[index])
                        foreach (var binding in channel.Value)
                            maxima[index][channel.Key] = Mathf.Max(maxima[index][channel.Key], binding.Item1.GetBlendShapeWeight(binding.Item2));
                    Assert.That(Vector3.Distance(actors[index].FootRoot.position, feet[index]), Is.LessThan(.00001f),
                        "Actual facial animation must never move the physical source owner.");
                }
            }
            for (int index = 0; index < actors.Count; index++)
                foreach (string channel in new[] { "A", "I", "O" })
                    Assert.That(maxima[index][channel], Is.GreaterThan(3), identities[index] + ": actual source-mapped " + channel + " must deform its renderer during the live cycle.");
            App.CameraRig.SetTarget(expressionFixture.transform); App.CameraRig.SetOrbit(0, 12); App.CameraRig.SetZoom(8.2f); App.CameraRig.Snap();
            yield return Shot("five-source-mapped-aio-world-actors");
            foreach (var actor in actors) actor.SetSpeaking(false);
            float settle = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < settle)
            {
                yield return null;
                foreach (var actor in actors) actor.Tick(Time.unscaledDeltaTime);
            }
            foreach (var channels in bindings)
                foreach (var channel in channels.Values)
                    foreach (var binding in channel) Assert.That(binding.Item1.GetBlendShapeWeight(binding.Item2), Is.LessThan(1),
                        "Stopping actual speech must close the animated mouth, not leave a frozen vowel.");
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }
    }
}
