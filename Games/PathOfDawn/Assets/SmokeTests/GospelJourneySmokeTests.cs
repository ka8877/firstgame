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
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Current Gospel entry path. Retired observer/rewind tests remain historical fixtures.</summary>
    public sealed class GospelJourneySmokeTests
    {
        IDisposable storageScope;
        MemorySaveBackend backend;
        Keyboard keyboard;
        InputSettings previousInputs, fixtureInputs;
        bool hadActiveSlot;
        string originalActiveSlot;
        GospelJourneyApp App => UnityEngine.Object.FindFirstObjectByType<GospelJourneyApp>();

        [UnitySetUp]
        public IEnumerator EnterGame()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            backend = new MemorySaveBackend();
            backend.WriteAtomic("preserved-observer-slot", "old-observer-save-bytes");
            storageScope = PersistentSaves.UseBackendForTesting(backend);
            hadActiveSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot");
            originalActiveSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            previousInputs = InputSystem.settings;
            fixtureInputs = UnityEngine.Object.Instantiate(previousInputs);
            fixtureInputs.hideFlags = HideFlags.HideAndDontSave;
            fixtureInputs.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            fixtureInputs.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = fixtureInputs;
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SmokeUi.LoadBoot();
        }

        [UnityTearDown]
        public IEnumerator LeaveGame()
        {
            if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return SmokeUi.UnloadBoot();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousInputs != null) InputSystem.settings = previousInputs;
            if (fixtureInputs != null) UnityEngine.Object.Destroy(fixtureInputs);
            storageScope?.Dispose(); storageScope = null;
            if (hadActiveSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", originalActiveSlot);
            else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        IEnumerator BeginTemple()
        {
            Assert.That(App, Is.Not.Null, "Boot must launch the Gospel journey, rather than the retired observer adventure.");
            Assert.That(UnityEngine.Object.FindFirstObjectByType<PathOfDawnApp>(), Is.Null,
                "The previous entry component must not create a second world or UI.");
            SmokeUi.Click("새 여정"); yield return null;
            Assert.That(App.Journey.Phase, Is.EqualTo(GospelPhase.Prologue));
            for (int page = 0; page < 3; page++)
            {
                Assert.That(App.Journey.ProloguePage, Is.EqualTo(page));
                Assert.That(SmokeUi.HasText(App.Journey.CurrentReading.Reference), Is.True,
                    "The actual Scripture reference must remain visible while reading.");
                SmokeUi.Click(page < 2 ? "다음 구절" : "성전으로");
                yield return null;
            }
            yield return SmokeUi.WaitUntil(() => App.Player != null && App.Player.InputEnabled, 10,
                "Birth readings must lead to the physical temple exploration scene.");
            Assert.That(App.Journey.Phase, Is.EqualTo(GospelPhase.Exploring));
            Assert.That(App.Journey.CollectedReadings.Count, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator BirthScripturePhysicalWalkingAndContinueRestoreTheRealJourney()
        {
            yield return SmokeUi.Capture("PathOfDawn", "gospel-01-title", 1280, 720);
            yield return BeginTemple();
            Assert.That(UnityEngine.Object.FindFirstObjectByType<DawnIllustratedStage>(), Is.Null,
                "Current exploration must not render the retired living-picture stage.");
            var motor = App.Player;
            Assert.That(motor.Controller, Is.Not.Null);
            Assert.That(motor.Controller.enabled, Is.True);
            Assert.That(motor.Animator, Is.Not.Null, "The active character needs a real animation controller.");
            Assert.That(motor.Animator.runtimeAnimatorController, Is.Not.Null);
            var skin = motor.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(s => s.sharedMesh != null && s.bones.Length >= 12);
            Assert.That(skin, Is.Not.Null, "An articulated skinned character must replace the static illustration.");
            Assert.That(skin.sharedMesh.vertexCount, Is.GreaterThan(200));
            var actualSkins = motor.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.That(actualSkins.SelectMany(s => s.sharedMaterials).All(m => m != null
                && m.name.StartsWith("Gospel_", StringComparison.Ordinal)
                && m.shader.name == "Universal Render Pipeline/Lit"), Is.True,
                "Actual rendered materials must be authored URP materials, not extracted FBX defaults.");
            var actualEyes = actualSkins.First(s => s.name == "Eyes");
            Assert.That(actualEyes.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null,
                "The visible protagonist must use the actual eye albedo texture.");
            yield return SmokeUi.Capture("PathOfDawn", "gospel-02-temple-idle", 1280, 720);
            var beforePosition = motor.transform.position;
            var beforeBones = skin.bones.Select(bone => bone.localRotation).ToArray();
            yield return Hold(new[] { Key.W }, .85f);
            float distance = Vector3.Distance(beforePosition, motor.transform.position);
            Assert.That(distance, Is.GreaterThan(.35f), "Queued real W input must move the CharacterController through the scene.");
            Assert.That(motor.MeasuredSpeed, Is.GreaterThan(.1f));
            Assert.That(skin.bones.Where((bone, index) => Quaternion.Angle(beforeBones[index], bone.localRotation) > .5f).Count(),
                Is.GreaterThanOrEqualTo(2), "Walk playback must articulate the rig instead of translating a still picture.");
            yield return SmokeUi.Capture("PathOfDawn", "gospel-03-temple-walking", 1280, 720);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(motor.PlanarVelocity.magnitude, Is.LessThan(.06f), "Releasing movement must stop the physical motor.");
            var stopped = motor.transform.position;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Vector2.Distance(new Vector2(stopped.x, stopped.z), new Vector2(motor.transform.position.x, motor.transform.position.z)),
                Is.LessThan(.04f), "Idle must not slide across the floor.");
            var pointer = InputSystem.AddDevice<Mouse>();
            try
            {
                float initialDistance = Vector3.Distance(App.World.View.transform.position, motor.transform.position);
                InputSystem.QueueStateEvent(pointer, new MouseState { buttons = 2, delta = new Vector2(1380, 90), scroll = new Vector2(0, 2200) });
                yield return null;
                yield return new WaitForSecondsRealtime(.15f);
                InputSystem.QueueStateEvent(pointer, new MouseState());
                yield return new WaitForSecondsRealtime(.7f);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, motor.CameraYaw)), Is.GreaterThan(100),
                    "Real right-button pointer input must orbit to the protagonist's front.");
                Assert.That(Vector3.Distance(App.World.View.transform.position, motor.transform.position), Is.LessThan(initialDistance),
                    "Real wheel input must bring the camera closer to the articulated character.");
                yield return SmokeUi.Capture("PathOfDawn", "gospel-03b-protagonist-front", 1280, 720);
                CaptureCharacterPose(motor);
            }
            finally { InputSystem.RemoveDevice(pointer); }
            yield return SmokeUi.Capture("PathOfDawn", "gospel-04-temple-mobile", 844, 390);
            SmokeUi.Click("저장"); yield return null;
            var saved = ReadSave();
            var savedPosition = new Vector3(saved.playerX, saved.playerY, saved.playerZ);
            Assert.That(Vector3.Distance(savedPosition, motor.transform.position), Is.LessThan(.04f));
            Assert.That(saved.objectiveIndex, Is.EqualTo(0));
            Assert.That(saved.collectedReadings.Count, Is.EqualTo(3));
            int previousApp = App.GetInstanceID();
            yield return SmokeUi.LoadBoot();
            Assert.That(App.GetInstanceID(), Is.Not.EqualTo(previousApp));
            SmokeUi.Click("이어하기"); yield return null;
            Assert.That(App.Journey.ObjectiveIndex, Is.EqualTo(saved.objectiveIndex));
            Assert.That(App.Journey.CollectedReadings, Is.EqualTo(saved.collectedReadings));
            Assert.That(Vector3.Distance(App.Player.transform.position, savedPosition), Is.LessThan(.05f));
            Assert.That(backend.Read("preserved-observer-slot"), Is.EqualTo("old-observer-save-bytes"));
            yield return SmokeUi.Capture("PathOfDawn", "gospel-05-continued-temple", 1280, 720);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RealApproachAndInteractionFollowScriptureObjectivesToNazareth()
        {
            yield return BeginTemple();
            yield return Press(Key.E);
            Assert.That(App.Journey.Phase, Is.EqualTo(GospelPhase.Exploring),
                "Interaction outside the first landmark radius must not advance Bible-study progress.");
            for (int i = 0; i < 4; i++)
            {
                string objectiveId = App.Journey.CurrentObjective.Id;
                yield return WalkTo(App.World.LandmarkPosition(objectiveId));
                yield return Press(Key.E);
                Assert.That(App.Journey.Phase, Is.EqualTo(GospelPhase.Reading), "Nearby E interaction must open the actual landmark Scripture.");
                Assert.That(SmokeUi.HasText(App.Journey.CurrentReading.Reference), Is.True);
                Assert.That(App.Player.InputEnabled, Is.False, "Movement must pause during Scripture reading.");
                yield return SmokeUi.Capture("PathOfDawn", "gospel-reading-" + objectiveId, 1280, 720);
                SmokeUi.Click("말씀을 따라 걷기"); yield return null;
                Assert.That(App.Journey.ObjectiveIndex, Is.EqualTo(i + 1));
            }
            Assert.That(App.Journey.Phase, Is.EqualTo(GospelPhase.Completed));
            Assert.That(App.Journey.CollectedReadings.Count, Is.EqualTo(7));
            SmokeUi.Click("저장"); yield return null;
            Assert.That(ReadSave().phase, Is.EqualTo(GospelPhase.Completed));
            yield return SmokeUi.Capture("PathOfDawn", "gospel-06-first-journey-completed", 1280, 720);
            LogAssert.NoUnexpectedReceived();
        }

        [Serializable]
        sealed class CharacterPose
        {
            public Vector3 prefabEuler, actorForward, cameraPosition, headPosition, eyesPosition;
            public float cameraForwardDot, eyesForwardDot;
            public string headBone, eyesRenderer;
            public string[] renderers;
        }

        void CaptureCharacterPose(GospelThirdPersonMotor motor)
        {
            var skins = motor.GetComponentsInChildren<SkinnedMeshRenderer>();
            var head = skins.SelectMany(s => s.bones).FirstOrDefault(b => b != null && b.name.Equals("head", StringComparison.OrdinalIgnoreCase));
            var eyes = skins.FirstOrDefault(s => s.name.ToLowerInvariant().Contains("eyes"));
            var eyeDirection = head != null && eyes != null ? eyes.bounds.center - head.position : Vector3.zero;
            eyeDirection.y = 0;
            var toCamera = App.World.View.transform.position - motor.transform.position; toCamera.y = 0;
            var result = new CharacterPose {
                prefabEuler = Resources.Load<GameObject>(GospelTempleWorld.HeroResource).transform.localEulerAngles,
                actorForward = motor.transform.forward, cameraPosition = App.World.View.transform.position,
                headPosition = head != null ? head.position : Vector3.zero, eyesPosition = eyes != null ? eyes.bounds.center : Vector3.zero,
                headBone = head != null ? head.name : "missing", eyesRenderer = eyes != null ? eyes.name : "missing",
                cameraForwardDot = Vector3.Dot(toCamera.normalized, motor.transform.forward),
                eyesForwardDot = Vector3.Dot(eyeDirection, motor.transform.forward),
                renderers = skins.Select(s => s.name + " · " + string.Join(",", s.sharedMaterials.Select(m => m != null ? m.name : "missing"))).ToArray()
            };
            var directory = System.IO.Path.GetFullPath("../../Artifacts/Validation/Revival");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "native-gospel-character-pose.json"), JsonUtility.ToJson(result, true));
        }

        IEnumerator Hold(Key[] keys, float seconds)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
            // The caller can inspect the actual moving pose before releasing.
        }

        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        IEnumerator WalkTo(Vector3 target)
        {
            var motor = App.Player;
            float deadline = Time.realtimeSinceStartup + 60;
            while (Time.realtimeSinceStartup < deadline)
            {
                Vector3 delta = target - motor.transform.position; delta.y = 0;
                if (delta.magnitude < 1.65f) break;
                Vector3 local = Quaternion.Euler(0, -motor.CameraYaw, 0) * delta;
                var keys = new List<Key>();
                if (Mathf.Abs(local.x) > .45f) keys.Add(local.x > 0 ? Key.D : Key.A);
                if (Mathf.Abs(local.z) > .45f) keys.Add(local.z > 0 ? Key.W : Key.S);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
                yield return null;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.2f);
            var remaining = target - motor.transform.position; remaining.y = 0;
            Assert.That(remaining.magnitude, Is.LessThan(2.1f), "Actual keyboard navigation could not reach landmark " + target);
        }

        static GospelSave ReadSave()
        {
            Assert.That(PersistentSaves.TryLoad(GospelJourney.SaveSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, out var payload, out var error), Is.True, error);
            var saved = JsonUtility.FromJson<GospelSave>(payload);
            GospelJourney.New().ValidateSave(saved); return saved;
        }
    }
}
