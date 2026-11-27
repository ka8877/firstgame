using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Actual field dialogue, camera draws and unchanged physical saves.
    /// A pass does not certify art quality or replace the retained fifteen NPR cases.</summary>
    public sealed class GospelNprPolishSmokeTests
    {
        const string PreservedSlot = "polish-untouched-source-player-slot";
        IDisposable storage;
        MemorySaveBackend backend;
        Keyboard keyboard;
        InputSettings previousInputs, testInputs;
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
            backend = new MemorySaveBackend(); backend.WriteAtomic(PreservedSlot, "untouched original player bytes");
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
            Assert.That(App, Is.Not.Null); SmokeUi.AssertNoMissingScripts();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Keys(); yield return SmokeUi.UnloadBoot();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousInputs != null) InputSystem.settings = previousInputs;
            if (backend != null) Assert.That(backend.Read(PreservedSlot), Is.EqualTo("untouched original player bytes"));
            storage?.Dispose();
            if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", oldSlot); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            if (hadMuted) PlayerPrefs.SetInt("GOSPEL.music.muted", oldMuted); else PlayerPrefs.DeleteKey("GOSPEL.music.muted");
            if (hadVolume) PlayerPrefs.SetFloat("GOSPEL.music.volume", oldVolume); else PlayerPrefs.DeleteKey("GOSPEL.music.volume");
            if (hadVoice) PlayerPrefs.SetInt("GOSPEL.voice.enabled", oldVoice); else PlayerPrefs.DeleteKey("GOSPEL.voice.enabled");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (testInputs != null && testInputs != previousInputs) UnityEngine.Object.DestroyImmediate(testInputs);
        }

        static void VerifyPrincipalCast()
        {
            var portraits = new HashSet<Mesh>();
            var costumes = new HashSet<Mesh>();
            foreach (string id in new[] { "JesusChild", "Mary", "Joseph", "JesusAdult", "Teacher" })
            {
                string folder = id == "JesusChild" ? "GospelHero20261119" : "GospelNpr";
                var model = Resources.Load<GameObject>("Art/" + folder + "/Characters/" + id);
                Assert.That(model, Is.Not.Null, id);
                var rigs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var face = rigs.Single(r => r.name == id + "__SculptedPortrait");
                var clothes = rigs.Single(r => r.name == id + "__TravelCostume");
                Assert.That(face.enabled && clothes.enabled && portraits.Add(face.sharedMesh) && costumes.Add(clothes.sharedMesh), Is.True, id);
                Assert.That(face.bones.All(bone => bone != null), Is.True);
                Assert.That(clothes.sharedMesh.boneWeights.Length, Is.EqualTo(clothes.sharedMesh.vertexCount));
                foreach (string pose in new[] { "blink", "aa", "ih", "oh", "relaxed", "concerned", "happy" })
                    Assert.That(face.sharedMesh.GetBlendShapeIndex(pose), Is.GreaterThanOrEqualTo(0), id + " " + pose);
                Assert.That(face.sharedMaterials[1].mainTexture, Is.Not.Null, id + " iris");
                Assert.That(clothes.sharedMaterials[0].mainTexture, Is.Not.Null, id + " woven repeat");
            }
        }

        [UnityTest]
        public IEnumerator ActualMaryDialogueKeepsBothWorldActorsVisibleAndPhysicalSavesStable()
        {
            VerifyPrincipalCast();
            yield return Click("새 여정 시작"); int turns = 0;
            while (App.Chronicle.CurrentQuest.IsPrologue && turns++ < 60) yield return Click("이야기 계속");
            Assert.That(turns, Is.EqualTo(51), "Enter through the real preserved opening rather than injecting a later quest.");
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(11));
            yield return Click("이야기 속으로");
            Assert.That(App.People.CurrentPerson.Id, Is.EqualTo("joseph"));
            yield return ReachCurrentPerson(); yield return AcceptCurrentPerson();
            yield return SmokeUi.Capture("PathOfDawn", "galilee-" + attempt + "-joseph-at-work", 1280, 720, settleSeconds: .55f);
            int josephEvent = App.Chronicle.QuestIndex, guard = 0;
            while (App.Chronicle.QuestIndex == josephEvent && guard++ < 40) yield return Click("이야기 계속");
            Assert.That(guard, Is.LessThan(40));
            if (App.Ui.CurrentView == "scripture-acquired") yield return Click("이야기 계속");
            Assert.That(App.People.CurrentPerson.Id, Is.EqualTo("mary"));
            // This formerly problematic exploration orbit must not dictate the
            // dialogue camera, or be overwritten by the temporary composition.
            App.CameraRig.SetOrbit(137, 18); App.CameraRig.SetZoom(5.6f); App.CameraRig.Snap();
            yield return ReachCurrentPerson(); yield return AcceptCurrentPerson();
            yield return SmokeUi.WaitUntil(() => App.Player.IsGrounded && App.Player.Velocity.sqrMagnitude < .001f, 4,
                "The traveler must finish its real ground contact before testing camera-only physical invariance.");
            var maryRoot = App.People.CurrentPerson.RootTransform;
            int maryIdentity = maryRoot.GetInstanceID(), playerIdentity = App.Player.GetInstanceID();
            Vector3 physical = App.Player.Position, maryPosition = maryRoot.position;
            Quaternion playerRotation = App.Player.transform.rotation, maryRotation = maryRoot.rotation;
            float facing = App.Player.FacingYaw, requestedYaw = App.CameraRig.Yaw, requestedZoom = App.CameraRig.Zoom;
            string line = App.Chronicle.CurrentStoryBeat.Text, voiceKey = App.Chronicle.StoryVoiceKey;
            int quest = App.Chronicle.QuestIndex, discovered = App.Chronicle.DiscoveredCount;
            yield return AssertCurrentPairDraws("mary-two-person-world");
            AssertPhysicalPair(physical, facing, playerRotation, maryPosition, maryRotation, playerIdentity, maryIdentity);
            App.ShowBible(); yield return null; yield return null;
            Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"));
            Assert.That(App.CameraRig.ConversationTarget, Is.Null);
            Assert.That(App.CameraRig.Yaw, Is.EqualTo(requestedYaw).Within(.001f));
            Assert.That(App.CameraRig.Zoom, Is.EqualTo(requestedZoom).Within(.001f));
            yield return Click("여정으로");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("story"));
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(voiceKey));
            Assert.That(App.Chronicle.CurrentStoryBeat.Text, Is.EqualTo(line));
            yield return AssertCurrentPairDraws("mary-dialogue-after-bible");
            AssertPhysicalPair(physical, facing, playerRotation, maryPosition, maryRotation, playerIdentity, maryIdentity);
            Assert.That(App.CameraRig.Yaw, Is.EqualTo(requestedYaw).Within(.001f));
            Assert.That(App.CameraRig.Zoom, Is.EqualTo(requestedZoom).Within(.001f));
            Assert.That(App.Save(false), Is.True); string slot = App.ActiveSaveSlot;
            yield return SmokeUi.LoadBoot(); yield return Click("이어하기");
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(slot));
            Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(quest));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(discovered));
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(voiceKey));
            Assert.That(App.Chronicle.CurrentStoryBeat.Text, Is.EqualTo(line));
            Assert.That(App.People.CurrentPerson.Id, Is.EqualTo("mary"));
            Assert.That(Vector3.Distance(App.Player.Position, physical), Is.LessThan(.12f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, facing)), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(App.People.CurrentPerson.Position, maryPosition), Is.LessThan(.12f));
            yield return AssertCurrentPairDraws("mary-saved-dialogue-reloaded");
        }

        IEnumerator AssertCurrentPairDraws(string shot)
        {
            yield return SmokeUi.WaitUntil(() => App.CameraRig.HasReadableConversationView, 5,
                "The actual Mary/player camera must find a two-body view without masking the speaker.");
            var owner = App; var camera = owner.SceneCamera; var rig = owner.CameraRig;
            var speaker = owner.People.CurrentPerson.RootTransform.GetComponentInChildren<GospelPilgrimageActor>();
            var mask = camera.GetComponent<GospelNprNpcCameraOcclusion>();
            Assert.That(speaker.HasSkinnedModel, Is.True); Assert.That(speaker.CharacterView.ModelIdentity, Is.EqualTo("Mary"));
            Assert.That(owner.Actor.SkinnedRenderers.Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy
                && !renderer.forceRenderingOff), Is.True, "The actual player world model must be drawing before observation.");
            Assert.That(speaker.SkinnedRenderers.Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy
                && !renderer.forceRenderingOff), Is.True, "The actual Mary world model must be drawing before observation.");
            Assert.That(owner.Actor.SkinnedRenderers.Concat(speaker.SkinnedRenderers)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
                .All(renderer => !renderer.forceRenderingOff), Is.True, "Do not accept an already masked speaker/player baseline.");
            var playerFlags = owner.Actor.SkinnedRenderers.Select(renderer => renderer.forceRenderingOff).ToArray();
            var speakerFlags = speaker.SkinnedRenderers.Select(renderer => renderer.forceRenderingOff).ToArray();
            int renders = 0; bool playerDraws = true, speakerDraws = true, observedPair = true;
            Action<ScriptableRenderContext, Camera> begin = (context, actual) =>
            {
                if (actual != camera) return;
                renders++;
                playerDraws &= owner.Actor.SkinnedRenderers.Select((renderer, index) => renderer.forceRenderingOff == playerFlags[index]).All(value => value);
                speakerDraws &= speaker.SkinnedRenderers.Select((renderer, index) => renderer.forceRenderingOff == speakerFlags[index]).All(value => value);
                observedPair &= mask.LastProtectedConversationNpcCount == 1 && rig.IsConversationActor(speaker)
                    && rig.HasReadableConversationView && (rig.IsSpeakerCloseup || rig.ConversationScreenSeparation >= .07f);
            };
            RenderPipelineManager.beginCameraRendering += begin;
            try
            {
                // Native batch mode does not automatically render the GameView.
                // Submit two real URP draws, as SmokeUi.Capture does, before
                // inspecting callback observations. Renderer-default inspection
                // alone must never satisfy the unchanged two-draw obligation.
                var target = RenderTexture.GetTemporary(640, 360, 24, RenderTextureFormat.ARGB32);
                try
                {
                    var request = new RenderPipeline.StandardRequest { destination = target };
                    Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    yield return null;
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                finally { RenderTexture.ReleaseTemporary(target); }
                Assert.That(renders, Is.GreaterThanOrEqualTo(2), "Observe real SceneCamera draws, not just renderer defaults between frames.");
                yield return SmokeUi.Capture("PathOfDawn", "npr-polish-" + attempt + "-" + shot, 1280, 720, settleSeconds: .15f);
                Assert.That(playerDraws && speakerDraws && observedPair, Is.True,
                    "Both real world actors must retain their draw flags during actual camera renders.");
                Assert.That(rig.ConversationObstructionCount, Is.EqualTo(0));
                Assert.That(camera.fieldOfView, Is.EqualTo(47).Within(.001f));
                Assert.That(rig.IsSpeakerCloseup, Is.True);
                AssertSpeakerFace(camera, speaker);
                var gaze = owner.Player.Position - speaker.transform.position;
                var toCamera = camera.transform.position - speaker.transform.position;
                Assert.That(Vector3.Angle(Vector3.ProjectOnPlane(gaze, Vector3.up), Vector3.ProjectOnPlane(toCamera, Vector3.up)),
                    Is.LessThan(63), "Show the speaking face from the front or three quarters.");
            }
            finally { RenderPipelineManager.beginCameraRendering -= begin; }
            Assert.That(speaker.SkinnedRenderers.Select((renderer, index) => renderer.forceRenderingOff == speakerFlags[index]).All(value => value), Is.True);
        }

        static void AssertSpeakerFace(Camera camera, GospelPilgrimageActor actor)
        {
            var animator = actor.Animator;
            var head = animator.GetBoneTransform(HumanBodyBones.Head).position;
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck).position;
            var points = new[] { head, neck };
            float projectedFace = Mathf.Abs(camera.WorldToViewportPoint(head + Vector3.up * actor.VisualHeight * .08f).y
                - camera.WorldToViewportPoint(neck).y);
            Assert.That(projectedFace, Is.GreaterThan(.12f), "Faces must occupy enough of the dialogue view to read expressions.");
            foreach (Vector3 point in points)
            {
                Vector3 viewport = camera.WorldToViewportPoint(point);
                Assert.That(viewport.z, Is.GreaterThan(camera.nearClipPlane + .03f));
                Assert.That(viewport.x, Is.InRange(.07f, .93f));
                Assert.That(viewport.y, Is.InRange(.35f, .93f), "Keep both real heads clear of the dialogue panel and frame edges.");
            }
        }

        void AssertPhysicalPair(Vector3 position, float yaw, Quaternion rotation, Vector3 maryPosition,
            Quaternion maryRotation, int playerIdentity, int maryIdentity)
        {
            Assert.That(App.Player.GetInstanceID(), Is.EqualTo(playerIdentity));
            Assert.That(App.People.CurrentPerson.RootTransform.GetInstanceID(), Is.EqualTo(maryIdentity));
            Assert.That(Vector3.Distance(App.Player.Position, position), Is.LessThan(.001f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, yaw)), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(App.Player.transform.rotation, rotation), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(App.People.CurrentPerson.Position, maryPosition), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(App.People.CurrentPerson.RootTransform.rotation, maryRotation), Is.LessThan(.001f));
        }

        void Keys(params Key[] keys)
        { if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
        IEnumerator Click(string label) { SmokeUi.Click(label); yield return null; yield return null; }
        IEnumerator AcceptCurrentPerson()
        {
            Keys(Key.E); yield return null; yield return null; Keys();
            yield return SmokeUi.WaitUntil(() => App.Chronicle.Stage == GospelQuestStage.Reading, 3, "Real nearby E input did not accept the current encounter.");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("story"));
        }
        IEnumerator ReachCurrentPerson()
        {
            float deadline = Time.realtimeSinceStartup + 45, lastProgress = Time.realtimeSinceStartup;
            Vector3 previous = App.Player.Position;
            while (!App.CanInteract && Time.realtimeSinceStartup < deadline)
            {
                Vector3 delta = App.ObjectivePosition - App.Player.Position; delta.y = 0;
                if (delta.magnitude < .82f) { Keys(); yield return new WaitForSecondsRealtime(.06f); continue; }
                Vector3 right = Flat(App.SceneCamera.transform.right), forward = Flat(App.SceneCamera.transform.forward);
                float x = Vector3.Dot(delta, right), y = Vector3.Dot(delta, forward);
                var held = new List<Key> { Key.LeftShift };
                if (Mathf.Abs(x) > .25f) held.Add(x > 0 ? Key.D : Key.A);
                if (Mathf.Abs(y) > .25f) held.Add(y > 0 ? Key.W : Key.S);
                Keys(held.ToArray()); yield return new WaitForSecondsRealtime(.06f);
                if (Vector3.Distance(previous, App.Player.Position) > .08f) { previous = App.Player.Position; lastProgress = Time.realtimeSinceStartup; }
                else if (Time.realtimeSinceStartup - lastProgress > 1.2f)
                {
                    Keys(Key.LeftShift, Mathf.Abs(x) > Mathf.Abs(y) ? Key.W : Key.D);
                    yield return new WaitForSecondsRealtime(.28f); Keys(); lastProgress = Time.realtimeSinceStartup;
                }
            }
            Keys(); yield return new WaitForSecondsRealtime(.12f);
            Assert.That(App.CanInteract, Is.True, "Ordinary camera-relative movement must reach the actual unchanged encounter.");
        }
        static Vector3 Flat(Vector3 value) { value.y = 0; return value.sqrMagnitude > .0001f ? value.normalized : Vector3.forward; }
    }
}
