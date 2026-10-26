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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
        bool hadCameraDistance;
        float originalCameraDistance;
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
            hadCameraDistance = PlayerPrefs.HasKey("GOSPEL.cameraDistance");
            originalCameraDistance = PlayerPrefs.GetFloat("GOSPEL.cameraDistance",4.6f);
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            PlayerPrefs.DeleteKey("GOSPEL.cameraDistance");
            previousInputs = InputSystem.settings;
            fixtureInputs = UnityEngine.Object.Instantiate(previousInputs);
            fixtureInputs.hideFlags = HideFlags.HideAndDontSave;
            fixtureInputs.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            fixtureInputs.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            fixtureInputs.scrollDeltaBehavior = InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
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
            if (hadCameraDistance) PlayerPrefs.SetFloat("GOSPEL.cameraDistance",originalCameraDistance);
            else PlayerPrefs.DeleteKey("GOSPEL.cameraDistance");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        IEnumerator BeginTemple()
        {
            Assert.That(App, Is.Not.Null, "Boot must launch the Gospel journey, rather than the retired observer adventure.");
            Assert.That(UnityEngine.Object.FindFirstObjectByType<PathOfDawnApp>(), Is.Null,
                "The previous entry component must not create a second world or UI.");
            // Start with the actual title callback; reading checks below exercise
            // the visible dialogue buttons for every passage.
            App.StartNew(); yield return null;
            Assert.That(App.Journey, Is.Not.Null, "Starting a new journey must create its Scripture state.");
            Assert.That(App.Journey.Phase, Is.EqualTo(GospelPhase.Prologue));
            int completedPages = 0;
            while (App.Journey.Phase == GospelPhase.Prologue && completedPages < GospelCatalog.PrologueReadings.Count)
            {
                Assert.That(App.Journey.ProloguePage, Is.EqualTo(completedPages));
                Assert.That(SmokeUi.HasText(App.Journey.CurrentReading.Reference), Is.True,
                    "The actual Scripture reference must remain visible while reading.");
                yield return ConsumeCurrentDialogue();
                completedPages++;
            }
            yield return SmokeUi.WaitUntil(() => App.Player != null && App.Player.InputEnabled, 10,
                "Birth readings must lead to the physical temple exploration scene.");
            Assert.That(App.Journey.Phase, Is.EqualTo(GospelPhase.Exploring));
            Assert.That(completedPages, Is.EqualTo(GospelCatalog.PrologueReadings.Count));
            Assert.That(App.Journey.CollectedReadings.Count, Is.EqualTo(GospelCatalog.PrologueReadings.Count));
            foreach(var npc in App.World.GetComponentsInChildren<Animator>())
                if(npc!=App.Player.Animator)
                {
                    Assert.That(npc.GetBool("Grounded"),Is.True,"Stationary NPCs must not enter the airborne animation.");
                    Assert.That(npc.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"),Is.True);
                }
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
            Assert.That(actualSkins.Length, Is.LessThanOrEqualTo(10),
                "Matching-material skin parts must be combined without discarding the articulated body.");
            Assert.That(actualSkins.SelectMany(s => s.bones).Where(b => b != null).Distinct().Count(), Is.GreaterThanOrEqualTo(60));
            foreach (string expected in new[] { "Idle", "Walk", "Run", "JumpStart", "JumpAir", "JumpLand", "Practice" })
                Assert.That(motor.Animator.runtimeAnimatorController.animationClips.Any(c => c.name == expected || c.name.EndsWith("|" + expected) || c.name.EndsWith("_" + expected)), Is.True,
                    "The production character is missing the real joint animation " + expected);
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
                float initialZoom = motor.CameraDistance;
                InputSystem.QueueStateEvent(pointer, new MouseState { buttons = 2, delta = new Vector2(1380, 90), scroll = Vector2.up });
                yield return null;
                yield return new WaitForSecondsRealtime(.15f);
                InputSystem.QueueStateEvent(pointer, new MouseState());
                yield return new WaitForSecondsRealtime(.7f);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, motor.CameraYaw)), Is.GreaterThan(100),
                    "Real right-button pointer input must orbit to the protagonist's front.");
                Assert.That(Vector3.Distance(App.World.View.transform.position, motor.transform.position), Is.LessThan(initialDistance),
                    "Real wheel input must bring the camera closer to the articulated character.");
                Assert.That(initialZoom-motor.CameraDistance,Is.InRange(.5f,.8f),
                    "One normalized wheel tick must produce a perceptible camera change, not a legacy pixel-scale movement.");
                yield return SmokeUi.Capture("PathOfDawn", "gospel-03b-protagonist-front", 1280, 720);
                CaptureCharacterPose(motor);
                InputSystem.QueueStateEvent(pointer,new MouseState{scroll=Vector2.down});yield return null;
                InputSystem.QueueStateEvent(pointer,new MouseState());yield return null;
                Assert.That(motor.CameraDistance,Is.EqualTo(initialZoom).Within(.01f),
                    "A normalized opposite tick must zoom back out by the same usable distance.");
                motor.SetCameraDistance(GospelThirdPersonMotor.MinimumCameraDistance);
                InputSystem.QueueStateEvent(pointer,new MouseState{scroll=Vector2.up});yield return null;
                InputSystem.QueueStateEvent(pointer,new MouseState());yield return null;
                Assert.That(motor.CameraDistance,Is.EqualTo(GospelThirdPersonMotor.MinimumCameraDistance).Within(.001f));
                motor.SetCameraDistance(GospelThirdPersonMotor.MaximumCameraDistance);
                InputSystem.QueueStateEvent(pointer,new MouseState{scroll=Vector2.down});yield return null;
                InputSystem.QueueStateEvent(pointer,new MouseState());yield return null;
                Assert.That(motor.CameraDistance,Is.EqualTo(GospelThirdPersonMotor.MaximumCameraDistance).Within(.001f));
                motor.SetCameraDistance(initialZoom-.65f);
                Assert.That(motor.CameraDistance,Is.InRange(GospelThirdPersonMotor.MinimumCameraDistance,GospelThirdPersonMotor.MaximumCameraDistance));
                var objectiveBeforeStatus=App.Journey.CurrentObjective.Id;
                SmokeUi.Click("상태");yield return null;
                Assert.That(App.IsModal,Is.True);
                Assert.That(motor.InputEnabled,Is.False);
                Assert.That(SmokeUi.HasText("열두 살 · 예루살렘"),Is.True);
                float distanceBeforeModalWheel=motor.CameraDistance;
                InputSystem.QueueStateEvent(pointer,new MouseState{scroll=Vector2.up});
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(motor.CameraDistance,Is.EqualTo(distanceBeforeModalWheel).Within(.001f),
                    "Reading and status modals must own pointer input instead of zooming the world behind them.");
                InputSystem.QueueStateEvent(pointer,new MouseState());
                yield return SmokeUi.Capture("PathOfDawn","gospel-modern-character-status",1280,720);
                SmokeUi.Click("돌아가기");yield return null;
                SmokeUi.Click("-");yield return null;
                Assert.That(motor.CameraDistance,Is.GreaterThan(distanceBeforeModalWheel),"The visible zoom-out button must change camera distance.");
                SmokeUi.Click("+");yield return null;
                Assert.That(motor.CameraDistance,Is.EqualTo(distanceBeforeModalWheel).Within(.001f));
                Assert.That(App.Journey.CurrentObjective.Id,Is.EqualTo(objectiveBeforeStatus),"Camera and status controls must not advance Scripture progress.");
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
                yield return ConsumeCurrentDialogue();
                Assert.That(App.Journey.ObjectiveIndex, Is.EqualTo(i + 1));
            }
            Assert.That(App.Journey.Phase, Is.EqualTo(GospelPhase.Completed));
            Assert.That(App.Journey.CollectedReadings.Count, Is.EqualTo(7));
            SmokeUi.Click("저장"); yield return null;
            Assert.That(ReadSave().phase, Is.EqualTo(GospelPhase.Completed));
            yield return SmokeUi.Capture("PathOfDawn", "gospel-06-first-journey-completed", 1280, 720);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RealSprintJumpLandingAndTouchJumpArticulateTheProductionCharacter()
        {
            yield return BeginTemple();
            var motor = App.Player;
            yield return Grounded(motor);
            yield return Hold(new[] { Key.W }, .75f);
            float walkingSpeed = motor.MeasuredSpeed;
            Assert.That(walkingSpeed, Is.InRange(1.1f,2.1f), "Normal real input must produce a controlled walk.");
            yield return Hold(new[] { Key.W, Key.LeftShift }, .8f);
            float runningSpeed = motor.MeasuredSpeed;
            Assert.That(runningSpeed, Is.GreaterThan(3), "Shift must change the actual capsule speed to a run.");
            Assert.That(runningSpeed, Is.GreaterThan(walkingSpeed + 1));
            Assert.That(motor.State, Is.EqualTo(GospelLocomotionState.Running));
            yield return SmokeUi.Capture("PathOfDawn", "gospel-remaster-running", 1280,720,settleSeconds:.08f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            yield return SmokeUi.WaitUntil(() => motor.PlanarVelocity.magnitude < .06f,5,"Sprint release must brake to a stable idle.");
            yield return Grounded(motor);
            float floor = motor.transform.position.y;
            var skin = motor.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.bones.Length >= 12);
            var beforeBones = skin.bones.Select(b => b.localRotation).ToArray();
            yield return Press(Key.Space);
            bool sawAir = false, sawJointMotion = false;
            float apex = motor.transform.position.y, deadline = Time.realtimeSinceStartup + 8;
            while (Time.realtimeSinceStartup < deadline)
            {
                apex = Mathf.Max(apex,motor.transform.position.y);
                sawAir |= motor.State == GospelLocomotionState.Airborne;
                sawJointMotion |= skin.bones.Where((b,index) => Quaternion.Angle(beforeBones[index],b.localRotation) > 8).Count() >= 2;
                if (sawAir && motor.IsGrounded) break;
                yield return null;
            }
            Assert.That(sawAir,Is.True,"Space must put the physical player in the airborne state.");
            Assert.That(apex - floor,Is.InRange(.45f,1.25f),"Jump must lift the capsule through gravity, with a bounded arc.");
            Assert.That(motor.IsGrounded,Is.True,"The jump must land instead of leaving the player suspended.");
            Assert.That(Mathf.Abs(motor.transform.position.y - floor),Is.LessThan(.08f));
            Assert.That(sawJointMotion,Is.True,"The airborne character must articulate its rig.");
            yield return SmokeUi.Capture("PathOfDawn","gospel-remaster-landed",1280,720,settleSeconds:.1f);
            // This is the same public input path used by the touch jump button.
            motor.JumpPressed();
            yield return SmokeUi.WaitUntil(() => !motor.IsGrounded && motor.transform.position.y > floor + .15f,5,"Touch jump must use the same physical jump as Space.");
            yield return SmokeUi.Capture("PathOfDawn","gospel-remaster-jumping",1280,720,settleSeconds:0);
            yield return Grounded(motor);
            var stopped = motor.transform.position;
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(PlanarDistance(stopped,motor.transform.position),Is.LessThan(.025f),"A landed idle must keep its feet in one place.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EqualGameTimeWalkingTravelsTheSameDistanceAtThirtyAndTwelveFrames()
        {
            yield return BeginTemple();
            float previousCaptureDelta = Time.captureDeltaTime;
            float thirtyFrameDistance = 0, twelveFrameDistance = 0;
            try
            {
                yield return MeasureCapturedWalk(1f / 30,60,value => thirtyFrameDistance = value);
                yield return MeasureCapturedWalk(1f / 12,24,value => twelveFrameDistance = value);
                Assert.That(thirtyFrameDistance,Is.GreaterThan(3),"Two game seconds must cover the expected walk after acceleration.");
                Assert.That(twelveFrameDistance,Is.EqualTo(thirtyFrameDistance).Within(.18f),
                    "A long rendered frame must consume all movement time instead of truncating each frame to .05 seconds.");
                Time.captureDeltaTime = .25f;
                var motor = App.Player; motor.SetPose(new Vector3(3,.08f,-10),0);
                yield return null; yield return Grounded(motor);
                float floor = motor.transform.position.y;
                var jumpButton = SmokeUi.FindButton("점프 · Space");
                Assert.That(jumpButton,Is.Not.Null);
                Assert.That(jumpButton.IsInteractable(),Is.True,
                    "Ground contact must keep the actual jump button enabled when App.Update runs before a slow motor frame.");
                SmokeUi.Click("점프 · Space"); yield return null;
                Assert.That(!motor.IsGrounded && motor.transform.position.y > floor + .1f,Is.True,
                    "A queued touch jump must get a movement frame even when the frame interval exceeds its time buffer.");
                TestContext.WriteLine("GOSPEL_FRAME_RATE_MOVEMENT_OK gameSeconds=2 distanceAt30=" + thirtyFrameDistance + " distanceAt12=" + twelveFrameDistance);
            }
            finally
            {
                Time.captureDeltaTime = previousCaptureDelta;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                if (App != null) App.Player.StopInput();
            }
            LogAssert.NoUnexpectedReceived();
        }

        IEnumerator MeasureCapturedWalk(float captureDelta, int frames, Action<float> measured)
        {
            Time.captureDeltaTime = captureDelta;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            var motor = App.Player; motor.TouchMove = Vector2.zero; motor.TouchRun = false;
            // This unobstructed lane stays clear of the fountain, garden and NPCs.
            motor.SetPose(new Vector3(3,.08f,-10),0);
            yield return null;
            yield return Grounded(motor);
            Vector3 start = motor.transform.position;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
            for (int frame = 0; frame < frames; frame++) yield return null;
            measured(PlanarDistance(start,motor.transform.position));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
        }

        [UnityTest]
        public IEnumerator PracticeActionRequiresItsContextAndCompletesARealGestureWithCooldown()
        {
            yield return BeginTemple();
            var motor = App.Player;
            yield return Grounded(motor);
            Assert.That(motor.Actions,Is.Not.Null);
            Assert.That(motor.Actions.IsInTrainingArea,Is.False);
            yield return Press(Key.F);
            Assert.That(motor.Actions.State,Is.EqualTo(GospelActionState.Ready),"Story exploration must not act as a combat arena.");
            Assert.That(motor.TryAction(),Is.False);
            int objective = App.Journey.ObjectiveIndex, readings = App.Journey.CollectedReadings.Count;
            motor.SetPose(new Vector3(-8,.08f,-8),0);
            yield return Grounded(motor);
            Assert.That(motor.Actions.IsInTrainingArea,Is.True,"The marked optional practice area must configure the action context.");
            Assert.That(motor.CanPerformAction,Is.True);
            int completed = 0; motor.Actions.ActionCompleted += () => completed++;
            var arms = motor.GetComponentsInChildren<Transform>().Where(t => t.name == "upperarm_l" || t.name == "lowerarm_l" || t.name == "hand_l").ToArray();
            Assert.That(arms.Length,Is.EqualTo(3));
            var initial = arms.Select(t => t.localRotation).ToArray(); var largest = new float[arms.Length];
            yield return Press(Key.F);
            Assert.That(motor.Actions.IsActive,Is.True,"Real F input must start the optional practice action.");
            Assert.That(motor.TryAction(),Is.False,"Repeated input must respect the action state.");
            float deadline = Time.realtimeSinceStartup + 8;
            while (motor.Actions.IsActive && Time.realtimeSinceStartup < deadline)
            {
                for (int i = 0; i < arms.Length; i++) largest[i] = Mathf.Max(largest[i],Quaternion.Angle(initial[i],arms[i].localRotation));
                yield return null;
            }
            Assert.That(largest.Count(angle => angle > 5),Is.GreaterThanOrEqualTo(2),"The action must move actual arm joints through its authored clip.");
            Assert.That(completed,Is.EqualTo(1));
            Assert.That(motor.Actions.State,Is.EqualTo(GospelActionState.Cooldown));
            Assert.That(motor.TryAction(),Is.False,"The completed action must have a cooldown.");
            yield return SmokeUi.WaitUntil(() => motor.CanPerformAction,5,"The practice action must recover for another input.");
            yield return SmokeUi.Capture("PathOfDawn","gospel-remaster-practice-area",1280,720,settleSeconds:.05f);
            Assert.That(App.Journey.Phase,Is.EqualTo(GospelPhase.Exploring));
            Assert.That(App.Journey.ObjectiveIndex,Is.EqualTo(objective));
            Assert.That(App.Journey.CollectedReadings.Count,Is.EqualTo(readings),"Optional practice must not rewrite Scripture progress.");
            motor.SetPose(new Vector3(0,.08f,-9),0); yield return Grounded(motor);
            Assert.That(motor.CanPerformAction,Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MenusBlockMovementJumpAndActionAndPointerUiClicksDoNotPractice()
        {
            yield return BeginTemple();
            var cameraPad = UnityEngine.Object.FindObjectsByType<DawnPointerPad>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Single(pad => pad.name == "Camera drag");
            var touchControls = cameraPad.transform.parent.gameObject;
            bool touchWasActive = touchControls.activeSelf;
            try
            {
                // Inspect the real touch-layer geometry synchronously, before
                // the desktop app hides that layer on its next Update.
                touchControls.SetActive(true); Canvas.ForceUpdateCanvases();
                var practiceButton = SmokeUi.FindButton("동작 연습 · F");
                Assert.That(practiceButton,Is.Not.Null);
                var buttonRect = practiceButton.GetComponent<RectTransform>();
                var position = RectTransformUtility.WorldToScreenPoint(null,buttonRect.TransformPoint(buttonRect.rect.center));
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position },hits);
                Assert.That(hits.Count,Is.GreaterThan(0),"The actual action button must be reachable by the UI raycaster.");
                Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(practiceButton),
                    "The transparent camera pad must not cover the visible practice action button on touch screens.");
            }
            finally { touchControls.SetActive(touchWasActive); }
            var motor = App.Player; motor.SetPose(new Vector3(-8,.08f,-8),0);
            yield return Grounded(motor);
            yield return Press(Key.Escape);
            Assert.That(App.IsModal,Is.True); Assert.That(motor.InputEnabled,Is.False);
            var stopped = motor.transform.position;
            motor.TouchMove = Vector2.up; motor.TouchRun = true; motor.JumpPressed();
            Assert.That(motor.TryAction(),Is.False);
            yield return Hold(new[] { Key.W,Key.LeftShift,Key.Space,Key.F },.4f);
            Assert.That(PlanarDistance(stopped,motor.transform.position),Is.LessThan(.025f));
            Assert.That(Mathf.Abs(stopped.y - motor.transform.position.y),Is.LessThan(.08f));
            Assert.That(motor.Actions.IsActive,Is.False);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); motor.TouchMove = Vector2.zero; motor.TouchRun = false;
            SmokeUi.Click("돌아가기"); yield return null; yield return null;
            Assert.That(App.IsModal,Is.False); Assert.That(motor.InputEnabled,Is.True);
            yield return Grounded(motor);
            var pointer = InputSystem.AddDevice<Mouse>();
            try
            {
                var saveButton = SmokeUi.FindButton("저장"); Assert.That(saveButton,Is.Not.Null);
                Canvas.ForceUpdateCanvases();
                var rect = saveButton.GetComponent<RectTransform>();
                Vector2 pointerPosition = RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                InputSystem.QueueStateEvent(pointer,new MouseState { position = pointerPosition }); yield return null; yield return null;
                Assert.That(EventSystem.current.IsPointerOverGameObject(),Is.True,"The real pointer must be over a UI button for this guard check.");
                InputSystem.QueueStateEvent(pointer,new MouseState { position = pointerPosition,buttons = 1 }); yield return null; yield return null;
                Assert.That(motor.Actions.IsActive,Is.False,"Clicking the save UI must not start the left-mouse practice action.");
                InputSystem.QueueStateEvent(pointer,new MouseState { position = pointerPosition }); yield return null;
                yield return Press(Key.F);
                Assert.That(motor.Actions.IsActive,Is.True,"The explicit practice key remains available after a UI click.");
            }
            finally { InputSystem.RemoveDevice(pointer); }
            LogAssert.NoUnexpectedReceived();
        }

        IEnumerator ConsumeCurrentDialogue()
        {
            string reading = App.Journey.CurrentReading.Id;
            int turns = App.Journey.DialogueCount, collected = App.Journey.CollectedReadings.Count;
            Assert.That(turns,Is.GreaterThanOrEqualTo(1),"Every Scripture scene must have a readable turn.");
            if (App.Journey.Phase == GospelPhase.Reading)
                Assert.That(turns,Is.GreaterThan(1),"The temple conversations must offer several readable turns.");
            for (int turn = 0; turn < turns; turn++)
            {
                Assert.That(App.Journey.DialogueIndex,Is.EqualTo(turn));
                Assert.That(SmokeUi.HasText(App.Journey.CurrentDialogue.Speaker),Is.True,"The current speaker must be visible.");
                Assert.That(SmokeUi.HasText(App.Journey.CurrentDialogue.Text),Is.True,"The current dialogue text must be visible.");
                string button = turn + 1 < turns ? "다음 대화" : App.Journey.Phase == GospelPhase.Prologue
                    ? (App.Journey.ProloguePage + 1 < GospelCatalog.PrologueReadings.Count ? "다음 구절" : "성전으로") : "말씀을 따라 걷기";
                SmokeUi.Click(button); yield return null;
                if (turn + 1 < turns)
                {
                    Assert.That(App.Journey.CurrentReading.Id,Is.EqualTo(reading));
                    Assert.That(App.Journey.CollectedReadings.Count,Is.EqualTo(collected),"One dialogue click must not finish the whole Scripture passage.");
                }
            }
        }

        static IEnumerator Grounded(GospelThirdPersonMotor motor)
        {
            yield return SmokeUi.WaitUntil(() => motor.IsGrounded,8,"The real capsule must settle on the courtyard before this check.");
            yield return null;
        }

        static float PlanarDistance(Vector3 first,Vector3 second) => Vector2.Distance(new Vector2(first.x,first.z),new Vector2(second.x,second.z));

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
