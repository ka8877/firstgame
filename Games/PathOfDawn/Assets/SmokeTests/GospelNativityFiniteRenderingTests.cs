using System;
using System.Collections;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Real HDR renderer regression at fixed diagnostic poses, not gameplay navigation proof.</summary>
    public sealed class GospelNativityFiniteRenderingTests
    {
        IDisposable storage;
        MemorySaveBackend backend;
        GospelPilgrimageApp app;
        GameObject encounter;
        bool hadSlot;
        string originalSlot, attempt;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            backend = new MemorySaveBackend(); backend.WriteAtomic("preserved-finite-render", "original source bytes");
            storage = PersistentSaves.UseBackendForTesting(backend);
            hadSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot"); originalSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            yield return SmokeUi.LoadBoot();
            app = UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageApp>(); Assert.That(app, Is.Not.Null);
            app.enabled = false; app.Player.Enabled = false; app.Player.enabled = false; app.CameraRig.InputEnabled = false;
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (encounter != null) UnityEngine.Object.Destroy(encounter);
            yield return SmokeUi.UnloadBoot(); storage?.Dispose();
            if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", originalSlot); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        void Pose(string chapter, float x, float z, string questId)
        {
            app.World.SetChapter(chapter); app.Player.SetBounds(app.World.Bounds);
            app.Player.SetPose(new Vector3(x, app.World.HeightAt(x,z), z), 180);
            app.Actor.SetAdult(chapter == "baptism"); app.Actor.SetLamp(false);
            app.Actor.SetMotion(Vector3.zero, true, 180); app.CameraRig.SetZoom(6.4f); app.CameraRig.Snap();
            if (encounter != null) { encounter.SetActive(false); UnityEngine.Object.Destroy(encounter); }
            var quest = GospelChronicleCatalog.Quests.First(q => q.Id == questId);
            encounter = new GameObject("Finite render regression · " + questId);
            encounter.transform.position = app.World.PositionForQuest(quest);
            var actor = GospelPilgrimageActor.BuildNpc(encounter.transform, app.SceneCamera, quest.NpcId, quest.Portrait == "mary" ? 1.63f : 1.78f);
            actor.SetGroundSampler(app.World.HeightAt); app.World.SetObjective(encounter.transform.position, true);
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator CedarSourceAndRawHdrRemainFiniteBeforeThePostGuardAtMaryAndJordan()
        {
            var source = Resources.Load<Material>("Art/GospelWell/Materials/Well_Cedar"); Assert.That(source, Is.Not.Null);
            Assert.That(source.IsKeywordEnabled("_NORMALMAP"), Is.True, "The preserved source material must remain intact.");
            Assert.That(source.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            var well = app.World.GetComponentsInChildren<GospelAuthoredPropRoot>(true).Single(p => p.Kind == "well");
            var runtimeCedar = well.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .First(m => m != null && m.name == "Well_Cedar");
            Assert.That(runtimeCedar, Is.Not.SameAs(source));
            Assert.That(runtimeCedar.IsKeywordEnabled("_NORMALMAP"), Is.False);
            Assert.That(runtimeCedar.shader.name, Is.EqualTo("FirstGame/Pilgrimage/FiniteCedar"));
            Assert.That(runtimeCedar.GetTexture("_BaseMap"), Is.SameAs(source.GetTexture("_BaseMap")));
            Assert.That(source.GetTexture("_BumpMap"), Is.SameAs(Resources.Load<Texture2D>("Art/GospelWell/cedar-normal")), "The original normal-map resource remains intact.");
            Assert.That(runtimeCedar.GetTextureScale("_BaseMap"), Is.EqualTo(source.GetTextureScale("_BaseMap")));
            Assert.That(runtimeCedar.GetTextureOffset("_BaseMap"), Is.EqualTo(source.GetTextureOffset("_BaseMap")));
            Assert.That(runtimeCedar.GetColor("_BaseColor"), Is.EqualTo(source.GetColor("_BaseColor")));
            foreach (var renderer in well.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.sharedMaterials.Contains(runtimeCedar)) continue;
                var filter = renderer.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh;
                Debug.Log("GOSPEL_RESTAGE_CEDAR_MESH name=" + mesh.name + " vertices=" + mesh.vertexCount
                    + " readable=" + mesh.isReadable + " normalAttribute=" + mesh.HasVertexAttribute(VertexAttribute.Normal)
                    + " tangentAttribute=" + mesh.HasVertexAttribute(VertexAttribute.Tangent));
            }
            var cameraData = app.SceneCamera.GetUniversalAdditionalCameraData(); Assert.That(cameraData.stopNaN, Is.True);
            foreach (string name in new[] { "mary-well", "jordan-water" })
            {
                if (name == "mary-well") Pose("temple", -15, -8, "pilgrim-spring");
                else Pose("baptism", -1, 4, "baptism-request");
                yield return new WaitForSecondsRealtime(.25f);
                AssertRawHdrFinite(name);
                yield return SmokeUi.Capture("PathOfDawn", "nativity-" + attempt + "-finite-" + name, 1280, 720, settleSeconds:.15f);
                yield return SmokeUi.Capture("PathOfDawn", "nativity-" + attempt + "-finite-" + name + "-mobile", 844, 390, settleSeconds:.15f);
            }
            Assert.That(source.IsKeywordEnabled("_NORMALMAP"), Is.True);
            Assert.That(backend.Read("preserved-finite-render"), Is.EqualTo("original source bytes"));
        }

        void AssertRawHdrFinite(string location)
        {
            Assert.That(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf), Is.True);
            Assert.That(SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat), Is.True);
            var camera = app.SceneCamera; var data = camera.GetUniversalAdditionalCameraData();
            bool post = data.renderPostProcessing, stop = data.stopNaN;
            var previous = camera.targetTexture; var active = RenderTexture.active; float aspect = camera.aspect;
            var target = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Texture2D pixels = null;
            try
            {
                // Prove the material fix itself: neither postprocessing nor
                // the defensive StopNaN pass may hide invalid source pixels.
                data.renderPostProcessing = false; data.stopNaN = false;
                target.Create(); camera.targetTexture = target; camera.aspect = 640f / 360f;
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request); RenderTexture.active = target;
                pixels = new Texture2D(640, 360, TextureFormat.RGBAFloat, false, true);
                pixels.ReadPixels(new Rect(0,0,640,360),0,0); pixels.Apply();
                int invalid = 0; float peak = 0;
                foreach (var c in pixels.GetPixels())
                {
                    if (Bad(c.r) || Bad(c.g) || Bad(c.b)) invalid++;
                    else peak = Mathf.Max(peak, Mathf.Max(c.r, Mathf.Max(c.g,c.b)));
                }
                Debug.Log("GOSPEL_RESTAGE_FINITE_HDR fixture=fixed-render-pose-not-navigation location=" + location
                    + " rawPost=false rawStopNaN=false pixels=230400 invalid=" + invalid + " peak=" + peak.ToString("F6")
                    + " position=" + app.Player.Position + " graphics=" + SystemInfo.graphicsDeviceName);
                Assert.That(invalid, Is.Zero, "Source HDR pixels must be finite before any postprocessing guard at " + location);
                Assert.That(peak, Is.LessThan(8), "The quiet scene must not contain an extreme HDR firefly.");
            }
            finally
            {
                data.renderPostProcessing = post; data.stopNaN = stop;
                camera.targetTexture = previous; camera.aspect = aspect; RenderTexture.active = active;
                if (pixels != null) UnityEngine.Object.Destroy(pixels); target.Release(); UnityEngine.Object.Destroy(target);
            }
        }
        static bool Bad(float value) => float.IsNaN(value) || float.IsInfinity(value);
    }
}
