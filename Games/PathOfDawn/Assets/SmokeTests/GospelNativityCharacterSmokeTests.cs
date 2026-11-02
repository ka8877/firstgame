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
    public sealed class GospelNativityCharacterSmokeTests
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
        public IEnumerator ImportedWholeBodyCyclesBothStepsFacesFourWaysAndSettlesAtActualFeet()
        {
            Assert.That(actor.UsesExpandedAnimation, Is.True, "The new release must use its reviewed original Canopy atlas.");
            Assert.That(actor.UsesMeasuredLayout, Is.True);
            Assert.That(actor.FramesPerDirectionCount, Is.EqualTo(8));
            Assert.That(actor.AnimationFrameCount, Is.EqualTo(32));
            Assert.That(actor.WalkPoseCount, Is.EqualTo(6));
            Assert.That(actor.CurrentSprite.bounds.size.y, Is.InRange(1.54f, 1.76f), "Child standing scale is 1.64 world metres, with measured source padding.");
            Vector3 feet = root.transform.position;
            var poses = new HashSet<int>();
            actor.SetMotion(Vector3.right * 3.25f, true, 90);
            yield return null;
            yield return null;
            float until = Time.realtimeSinceStartup + 1.35f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                poses.Add(actor.CurrentFrame);
                Assert.That(actor.CurrentFacing, Is.EqualTo("Right"));
                Assert.That(actor.CurrentState, Is.EqualTo("Walk"));
                Assert.That(root.transform.position, Is.EqualTo(feet));
                Assert.That(actor.Renderer.transform.position, Is.EqualTo(feet));
            }
            Assert.That(poses.SetEquals(new[] { 1, 2, 3, 4, 5, 6 }), Is.True,
                "A full gait must use both contact/down/passing sequences, without inserting the idle pose.");
            Vector3[] velocities = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };
            string[] facings = { "Front", "Back", "Left", "Right" };
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
            Assert.That(actor.CurrentFrame, Is.EqualTo(7));
            actor.SetMotion(new Vector3(.5f, -3, 0), false, 90);
            yield return null;
            yield return null;
            Assert.That(actor.CurrentState, Is.EqualTo("Fall"));
            Assert.That(actor.CurrentFrame, Is.EqualTo(7));
            actor.SetMotion(Vector3.zero, true, 90);
            yield return new WaitForSecondsRealtime(.14f);
            Assert.That(actor.CurrentState, Is.EqualTo("Idle"));
            Assert.That(actor.CurrentFrame, Is.Zero);
            Assert.That(actor.Renderer.transform.position, Is.EqualTo(feet));
            actor.SetAdult(true);
            yield return null;
            Assert.That(actor.IsAdult, Is.True);
            Assert.That(actor.UsesExpandedAnimation, Is.True);
            Assert.That(actor.CurrentSprite.bounds.size.y, Is.InRange(1.82f, 2.08f), "Adult scale must remain 1.94 world metres.");
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
