using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Real imported atlas, gait response and equipped lamp size; no story/save mutation.</summary>
    public sealed class GospelCanopyCharacterSmokeTests
    {
        GameObject root, cameraObject;
        GospelPilgrimageActor actor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            cameraObject = new GameObject("Canopy character diagnostic camera", typeof(Camera));
            cameraObject.transform.rotation = Quaternion.Euler(43, 0, 0);
            root = new GameObject("Canopy measured feet diagnostic");
            root.transform.position = new Vector3(2, 0, 3);
            // Deliberately no motor: isolate the public motion input from root
            // movement and verify that animation never translates the feet.
            actor = GospelPilgrimageActor.Build(root.transform, cameraObject.GetComponent<Camera>());
            actor.SetGroundSampler((x, z) => 0);
            actor.SetMotion(Vector3.zero, true, 180);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            if (cameraObject != null) Object.Destroy(cameraObject);
            yield return null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ImportedLayersCycleEightDirectionsAndSettleAtActualFeet()
        {
            Assert.That(actor.UsesExpandedAnimation, Is.True);
            Assert.That(actor.UsesLayeredMotion, Is.True, "Use original animated game assets, not a static picture with bobbing.");
            Assert.That(actor.UsesMeasuredLayout, Is.True);
            Assert.That(actor.MotionContractVersion, Is.EqualTo(2));
            Assert.That(actor.MotionDirectionCount, Is.EqualTo(8)); Assert.That(actor.MotionLayerCount, Is.EqualTo(1));
            Assert.That(actor.FramesPerDirectionCount, Is.EqualTo(20));
            Assert.That(actor.AnimationFrameCount, Is.EqualTo(160));
            Assert.That(actor.WalkPoseCount, Is.EqualTo(8));
            Assert.That(actor.RunPoseCount, Is.EqualTo(8));
            Assert.That(actor.VisualHeight, Is.EqualTo(1.2f).Within(.001f));
            Assert.That(actor.VisiblePlaneHeight, Is.InRange(1.14f, 1.27f), "Actual child geometry and measured alpha use their recorded metre scale once.");
            Assert.That(actor.WalkStrideMeters, Is.InRange(.65f, 1.5f));
            Assert.That(actor.RunStrideMeters, Is.GreaterThan(actor.WalkStrideMeters));
            Vector3 feet = root.transform.position;
            var poses = new HashSet<int>();
            actor.SetMotion(Vector3.right * 1.65f, true, 90);
            yield return null;
            yield return null;
            float until = Time.realtimeSinceStartup + 1.8f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                poses.Add(actor.CurrentFrame);
                Assert.That(actor.CurrentFacing, Is.EqualTo("Right"));
                Assert.That(actor.CurrentState, Is.EqualTo("Walk"));
                Assert.That(root.transform.position, Is.EqualTo(feet));
                Assert.That(actor.VisualFootOrigin.position, Is.EqualTo(feet));
                foreach (var layer in actor.MotionRenderers)
                {
                    Assert.That(layer.sprite, Is.Not.Null);
                    Assert.That(float.IsNaN(layer.transform.position.y) || float.IsInfinity(layer.transform.position.y), Is.False);
                }
            }
            Assert.That(poses.SetEquals(new[] { 4, 5, 6, 7, 8, 9, 10, 11 }), Is.True,
                "All eight neutral walk drawings must play, without inserting stance or run drawings.");
            poses.Clear();
            actor.SetMotion(Vector3.right * 5.4f, true, 90);
            until = Time.realtimeSinceStartup + 1.8f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                Assert.That(actor.CurrentState, Is.EqualTo("Run"));
                Assert.That(actor.CurrentFrame, Is.InRange(12, 19));
                Assert.That(root.transform.position, Is.EqualTo(feet));
                Assert.That(actor.VisualFootOrigin.position, Is.EqualTo(feet));
                poses.Add(actor.CurrentFrame);
            }
            Assert.That(poses.SetEquals(new[] {12,13,14,15,16,17,18,19}), Is.True,
                "Running must use all eight separate longer-stride drawings.");
            Vector3[] velocities = { Vector3.back, new Vector3(-1,0,-1), Vector3.left, new Vector3(-1,0,1),
                Vector3.forward, new Vector3(1,0,1), Vector3.right, new Vector3(1,0,-1) };
            string[] facings = { "Front", "FrontLeft", "Left", "BackLeft", "Back", "BackRight", "Right", "FrontRight" };
            for (int i = 0; i < velocities.Length; i++)
            {
                actor.SetMotion(velocities[i] * 3.25f, true, i * 90);
                yield return null;
                yield return null;
                Assert.That(actor.CurrentFacing, Is.EqualTo(facings[i]));
            }
            actor.SetMotion(new Vector3(.5f, 3, 0), false, 90);
            yield return null;
            yield return null;
            Assert.That(actor.CurrentState, Is.EqualTo("Jump"));
            Assert.That(actor.CurrentFrame, Is.EqualTo(6), "An existing source stride drawing represents flight; this is not a claimed new jump take.");
            actor.SetMotion(new Vector3(.5f, -3, 0), false, 90);
            yield return null;
            yield return null;
            Assert.That(actor.CurrentState, Is.EqualTo("Fall"));
            Assert.That(actor.CurrentFrame, Is.EqualTo(10));
            actor.SetMotion(Vector3.zero, true, 90);
            yield return new WaitForSecondsRealtime(.14f);
            Assert.That(actor.CurrentState, Is.EqualTo("Idle"));
            Assert.That(actor.CurrentFrame, Is.InRange(0, 3));
            Assert.That(actor.VisualFootOrigin.position, Is.EqualTo(feet));
            actor.SetAdult(true);
            yield return null;
            Assert.That(actor.IsAdult, Is.True);
            Assert.That(actor.UsesExpandedAnimation, Is.True);
            Assert.That(actor.VisualHeight, Is.EqualTo(1.85f).Within(.001f));
            Assert.That(actor.OriginalAtlasResource, Is.EqualTo("Art/GospelMotion/Neutral/adult"));
            Assert.That(actor.VisiblePlaneHeight, Is.InRange(1.75f, 1.97f), "Adult scale comes from the complete-body alpha bounds and recorded physical height.");
            actor.SetLamp(true);
            yield return null;
            yield return null;
            Assert.That(actor.LampEquipped, Is.True);
            var hook = actor.transform.Find("Equipped travel lamp · preserved authored mesh and light");
            Assert.That(hook, Is.Not.Null);
            var meshes = hook.GetComponentsInChildren<MeshRenderer>();
            Assert.That(meshes.Length, Is.GreaterThan(0));
            Bounds lampBounds = meshes[0].bounds;
            foreach (var mesh in meshes) lampBounds.Encapsulate(mesh.bounds);
            Assert.That(Mathf.Max(lampBounds.size.x, Mathf.Max(lampBounds.size.y, lampBounds.size.z)),
                Is.EqualTo(.22f).Within(.004f), "The equipped authored lamp must not grow to cover the body.");
            actor.SetLamp(false);
            Assert.That(actor.LampEquipped, Is.False);
        }
    }
}
