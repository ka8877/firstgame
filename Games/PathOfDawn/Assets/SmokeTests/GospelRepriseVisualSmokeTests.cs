using System.Collections;
using NUnit.Framework;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    public sealed class GospelRepriseVisualSmokeTests
    {
        Keyboard keyboard;
        InputSettings previousInputs, testInputs;
        float previousCaptureDelta;
        bool clockCaptured;
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null && keyboard.added)
            { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.RemoveDevice(keyboard); }
            if (previousInputs != null) InputSystem.settings = previousInputs;
            if (clockCaptured) Time.captureDeltaTime = previousCaptureDelta;
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (testInputs != null && InputSystem.settings != testInputs) Object.DestroyImmediate(testInputs);
        }
        [UnityTest]
        public IEnumerator RealExplorationDashStopsAtCollisionAndCannotBypassCooldownOrModal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            previousCaptureDelta = Time.captureDeltaTime; clockCaptured = true;
            // Match the shipping motor's ordinary dynamic-update input context,
            // while isolating the fixture from editor focus and old held keys.
            previousInputs = InputSystem.settings; testInputs = Object.Instantiate(previousInputs);
            testInputs.hideFlags = HideFlags.HideAndDontSave;
            testInputs.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputs.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testInputs; keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            var floor = new GameObject("Dash test solid ground");
            var floorCollider = floor.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(16, 1, 16); floor.transform.position = Vector3.down * .5f;
            var wall = new GameObject("Dash test blocking wall");
            wall.transform.position = new Vector3(.85f, 1, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(.2f, 2, 4);
            var cameraObject = new GameObject("Dash test camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.transform.rotation = Quaternion.Euler(43, 0, 0);
            var player = new GameObject("Real dash motor and character controller");
            var motor = player.AddComponent<GospelPilgrimageMotor>(); motor.Initialize(camera);
            motor.SetBounds(new Rect(-7, -7, 14, 14)); motor.SetPose(Vector3.zero, 90);
            Physics.SyncTransforms();
            float groundDeadline = Time.realtimeSinceStartup + 2;
            while (!motor.IsGrounded && Time.realtimeSinceStartup < groundDeadline) yield return null;
            Assert.That(motor.IsGrounded, Is.True,
                "The actual controller must settle on the solid fixture floor; position=" + player.transform.position
                + " controllerGrounded=" + motor.Controller.isGrounded + " enabled=" + motor.Enabled
                + " dt=" + Time.deltaTime + " timeScale=" + Time.timeScale);
            Vector3 initial = player.transform.position;
            int firstSequence = motor.DashSequence;
            motor.RequestDash(); Assert.That(motor.IsDashing, Is.True);
            Assert.That(motor.DashReady, Is.False, "A queued external UI request must already block another dash.");
            float dashDeadline = Time.realtimeSinceStartup + 2;
            while (motor.IsDashing && Time.realtimeSinceStartup < dashDeadline) yield return null;
            Assert.That(motor.IsDashing, Is.False, "The queued request must finish actual motor movement.");
            Assert.That(motor.DashSequence, Is.EqualTo(firstSequence + 1), "The external request starts exactly once in the motor.");
            Assert.That(player.transform.position.x, Is.GreaterThan(initial.x + .15f));
            Assert.That(player.transform.position.x, Is.LessThan(.57f), "The actual CharacterController must not pass through the wall.");
            Assert.That(motor.DashReady, Is.False);
            motor.RequestDash(); Assert.That(motor.IsDashing, Is.False, "Repeated requests must not restart the dash before cooldown.");
            float readyDeadline = Time.realtimeSinceStartup + 2;
            while (!motor.DashReady && Time.realtimeSinceStartup < readyDeadline) yield return null;
            Assert.That(motor.DashReady, Is.True, "The cooldown must eventually finish after actual movement started.");
            motor.Enabled = false; Vector3 paused = player.transform.position;
            motor.RequestDash(); Assert.That(motor.IsDashing, Is.False);
            yield return new WaitForSeconds(.12f);
            Assert.That(Vector3.Distance(paused, player.transform.position), Is.LessThan(.05f));
            motor.Enabled = true; yield return null;
            Assert.That(motor.DashReady, Is.True);
            // A real Unity frame longer than the 0.19-second dash must neither
            // erase an EventSystem-style request nor apply dash speed for all
            // 0.25 seconds. Restore the original clock in TearDown.
            Time.captureDeltaTime = .25f;
            motor.SetPose(new Vector3(-3, 0, 0), 270); yield return null;
            Vector3 openStart = player.transform.position;
            int openSequence = motor.DashSequence;
            motor.RequestDash(); Assert.That(motor.IsDashing, Is.True);
            dashDeadline = Time.realtimeSinceStartup + 2;
            while (motor.IsDashing && Time.realtimeSinceStartup < dashDeadline) yield return null;
            Assert.That(motor.IsDashing, Is.False);
            Assert.That(motor.DashSequence, Is.EqualTo(openSequence + 1));
            Assert.That(openStart.x - player.transform.position.x, Is.InRange(1.1f, 3.2f), "A 0.25-second frame must retain the queued dash and cap its actual movement interval.");
            Assert.That(motor.Bounds.Contains(new Vector2(player.transform.position.x, player.transform.position.z)), Is.True);
            Debug.Log("GOSPEL_REPRISE_DASH real-collider=true cooldown=true modal-stops=true queued-ui=true captureDeltaTime=0.25 open-distance=" + (openStart.x - player.transform.position.x).ToString("F3"));
            Object.Destroy(player); Object.Destroy(cameraObject); Object.Destroy(wall); Object.Destroy(floor);
            yield return null; yield return new ExitPlayMode();
        }
    }
}
