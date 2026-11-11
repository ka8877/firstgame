using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>
    /// Root-only prototype screenshot producer. Successful execution means only
    /// that this capture sequence completed, never visual or release approval.
    /// Deliberately excluded from the current 15-case Native contract.
    /// </summary>
    [Explicit("Root must opt into a unique prototype capture attempt and review its originals.")]
    [Category("NprPrototypeCaptureOnly")]
    public sealed class GospelNprPrototypeCapture
    {
        const string OriginalSlot = "npr-prototype-untouched-player-original";
        readonly List<Frame> frames = new List<Frame>();
        IDisposable storage;
        MemorySaveBackend backend;
        GameObject fixture;
        string attempt, captureRoot, previousSlot;
        bool hadSlot, hadMuted, hadVolume, hadVoice, preferencesCaptured, enteredPlay;
        int previousMuted, previousVoice;
        float previousVolume;
        GospelPilgrimageApp App => UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageApp>();

        [Serializable]
        sealed class Frame
        {
            public string png, scope, uiView, quest, questStage;
            public Vector3 player, camera;
            public float playerYaw, cameraYaw, cameraPitch, orbitYaw, orbitPitch;
            public string[] models;
        }

        [Serializable]
        sealed class CaptureIndex
        {
            public string kind = "unreviewed-native-prototype-capture";
            public string attempt, unityVersion;
            public bool captureSequenceReachedEnd;
            public bool visualApproval = false;
            public bool releaseEvidence = false;
            public string visualReviewStatus = "pending-root-original-inspection";
            public string limitations = "Model shots are explicitly isolated visual fixtures; story/field/book shots use the actual App and UI callbacks. Pointer hit testing, full story, save compatibility, Native contract and Web validation are separate.";
            public Frame[] frames;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // No fallback output or attempt name: an accidental broad run must
            // not create unnamed pictures or overwrite canonical originals.
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NPR_PROTOTYPE_ATTEMPT");
            Assert.That(attempt != null && Regex.IsMatch(attempt, @"\A[A-Za-z0-9][A-Za-z0-9_-]{2,95}\z"), Is.True,
                "Root must provide FIRSTGAME_NPR_PROTOTYPE_ATTEMPT (3-96 safe characters). This is a prototype capture, not a Native admission run.");
            captureRoot = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_CAPTURE_ROOT");
            Assert.That(!string.IsNullOrEmpty(captureRoot) && Path.IsPathRooted(captureRoot), Is.True,
                "Root must provide a unique absolute FIRSTGAME_NATIVE_CAPTURE_ROOT inside repository Artifacts.");
            captureRoot = Path.GetFullPath(captureRoot);
            string artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Artifacts"));
            Assert.That(captureRoot.StartsWith(artifacts + Path.DirectorySeparatorChar, StringComparison.Ordinal), Is.True);
            Assert.That(Directory.Exists(Path.Combine(captureRoot, "PathOfDawn")), Is.False,
                "Use a fresh prototype output directory; do not mix attempts or approved originals.");
            frames.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            enteredPlay = true;
            // Keep the process configuration explicit after a possible domain
            // transition; this producer never changes environment variables.
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NPR_PROTOTYPE_ATTEMPT");
            captureRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_CAPTURE_ROOT"));
            frames.Clear();
            backend = new MemorySaveBackend();
            backend.WriteAtomic(OriginalSlot, "preserved prototype player bytes");
            storage = PersistentSaves.UseBackendForTesting(backend);
            hadSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot"); previousSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            hadMuted = PlayerPrefs.HasKey("GOSPEL.music.muted"); previousMuted = PlayerPrefs.GetInt("GOSPEL.music.muted", 0);
            hadVolume = PlayerPrefs.HasKey("GOSPEL.music.volume"); previousVolume = PlayerPrefs.GetFloat("GOSPEL.music.volume", .5f);
            hadVoice = PlayerPrefs.HasKey("GOSPEL.voice.enabled"); previousVoice = PlayerPrefs.GetInt("GOSPEL.voice.enabled", 1);
            preferencesCaptured = true;
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot"); PlayerPrefs.SetInt("GOSPEL.music.muted", 0);
            PlayerPrefs.SetFloat("GOSPEL.music.volume", .5f); PlayerPrefs.SetInt("GOSPEL.voice.enabled", 1);
            yield return SmokeUi.LoadBoot();
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(App, Is.Not.Null); Assert.That(App.SceneCamera, Is.SameAs(Camera.main));
            Assert.That(App.World.NprEnvironmentReady, Is.True, "The prototype must use the actual prepared spatial world.");
            SmokeUi.AssertNoMissingScripts();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (enteredPlay && Application.isPlaying && App != null && App.Ui != null)
            {
                App.Ui.OnMove?.Invoke(Vector2.zero); App.Ui.OnRun?.Invoke(false);
            }
            if (enteredPlay && Application.isPlaying)
            {
                if (fixture != null) UnityEngine.Object.Destroy(fixture);
                yield return SmokeUi.UnloadBoot();
            }
            storage?.Dispose();
            if (preferencesCaptured)
            {
                if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", previousSlot); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
                if (hadMuted) PlayerPrefs.SetInt("GOSPEL.music.muted", previousMuted); else PlayerPrefs.DeleteKey("GOSPEL.music.muted");
                if (hadVolume) PlayerPrefs.SetFloat("GOSPEL.music.volume", previousVolume); else PlayerPrefs.DeleteKey("GOSPEL.music.volume");
                if (hadVoice) PlayerPrefs.SetInt("GOSPEL.voice.enabled", previousVoice); else PlayerPrefs.DeleteKey("GOSPEL.voice.enabled");
                PlayerPrefs.Save(); preferencesCaptured = false;
            }
            if (enteredPlay && Application.isPlaying) yield return new ExitPlayMode();
            enteredPlay = false;
            if (backend != null) Assert.That(backend.Read(OriginalSlot), Is.EqualTo("preserved prototype player bytes"));
        }

        IEnumerator Click(string label)
        {
            SmokeUi.Click(label); yield return null; yield return null;
        }

        IEnumerator Shot(string name, string scope, params string[] models)
        {
            string filename = "npr-prototype-" + attempt + "-" + frames.Count.ToString("D2") + "-" + name;
            yield return SmokeUi.Capture("PathOfDawn", filename, 1920, 1080, settleSeconds: .6f);
            var frame = new Frame
            {
                png = filename + ".png", scope = scope, uiView = App.Ui.CurrentView,
                quest = App.Chronicle?.CurrentQuest?.Id ?? "",
                questStage = App.Chronicle != null ? App.Chronicle.Stage.ToString() : "",
                player = App.Player.Position, playerYaw = App.Player.FacingYaw,
                camera = App.SceneCamera.transform.position, cameraYaw = App.SceneCamera.transform.eulerAngles.y,
                cameraPitch = App.SceneCamera.transform.eulerAngles.x, orbitYaw = App.CameraRig.Yaw,
                orbitPitch = App.CameraRig.Pitch, models = models
            };
            frames.Add(frame);
            string output = Path.Combine(captureRoot, "PathOfDawn", filename + ".prototype.json");
            Assert.That(File.Exists(output), Is.False);
            File.WriteAllText(output, JsonUtility.ToJson(frame, true));
            Debug.Log("FIRSTGAME_NPR_PROTOTYPE_ORIGINAL " + Path.Combine(captureRoot, "PathOfDawn", frame.png)
                + " scope=" + scope + " visualApproval=false releaseEvidence=false");
        }

        GospelPilgrimageActor AddModel(string model, int index)
        {
            var owner = new GameObject("Prototype model-only physical owner · " + model).transform;
            owner.SetParent(fixture.transform, false); owner.localPosition = new Vector3((index - 2) * 1.18f, 0, 0);
            owner.position = new Vector3(owner.position.x, App.World.HeightAt(owner.position.x, owner.position.z) + .035f, owner.position.z);
            var actor = model == "JesusChild" || model == "JesusAdult"
                ? GospelPilgrimageActor.Build(owner, App.SceneCamera, model == "JesusAdult")
                : GospelPilgrimageActor.BuildNpc(owner, App.SceneCamera, model == "Mary" ? "mary" : model == "Joseph" ? "joseph" : "temple-teacher");
            actor.SetGroundSampler(App.World.HeightAt); actor.SetMotion(Vector3.zero, true, 0);
            Assert.That(actor.HasSkinnedModel, Is.True, model + " must be the actual prepared skinned model.");
            return actor;
        }

        IEnumerator RecordModelFixtures()
        {
            bool canvasActive = App.Ui.CanvasView.gameObject.activeSelf;
            bool mainActorActive = App.Actor.gameObject.activeSelf;
            bool cameraEnabled = App.CameraRig.enabled;
            var target = App.CameraRig.Target;
            fixture = new GameObject("Prototype only · five real model fixtures, not story NPCs");
            fixture.transform.position = App.Player.Position;
            string[] models = { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" };
            try
            {
                App.Ui.CanvasView.gameObject.SetActive(false); App.Actor.gameObject.SetActive(false);
                App.CameraRig.enabled = false;
                var actors = models.Select((model, index) => AddModel(model, index)).ToArray();
                yield return new WaitForSecondsRealtime(.8f);
                var group = actors[0].VisualBounds;
                foreach (var actor in actors.Skip(1)) group.Encapsulate(actor.VisualBounds);
                App.SceneCamera.transform.position = group.center + new Vector3(0, .5f, 8.2f);
                App.SceneCamera.transform.LookAt(group.center, Vector3.up);
                yield return Shot("five-model-lineup", "isolated visual fixture in actual world/light; no quest actors", models);
                for (int i = 0; i < actors.Length; i++)
                {
                    for (int j = 0; j < actors.Length; j++) actors[j].VisualFootOrigin.parent.gameObject.SetActive(i == j);
                    var actor = actors[i];
                    var bounds = actor.VisualBounds;
                    float height = GospelNprCharacterView.HeightFor(models[i]);
                    float distance = bounds.size.y / (2 * Mathf.Tan(App.SceneCamera.fieldOfView * Mathf.Deg2Rad * .5f)) * 1.3f;
                    App.SceneCamera.transform.position = bounds.center + new Vector3(height * .06f, height * .06f, distance);
                    App.SceneCamera.transform.LookAt(bounds.center, Vector3.up);
                    yield return Shot(models[i] + "-body", "isolated full body fixture; actual model/materials/animation/world light", models[i]);
                    var head = actor.Animator.GetBoneTransform(HumanBodyBones.Head);
                    Assert.That(head, Is.Not.Null, models[i]);
                    Vector3 faceTarget = head.position - Vector3.up * (height * .035f);
                    App.SceneCamera.transform.position = faceTarget + new Vector3(height * .02f, height * .025f, height * .65f);
                    App.SceneCamera.transform.LookAt(faceTarget, Vector3.up);
                    yield return Shot(models[i] + "-face", "isolated face fixture; same actual model and world lighting, not painted portrait", models[i]);
                }
            }
            finally
            {
                if (fixture != null) { fixture.SetActive(false); UnityEngine.Object.Destroy(fixture); fixture = null; }
                App.Actor.gameObject.SetActive(mainActorActive); App.Ui.CanvasView.gameObject.SetActive(canvasActive);
                App.CameraRig.enabled = cameraEnabled; App.CameraRig.SetTarget(target); App.CameraRig.Snap();
            }
            yield return null;
        }

        static Vector3 Flat(Vector3 value) { value.y = 0; return value.normalized; }

        IEnumerator WalkToActualEncounter()
        {
            float deadline = Time.realtimeSinceStartup + 30, progressTime = Time.realtimeSinceStartup;
            Vector3 previous = App.Player.Position;
            try
            {
                App.Ui.OnRun?.Invoke(true);
                while (!App.CanInteract && Time.realtimeSinceStartup < deadline)
                {
                    Vector3 delta = App.ObjectivePosition - App.Player.Position; delta.y = 0;
                    float x = Vector3.Dot(delta, Flat(App.SceneCamera.transform.right));
                    float y = Vector3.Dot(delta, Flat(App.SceneCamera.transform.forward));
                    App.Ui.OnMove?.Invoke(new Vector2(x, y).normalized);
                    yield return new WaitForSecondsRealtime(.06f);
                    if ((App.Player.Position - previous).sqrMagnitude > .0064f)
                    { previous = App.Player.Position; progressTime = Time.realtimeSinceStartup; }
                    else if (Time.realtimeSinceStartup - progressTime > 1.2f)
                    {
                        App.Ui.OnMove?.Invoke(Mathf.Abs(x) > Mathf.Abs(y) ? Vector2.up : Vector2.right);
                        yield return new WaitForSecondsRealtime(.28f); progressTime = Time.realtimeSinceStartup;
                    }
                }
            }
            finally
            {
                App.Ui.OnMove?.Invoke(Vector2.zero); App.Ui.OnRun?.Invoke(false);
            }
            yield return new WaitForSecondsRealtime(.15f);
            if (!App.CanInteract) yield return Shot("actual-travel-blocked", "actual controller input failed to reach encounter; diagnosis only");
            Assert.That(App.CanInteract, Is.True, "Prototype uses actual controller movement/proximity; it never teleports or injects a Chronicle cursor.");
        }

        [UnityTest]
        public IEnumerator RecordUnreviewedPrototypeFramesFromActualAppAndFiveModels()
        {
            yield return Shot("title", "actual Boot title, existing world, actual UI");
            yield return RecordModelFixtures();
            yield return Click("새 여정 시작");
            bool mary = false, joseph = false;
            int openingTurns = 0;
            while (App.Chronicle.CurrentQuest.IsPrologue && openingTurns++ < 60)
            {
                var turn = App.Chronicle.CurrentStoryBeat;
                if (!mary && turn.Speaker == "마리아")
                {
                    yield return Shot("mary-opening-dialogue", "actual opening turn and same-model UI portrait; illustrated opening remains visible", "Mary");
                    mary = true;
                }
                if (!joseph && turn.Speaker == "요셉")
                {
                    yield return Shot("joseph-opening-dialogue", "actual opening turn and same-model UI portrait; illustrated opening remains visible", "Joseph");
                    joseph = true;
                }
                yield return Click("이야기 계속");
            }
            Assert.That(mary && joseph, Is.True, "Record real Mary/Joseph turns through actual opening progression.");
            Assert.That(App.Chronicle.CurrentQuest.IsPrologue, Is.False);
            yield return Click("이야기 속으로");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("field-hud", "actual first field, default RPG camera, NPCs and actual HUD/minimap", "JesusChild");
            yield return WalkToActualEncounter();
            App.Ui.OnInteract?.Invoke();
            yield return SmokeUi.WaitUntil(() => App.Ui.CurrentView == "story", 3, "Actual nearby UI interaction did not open the field story.");
            int encounter = App.Chronicle.QuestIndex, details = 0;
            while (App.Chronicle.QuestIndex == encounter && App.Chronicle.CurrentStoryBeat.Speaker != "요셉" && details++ < 15)
                yield return Click("이야기 계속");
            Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(encounter));
            Assert.That(App.Chronicle.CurrentStoryBeat.Speaker, Is.EqualTo("요셉"));
            yield return Shot("joseph-live-field-conversation", "actual accepted encounter; real world, NPC/player and UI portrait", "JesusChild", "Joseph");
            yield return Click("성경");
            yield return Shot("bible-discovered-spread", "actual Bible UI populated only by discovered opening passages");
            yield return Click("표지");
            yield return Shot("bible-cover", "actual Bible cover UI");
            var index = new CaptureIndex { attempt = attempt, unityVersion = Application.unityVersion,
                captureSequenceReachedEnd = true, frames = frames.ToArray() };
            string output = Path.Combine(captureRoot, "PathOfDawn", "npr-prototype-" + attempt + "-capture-index.json");
            Assert.That(File.Exists(output), Is.False); File.WriteAllText(output, JsonUtility.ToJson(index, true));
            Debug.Log("FIRSTGAME_NPR_PROTOTYPE_CAPTURE_SEQUENCE_END originals=" + frames.Count
                + " visualApproval=false releaseEvidence=false index=" + output);
        }
    }
}
