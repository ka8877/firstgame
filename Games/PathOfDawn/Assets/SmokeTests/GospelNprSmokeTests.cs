using System;
using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PathOfDawn.SmokeTests
{
    /// <summary>
    /// Actual NPR geometry, skeletal animation, camera, live people and retained
    /// story/save behaviour. These tests do not grant art or user approval.
    /// </summary>
    public sealed class GospelNprSmokeTests
    {
        const string PreservedSlot = "npr-untouched-player-original";
        IDisposable storage;
        MemorySaveBackend backend;
        Keyboard keyboard;
        Touchscreen touchscreen;
        InputSettings previousInputs, testInputs;
        GameObject fixture;
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
            if (fixture != null) UnityEngine.Object.Destroy(fixture);
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
        IEnumerator Shot(string name) => SmokeUi.Capture("PathOfDawn", "npr-" + attempt + "-" + name, 1280, 720, settleSeconds: .15f);
        static float Planar(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static Vector3 Flat(Vector3 value) { value.y = 0; return value.normalized; }
        string VisibleText() => string.Join("\n", App.Ui.CanvasView.GetComponentsInChildren<TMP_Text>()
            .Where(text => text.gameObject.activeInHierarchy).Select(text => text.text));

        GospelPilgrimageActor AddActor(string model, int index = 0)
        {
            if (fixture == null)
            {
                fixture = new GameObject("NPR real model-only inspection fixture");
                fixture.transform.position = App.Player.Position;
            }
            var foot = new GameObject(model + " physical owner").transform;
            foot.SetParent(fixture.transform, false);
            foot.localPosition = new Vector3((index - 2) * 1.18f, 0, 0);
            foot.position = new Vector3(foot.position.x, App.World.HeightAt(foot.position.x, foot.position.z) + .035f, foot.position.z);
            GospelPilgrimageActor actor;
            if (model == "JesusChild" || model == "JesusAdult") actor = GospelPilgrimageActor.Build(foot, App.SceneCamera, model == "JesusAdult");
            else actor = GospelPilgrimageActor.BuildNpc(foot, App.SceneCamera, model == "Mary" ? "mary" : model == "Joseph" ? "joseph" : "temple-teacher");
            actor.SetGroundSampler(App.World.HeightAt); actor.SetMotion(Vector3.zero, true, 180);
            return actor;
        }

        static void AssertHumanoid(GospelPilgrimageActor actor, string model)
        {
            Assert.That(actor.HasSkinnedModel, Is.True, model);
            Assert.That(actor.ModelResource, Is.EqualTo("Art/GospelNpr/Characters/" + model));
            Assert.That(actor.Renderer, Is.Null, "A new 3D pass cannot be satisfied by an old directional sprite.");
            Assert.That(actor.Animator, Is.Not.Null); Assert.That(actor.Animator.avatar, Is.Not.Null);
            Assert.That(actor.Animator.avatar.isHuman && actor.Animator.avatar.isValid, Is.True, model);
            Assert.That(actor.Animator.applyRootMotion, Is.False, "The motor/people own physical travel.");
            foreach (var bone in new[] { HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftHand,
                HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                Assert.That(actor.Animator.GetBoneTransform(bone), Is.Not.Null, model + ": " + bone);
            Assert.That(actor.Bones.Count, Is.GreaterThanOrEqualTo(40));
            Assert.That(actor.SkinnedRenderers.Count, Is.GreaterThan(0));
            int vertices = 0, textured = 0;
            foreach (var renderer in actor.SkinnedRenderers)
            {
                Assert.That(renderer.sharedMesh, Is.Not.Null); vertices += renderer.sharedMesh.vertexCount;
                Assert.That(renderer.bones.Length, Is.GreaterThan(0));
                var weights = renderer.sharedMesh.boneWeights;
                Assert.That(weights.Length, Is.EqualTo(renderer.sharedMesh.vertexCount));
                for (int i = 0; i < weights.Length; i += Mathf.Max(1, weights.Length / 40))
                    Assert.That(weights[i].weight0 + weights[i].weight1 + weights[i].weight2 + weights[i].weight3,
                        Is.EqualTo(1).Within(.025f), "A real skinned vertex must retain normalized bone weights.");
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null); Assert.That(material.shader, Is.Not.Null);
                    Assert.That(material.shader.isSupported, Is.True, material.name);
                    Assert.That(material.shader.name.IndexOf("lilToon", StringComparison.OrdinalIgnoreCase), Is.GreaterThanOrEqualTo(0), material.name);
                    if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null) textured++;
                }
            }
            Assert.That(vertices, Is.GreaterThan(7000), "Use the authored anatomical mesh, not a few proxy shapes.");
            Assert.That(textured, Is.GreaterThan(2), "Retain authored face/eye/hair detail.");
            var bounds = actor.VisualBounds;
            Assert.That(Finite(bounds.center.x) && Finite(bounds.center.y) && Finite(bounds.center.z), Is.True);
            Assert.That(bounds.size.y, Is.EqualTo(GospelNprCharacterView.HeightFor(model)).Within(.24f));
            Assert.That(bounds.size.x, Is.GreaterThan(.25f)); Assert.That(bounds.size.z, Is.GreaterThan(.10f));
            Assert.That(Mathf.Abs(bounds.min.y - actor.VisualFootOrigin.position.y), Is.LessThan(.22f), "The authored figure must meet its physical feet.");
            Assert.That(actor.Animator.runtimeAnimatorController.animationClips.Count(clip => clip != null && clip.humanMotion), Is.GreaterThanOrEqualTo(3));
        }

        [UnityTest]
        public IEnumerator FiveAuthoredHumanoidsKeepRealSkinsFacesAndControllerOwnedFeet()
        {
            App.Ui.CanvasView.gameObject.SetActive(false); App.Actor.gameObject.SetActive(false);
            string[] models = { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" };
            var actors = models.Select((model, i) => AddActor(model, i)).ToArray();
            yield return null; yield return null;
            for (int i = 0; i < actors.Length; i++) AssertHumanoid(actors[i], models[i]);
            var root = actors[0].VisualFootOrigin.parent;
            Vector3 position = root.position; int instance = root.GetInstanceID();
            actors[0].SetLamp(true); yield return null;
            AssertLamp(actors[0]);
            actors[0].SetAdult(true); yield return null; yield return null;
            Assert.That(root.GetInstanceID(), Is.EqualTo(instance)); Assert.That(Vector3.Distance(root.position, position), Is.LessThan(.00001f));
            AssertHumanoid(actors[0], "JesusAdult"); AssertLamp(actors[0]);
            App.CameraRig.SetTarget(fixture.transform); App.CameraRig.SetOrbit(0, 12); App.CameraRig.SetZoom(8.2f); App.CameraRig.Snap();
            yield return Shot("five-real-humanoids");
        }

        static void AssertLamp(GospelPilgrimageActor actor)
        {
            Assert.That(actor.LampEquipped, Is.True);
            var light = actor.GetComponentsInChildren<Light>().Single(item => item.type == LightType.Point);
            var carried = light.transform.parent;
            Assert.That(carried.IsChildOf(actor.CharacterView.RightHand), Is.True, "The lamp belongs to the actual hand bone.");
            var renderers = carried.GetComponentsInChildren<MeshRenderer>(); Assert.That(renderers.Length, Is.GreaterThan(0));
            Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float worldAabbLongest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            // Keep real world metres, but measure in the carried lamp's rotated
            // axes. InverseTransformPoint would incorrectly remove its scale.
            Quaternion inverseRotation = Quaternion.Inverse(carried.rotation);
            Vector3 origin = carried.position;
            Bounds orientedWorldBounds = default; int vertexCount = 0;
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                Assert.That(filter, Is.Not.Null, "Measure the actual rendered lamp mesh.");
                Assert.That(filter.sharedMesh, Is.Not.Null);
                // The preserved FBX is non-readable in player builds. This
                // Editor-only smoke takes a read-only snapshot without reimport.
                using (var data = UnityEditor.MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh))
                using (var vertices = new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount, Unity.Collections.Allocator.Temp))
                {
                    data[0].GetVertices(vertices);
                    Assert.That(vertices.Length, Is.GreaterThan(0));
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        Vector3 world = filter.transform.TransformPoint(vertices[index]);
                        Vector3 point = inverseRotation * (world - origin);
                        if (vertexCount++ == 0) orientedWorldBounds = new Bounds(point, Vector3.zero);
                        else orientedWorldBounds.Encapsulate(point);
                    }
                }
            }
            Vector3 size = orientedWorldBounds.size;
            Assert.That(Finite(size.x) && Finite(size.y) && Finite(size.z), Is.True);
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            Debug.Log("FIRSTGAME_NPR_LAMP_WORLD_METRES worldAabbSize=" + bounds.size.ToString("F6")
                + " worldAabbLongest=" + worldAabbLongest.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " orientedWorldSize=" + size.ToString("F6")
                + " orientedWorldLongest=" + longest.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " facingYaw=" + actor.FacingYaw.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " carriedWorldYaw=" + carried.eulerAngles.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " vertices=" + vertexCount);
            Assert.That(longest, Is.InRange(.18f, .25f));
            Assert.That(longest, Is.LessThan(actor.VisualHeight * .25f));
            Assert.That(Vector3.Dot(carried.up, Vector3.up), Is.GreaterThan(.995f));
        }

        static Vector3[] BakedVertices(SkinnedMeshRenderer renderer)
        {
            var mesh = new Mesh();
            try { renderer.BakeMesh(mesh); return mesh.vertices; }
            finally { UnityEngine.Object.Destroy(mesh); }
        }
        static float MeshDifference(Vector3[] before, Vector3[] after)
        {
            Assert.That(after.Length, Is.EqualTo(before.Length)); float greatest = 0;
            for (int i = 0; i < before.Length; i += Mathf.Max(1, before.Length / 400)) greatest = Mathf.Max(greatest, Vector3.Distance(before[i], after[i]));
            return greatest;
        }
        static Vector3 LocalFoot(GospelPilgrimageActor actor, HumanBodyBones foot) =>
            actor.CharacterView.VisualRoot.InverseTransformPoint(actor.Animator.GetBoneTransform(foot).position);

        [UnityTest]
        public IEnumerator WalkRunAndConversationAnimateRealJointsWithoutMovingPhysicalOwners()
        {
            var actor = AddActor("Joseph", 2); var owner = actor.VisualFootOrigin.parent;
            Vector3 physical = owner.position; int identity = owner.GetInstanceID();
            var left = LocalFoot(actor, HumanBodyBones.LeftFoot); var right = LocalFoot(actor, HumanBodyBones.RightFoot);
            var body = actor.SkinnedRenderers.OrderByDescending(item => item.sharedMesh.vertexCount).First();
            var restMesh = BakedVertices(body); float leftTravel = 0, rightTravel = 0, skinTravel = 0;
            actor.SetMotion(Vector3.right * 1.2f, true, 90);
            var walkClips = new HashSet<string>(); float end = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                leftTravel = Mathf.Max(leftTravel, Vector3.Distance(left, LocalFoot(actor, HumanBodyBones.LeftFoot)));
                rightTravel = Mathf.Max(rightTravel, Vector3.Distance(right, LocalFoot(actor, HumanBodyBones.RightFoot)));
                foreach (var clip in actor.Animator.GetCurrentAnimatorClipInfo(0).Where(value => value.weight > .05f)) walkClips.Add(clip.clip.name);
                skinTravel = Mathf.Max(skinTravel, MeshDifference(restMesh, BakedVertices(body)));
                Assert.That(Vector3.Distance(owner.position, physical), Is.LessThan(.00001f));
            }
            Assert.That(leftTravel, Is.GreaterThan(.025f)); Assert.That(rightTravel, Is.GreaterThan(.025f));
            Assert.That(skinTravel, Is.GreaterThan(.003f), "Bone motion must deform the real skinned mesh.");
            Assert.That(actor.CurrentState, Is.EqualTo("Walk"));
            actor.SetMotion(Vector3.right * 5.4f, true, 90);
            var runClips = new HashSet<string>(); end = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                foreach (var clip in actor.Animator.GetCurrentAnimatorClipInfo(0).Where(value => value.weight > .05f)) runClips.Add(clip.clip.name);
            }
            Assert.That(actor.CurrentState, Is.EqualTo("Run")); Assert.That(walkClips.Count, Is.GreaterThan(0));
            Assert.That(runClips.Count, Is.GreaterThan(0)); Assert.That(runClips.SetEquals(walkClips), Is.False, "A separate authored gait must replace the walking blend.");
            actor.FaceConversationPartner(physical + Vector3.back * 2); yield return null;
            Assert.That(actor.CurrentState, Is.EqualTo("Idle")); Assert.That(actor.Animator.GetFloat("MoveSpeed"), Is.LessThan(.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(actor.FacingYaw, 180)), Is.LessThan(.01f));
            Assert.That(owner.GetInstanceID(), Is.EqualTo(identity)); Assert.That(Vector3.Distance(owner.position, physical), Is.LessThan(.00001f));
            App.Ui.CanvasView.gameObject.SetActive(false); App.Actor.gameObject.SetActive(false);
            App.CameraRig.SetTarget(owner); App.CameraRig.SetOrbit(0, 12); App.CameraRig.SetZoom(4); App.CameraRig.Snap();
            yield return Shot("actual-joint-conversation-stance");
        }

        static Dictionary<string, float> ShapeWeights(GospelPilgrimageActor actor)
        {
            var result = new Dictionary<string, float>();
            foreach (var renderer in actor.SkinnedRenderers)
                for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    result.Add(renderer.GetInstanceID() + ":" + i, renderer.GetBlendShapeWeight(i));
            return result;
        }
        static float ShapeDifference(Dictionary<string, float> before, Dictionary<string, float> after) =>
            after.Count == 0 ? 0 : after.Max(pair => Mathf.Abs(pair.Value - before[pair.Key]));
        static bool ChangedMorphHasAuthoredVertexDeltas(GospelPilgrimageActor actor, Dictionary<string, float> before)
        {
            foreach (var renderer in actor.SkinnedRenderers)
            {
                var mesh = renderer.sharedMesh;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    if (Mathf.Abs(renderer.GetBlendShapeWeight(i) - before[renderer.GetInstanceID() + ":" + i]) <= 3) continue;
                    int frames = mesh.GetBlendShapeFrameCount(i); if (frames == 0) continue;
                    var deltas = new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(i, frames - 1, deltas, null, null);
                    if (deltas.Any(value => value.sqrMagnitude > .0000000001f)) return true;
                }
            }
            return false;
        }
        static void AssertPortraitPixels(RenderTexture texture)
        {
            Assert.That(texture.IsCreated(), Is.True); Assert.That(texture.width, Is.GreaterThanOrEqualTo(384));
            var previous = RenderTexture.active;
            var resolved = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, resolved); RenderTexture.active = resolved;
                image.ReadPixels(new Rect(0, 0, resolved.width, resolved.height), 0, 0); image.Apply();
                var pixels = image.GetPixels32(); var buckets = new HashSet<int>();
                float low = 1, high = 0; double sum = 0; int count = 0;
                for (int i = 0; i < pixels.Length; i += 7)
                {
                    var color = pixels[i]; float light = (color.r * .2126f + color.g * .7152f + color.b * .0722f) / 255f;
                    low = Mathf.Min(low, light); high = Mathf.Max(high, light); sum += light; count++;
                    buckets.Add((color.r / 24) * 144 + (color.g / 24) * 12 + color.b / 24);
                }
                Assert.That(sum / count, Is.GreaterThan(.035)); Assert.That(high - low, Is.GreaterThan(.10f));
                Assert.That(buckets.Count, Is.GreaterThan(8), "An empty or uniform portrait target is not a rendered character.");
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(resolved); UnityEngine.Object.Destroy(image); }
        }

        [UnityTest]
        public IEnumerator FaceExpressionsAndSameModelPortraitsRenderAndRelease()
        {
            var actor = AddActor("JesusChild", 2); yield return null;
            Assert.That(actor.CharacterView.BlinkShapeCount, Is.GreaterThan(0)); Assert.That(actor.CharacterView.MouthShapeCount, Is.GreaterThan(0));
            var neutral = ShapeWeights(actor); Assert.That(neutral.Count, Is.GreaterThan(10));
            actor.SetSpeaking(true); yield return null; yield return null;
            Assert.That(ShapeDifference(neutral, ShapeWeights(actor)), Is.GreaterThan(3), "Speaking must change an actual imported facial morph.");
            Assert.That(ChangedMorphHasAuthoredVertexDeltas(actor, neutral), Is.True, "The speaking channel must deform authored face vertices, not an empty named shape.");
            actor.SetSpeaking(false); yield return null; yield return null;
            var closedMouth = ShapeWeights(actor); float blink = 0, end = Time.realtimeSinceStartup + 9f;
            while (Time.realtimeSinceStartup < end && blink < 3)
            { yield return null; blink = Mathf.Max(blink, ShapeDifference(closedMouth, ShapeWeights(actor))); }
            Assert.That(blink, Is.GreaterThan(3), "The live face must actually blink; a named unused channel is insufficient.");
            Assert.That(ChangedMorphHasAuthoredVertexDeltas(actor, closedMouth), Is.True);
            var portrait = actor.GetPortraitTexture() as RenderTexture;
            Assert.That(portrait, Is.Not.Null); yield return null; yield return null;
            AssertPortraitPixels(portrait);
            var camera = actor.CharacterView.PortraitCamera; Assert.That(camera, Is.Not.Null); Assert.That(camera.targetTexture, Is.SameAs(portrait));
            var portraitMeshes = camera.transform.parent.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.That(new HashSet<Mesh>(portraitMeshes.Select(item => item.sharedMesh)).SetEquals(actor.SkinnedRenderers.Select(item => item.sharedMesh)), Is.True,
                "The portrait must use the same actual model meshes, not an unrelated painted face.");
            actor.SetPortraitVisible(false); Assert.That(camera.enabled, Is.False);
            Assert.That(actor.GetPortraitTexture(), Is.SameAs(portrait));
            actor.CharacterView.ReleasePortrait(); yield return null;
            Assert.That(actor.CharacterView.CachedPortraitTexture, Is.Null); Assert.That(actor.CharacterView.PortraitCamera, Is.Null);
            Assert.That(portrait == null || !portrait.IsCreated(), Is.True);
            App.Ui.CanvasView.gameObject.SetActive(false); App.Actor.gameObject.SetActive(false);
            App.CameraRig.SetTarget(actor.VisualFootOrigin.parent); App.CameraRig.SetOrbit(0, 10); App.CameraRig.SetZoom(3); App.CameraRig.Snap();
            yield return Shot("clean-actual-face");
        }

        static void AssertInView(Camera camera, Vector3 point)
        {
            Vector3 viewport = camera.WorldToViewportPoint(point);
            Assert.That(viewport.z, Is.GreaterThan(camera.nearClipPlane));
            Assert.That(viewport.x, Is.InRange(-.03f, 1.03f)); Assert.That(viewport.y, Is.InRange(-.03f, 1.03f));
        }

        [UnityTest]
        public IEnumerator PerspectiveOrbitAndMarkedSceneryOcclusionKeepPhysicalPose()
        {
            App.Ui.CanvasView.gameObject.SetActive(false);
            var rig = App.CameraRig; var camera = App.SceneCamera;
            Assert.That(camera.orthographic, Is.False); Assert.That(camera.fieldOfView, Is.InRange(40f, 55f));
            Vector3 physical = App.Player.Position; float physicalYaw = App.Player.FacingYaw;
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                rig.SetOrbit(yaw, 18); rig.SetZoom(5.6f); rig.Snap(); yield return null;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(rig.Yaw, yaw)), Is.LessThan(.01f));
                AssertInView(camera, App.Actor.CharacterView.Head.position);
                AssertInView(camera, App.Actor.VisualFootOrigin.position + Vector3.up * .04f);
                Assert.That(Finite(camera.transform.position.x) && Finite(camera.transform.position.y) && Finite(camera.transform.position.z), Is.True);
            }
            rig.SetOrbit(47, 18); rig.Snap(); float baseline = rig.ResolvedDistance;
            Assert.That(baseline, Is.GreaterThan(1.2f));
            Vector3 focus = camera.transform.position + camera.transform.forward * baseline;
            var obstruction = new GameObject("NPR camera-only clearance regression fixture");
            try
            {
                obstruction.transform.SetPositionAndRotation(focus - camera.transform.forward * baseline * .55f, camera.transform.rotation);
                var box = obstruction.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(2.5f, 3, .15f);
                Physics.SyncTransforms(); rig.Snap();
                Assert.That(rig.ResolvedDistance, Is.EqualTo(baseline).Within(.025f), "An ordinary trigger must not become a camera wall.");
                obstruction.AddComponent<GospelNprCameraScenery>(); Physics.SyncTransforms(); rig.Snap();
                Assert.That(rig.IsObstructed, Is.True); Assert.That(rig.ObstructionCount, Is.GreaterThan(0));
                Assert.That(rig.DesiredPathDistance, Is.LessThan(baseline - .15f), "The unchanged requested-ray threshold proves the actual marked obstruction.");
                if (rig.IsAvoidingObstruction)
                {
                    Assert.That(rig.ComfortableDistance, Is.GreaterThanOrEqualTo(2.25f));
                    Assert.That(rig.ResolvedDistance, Is.GreaterThanOrEqualTo(rig.ComfortableDistance));
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(rig.EffectivePitch, rig.Pitch)) > .01f
                        || Mathf.Abs(Mathf.DeltaAngle(rig.EffectiveYaw, rig.Yaw)) > .01f, Is.True);
                    AssertReadableTraveler(camera);
                    AssertCameraClearOfActualScenery(camera, new Collider[] { box });
                }
                else Assert.That(rig.ResolvedDistance, Is.LessThan(baseline - .15f));
                Assert.That(Vector3.Distance(App.Player.Position, physical), Is.LessThan(.001f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, physicalYaw)), Is.LessThan(.001f));
                Assert.That(App.World.GetComponentsInChildren<GospelNprCameraScenery>(true).Length, Is.GreaterThan(0), "The shipped world must use the same camera-only clearance mechanism.");
            }
            finally { UnityEngine.Object.Destroy(obstruction); }
            yield return null; rig.Snap(); yield return Shot("perspective-spatial-field");
            yield return ExerciseNearCameraNpcRendering(rig, camera);
            yield return ExerciseActualSceneryCameraClearance(rig, camera);
        }

        IEnumerator ExerciseActualSceneryCameraClearance(GospelPilgrimageCamera rig, Camera camera)
        {
            // Controlled Native geometry setup only. Pause through the actual
            // app so camera queries cannot advance people, input or autosaving.
            // Public/browser routes still exercise ordinary movement separately.
            float originalYaw = rig.Yaw, originalPitch = rig.Pitch, originalZoom = rig.Zoom;
            App.Pause(); yield return null;
            App.Ui.CanvasView.gameObject.SetActive(false);
            string slot = App.ActiveSaveSlot, savedOriginal = backend.Read(App.ActiveSaveSlot);
            string peopleOriginal = JsonUtility.ToJson(App.People.Capture());
            string coreOriginal = GospelSaveCodec.Encode(App.Journey.CaptureSave());
            var well = App.World.GetComponentsInChildren<GospelAuthoredPropRoot>(false).Single(prop => prop.Kind == "well");
            Assert.That(well.transform.position.x, Is.EqualTo(-9.2f).Within(.01f));
            Assert.That(well.transform.position.z, Is.EqualTo(-4.2f).Within(.01f));
            Assert.That(well.GetComponentsInChildren<MeshFilter>().Any(filter => filter.sharedMesh != null && filter.sharedMesh.vertexCount > 3), Is.True);
            var wellTimber = well.GetComponentsInChildren<BoxCollider>().Single(collider => collider.name == "Well timber collision" && collider.transform.localPosition.x > 0);
            var workshop = App.World.GetComponentsInChildren<Transform>(false).Single(node => node.name == "Open Nazareth cedar and linen workshop");
            var posts = workshop.GetComponentsInChildren<GospelNprCameraScenery>()
                .Where(marker => marker.transform.parent.name == "Workshop raised cedar post")
                .Select(marker => marker.GetComponent<BoxCollider>()).OrderBy(collider => collider.bounds.center.x).ThenBy(collider => collider.bounds.center.z).ToArray();
            Assert.That(posts.Length, Is.EqualTo(4));
            Assert.That(posts[0].transform.parent.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
            Vector3 wellForward = Quaternion.Euler(0, 47, 0) * Vector3.forward;
            Vector3 pillarForward = Quaternion.Euler(0, 227, 0) * Vector3.forward;
            var cases = new[]
            {
                new SceneryCameraCase { Name = "well", Yaw = 47, Foot = wellTimber.bounds.center + wellForward * .85f,
                    Obstacles = well.GetComponentsInChildren<Collider>() },
                new SceneryCameraCase { Name = "workshop-pillar", Yaw = 227, Foot = posts[0].bounds.center + pillarForward * 1.15f,
                    Obstacles = workshop.GetComponentsInChildren<GospelNprCameraScenery>().Select(marker => (Collider)marker.GetComponent<BoxCollider>()).ToArray() }
            };
            int cullingMask = camera.cullingMask; float fieldOfView = camera.fieldOfView;
            var travelerMeshes = App.Actor.SkinnedRenderers.ToArray();
            var drawFlags = travelerMeshes.Select(mesh => mesh.forceRenderingOff).ToArray();
            var enabledFlags = travelerMeshes.Select(mesh => mesh.enabled).ToArray();
            try
            {
                foreach (var item in cases)
                {
                    // Position only the physical fixture before recording its
                    // invariant pose. The camera must then solve the real scene
                    // without moving this owner or writing the saved record.
                    Vector3 foot = item.Foot; foot.y = App.World.HeightAt(foot.x, foot.z);
                    App.Player.SetPose(foot, 137); App.Actor.SnapToPhysicalPose(); Physics.SyncTransforms();
                    yield return SmokeUi.WaitUntil(() => App.Player.IsGrounded, 3, "The scenery camera fixture did not settle onto the real field: " + item.Name);
                    Vector3 physical = App.Player.Position; float physicalYaw = App.Player.FacingYaw;
                    rig.SetOrbit(item.Yaw, 18); rig.SetZoom(5.6f); rig.Snap(); yield return null;
                    Assert.That(rig.IsObstructed, Is.True); Assert.That(rig.ObstructionCount, Is.GreaterThan(0));
                    Assert.That(rig.DesiredPathDistance, Is.LessThan(rig.ComfortableDistance), "The actual well/pillar must reproduce an unreadable requested camera path.");
                    Assert.That(rig.IsAvoidingObstruction, Is.True);
                    Assert.That(rig.ComfortableDistance, Is.GreaterThanOrEqualTo(2.25f));
                    Assert.That(rig.ResolvedDistance, Is.GreaterThanOrEqualTo(rig.ComfortableDistance));
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(rig.EffectivePitch, rig.Pitch)) > .01f
                        || Mathf.Abs(Mathf.DeltaAngle(rig.EffectiveYaw, rig.Yaw)) > .01f, Is.True, "A safe transient rendering view must differ from the obstructed requested view.");
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.x, rig.EffectivePitch)), Is.LessThan(.01f));
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.y, rig.EffectiveYaw)), Is.LessThan(.01f));
                    Assert.That(rig.FinalPathDistance, Is.GreaterThanOrEqualTo(rig.ResolvedDistance));
                    AssertReadableTraveler(camera);
                    AssertCameraClearOfActualScenery(camera, item.Obstacles);
                    yield return Shot("actual-" + item.Name + "-keeps-full-traveler-visible");
                    AssertReadableTraveler(camera); AssertCameraClearOfActualScenery(camera, item.Obstacles);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, camera.transform.eulerAngles.x)), Is.LessThanOrEqualTo(65f),
                        "The actual " + item.Name + " view must retain a natural third-person pitch instead of an extreme overhead escape.");
                    Assert.That(Vector3.Distance(App.Player.Position, physical), Is.LessThan(.001f));
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, physicalYaw)), Is.LessThan(.001f));
                    Assert.That(rig.Yaw, Is.EqualTo(item.Yaw)); Assert.That(rig.Pitch, Is.EqualTo(18)); Assert.That(rig.Zoom, Is.EqualTo(5.6f));
                    // Motor movement follows the actual rendered camera axes.
                    // A transient yaw escape must report those visible axes,
                    // while preserving the user's requested orbit separately.
                    Vector3 forward = Quaternion.Euler(0, rig.EffectiveYaw, 0) * Vector3.forward;
                    Vector3 right = Quaternion.Euler(0, rig.EffectiveYaw, 0) * Vector3.right;
                    Assert.That(Vector3.Dot(Flat(camera.transform.forward), forward), Is.GreaterThan(.9999f), "The reported effective view must match the actual movement camera basis.");
                    Assert.That(Vector3.Dot(Flat(camera.transform.right), right), Is.GreaterThan(.9999f));
                    Assert.That(camera.cullingMask, Is.EqualTo(cullingMask)); Assert.That(camera.fieldOfView, Is.EqualTo(fieldOfView));
                    Assert.That(App.ActiveSaveSlot, Is.EqualTo(slot)); Assert.That(backend.Read(slot), Is.EqualTo(savedOriginal));
                    Assert.That(JsonUtility.ToJson(App.People.Capture()), Is.EqualTo(peopleOriginal));
                    Assert.That(GospelSaveCodec.Encode(App.Journey.CaptureSave()), Is.EqualTo(coreOriginal));
                    for (int index = 0; index < travelerMeshes.Length; index++)
                    {
                        Assert.That(travelerMeshes[index].enabled, Is.EqualTo(enabledFlags[index]));
                        Assert.That(travelerMeshes[index].forceRenderingOff, Is.EqualTo(drawFlags[index]));
                    }
                    // A deliberately clear view from the other side must
                    // return to the requested pitch without a physical move.
                    rig.SetOrbit(Mathf.Repeat(item.Yaw + 180, 360), 18); rig.Snap(); yield return null;
                    Assert.That(rig.DesiredPathDistance, Is.GreaterThanOrEqualTo(rig.ComfortableDistance));
                    Assert.That(rig.IsAvoidingObstruction, Is.False);
                    Assert.That(rig.EffectivePitch, Is.EqualTo(rig.Pitch).Within(.01f));
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(rig.EffectiveYaw, rig.Yaw)), Is.LessThan(.01f));
                    AssertReadableTraveler(camera);
                    Assert.That(Vector3.Distance(App.Player.Position, physical), Is.LessThan(.001f));
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, physicalYaw)), Is.LessThan(.001f));
                    Assert.That(backend.Read(slot), Is.EqualTo(savedOriginal));
                    Assert.That(JsonUtility.ToJson(App.People.Capture()), Is.EqualTo(peopleOriginal));
                }
            }
            finally { rig.SetOrbit(originalYaw, originalPitch); rig.SetZoom(originalZoom); rig.Snap(); }
        }

        sealed class SceneryCameraCase
        {
            public string Name;
            public float Yaw;
            public Vector3 Foot;
            public Collider[] Obstacles;
        }

        void AssertReadableTraveler(Camera camera)
        {
            Assert.That(camera.orthographic, Is.False);
            Assert.That(Finite(camera.transform.position.x) && Finite(camera.transform.position.y) && Finite(camera.transform.position.z), Is.True);
            Vector3 head = camera.WorldToViewportPoint(App.Actor.CharacterView.Head.position);
            Vector3 feet = camera.WorldToViewportPoint(App.Actor.VisualFootOrigin.position + Vector3.up * .04f);
            foreach (var point in new[] { head, feet })
            {
                Assert.That(point.z, Is.GreaterThan(camera.nearClipPlane));
                Assert.That(point.x, Is.InRange(.06f, .94f)); Assert.That(point.y, Is.InRange(.06f, .94f));
            }
            Assert.That(Mathf.Abs(head.y - feet.y), Is.InRange(.08f, .78f), "The actual traveler must fit with readable head-to-foot separation, not fill the view with a torso.");
            Assert.That(Vector3.Distance(camera.transform.position, App.Actor.CharacterView.Head.position), Is.GreaterThan(1.2f));
        }

        void AssertCameraClearOfActualScenery(Camera camera, IReadOnlyList<Collider> obstacles)
        {
            var corners = new Vector3[4];
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, corners);
            var overlaps = Physics.OverlapSphere(camera.transform.position, .20f, ~0, QueryTriggerInteraction.Collide);
            Assert.That(obstacles.Any(obstacle => obstacle != null && overlaps.Contains(obstacle)), Is.False, "The actual camera endpoint sphere must remain outside scene geometry.");
            var samples = new List<Vector3> { camera.transform.position };
            samples.AddRange(corners.Select(point => camera.transform.TransformPoint(point)));
            foreach (var obstacle in obstacles.Where(collider => collider != null && collider.enabled && collider.gameObject.activeInHierarchy))
            foreach (var point in samples)
                Assert.That(Vector3.Distance(point, obstacle.ClosestPoint(point)), Is.GreaterThanOrEqualTo(.04f), "The eye and real near-plane corners need geometric clearance from " + obstacle.name);
            var hips = App.Actor.Animator.GetBoneTransform(HumanBodyBones.Hips);
            Assert.That(hips, Is.Not.Null);
            foreach (var target in new[] { App.Actor.CharacterView.Head.position, hips.position, App.Actor.VisualFootOrigin.position + Vector3.up * .04f })
            {
                Vector3 delta = target - camera.transform.position;
                var ray = new Ray(camera.transform.position, delta.normalized);
                foreach (var obstacle in obstacles.Where(collider => collider != null && collider.enabled && collider.gameObject.activeInHierarchy))
                    Assert.That(obstacle.Raycast(ray, out var hit, delta.magnitude - .03f), Is.False, "Actual scene geometry must not hide the traveler's head, hips or feet: " + obstacle.name);
            }
        }

        IEnumerator ExerciseNearCameraNpcRendering(GospelPilgrimageCamera rig, Camera camera)
        {
            // The existing orbit/scenery checks above run on the title. Enter
            // the real field through all51 opening callbacks before exercising
            // the field-only NPC render scope.
            App.Ui.CanvasView.gameObject.SetActive(true); yield return null;
            yield return ReachFirstField();
            yield return SmokeUi.WaitUntil(() => App.Player.IsGrounded, 3, "The camera regression traveler did not settle onto the real field.");
            App.Ui.CanvasView.gameObject.SetActive(false);
            rig.SetOrbit(47, 18); rig.SetZoom(5.6f); rig.Snap();
            var masking = camera.GetComponent<GospelNprNpcCameraOcclusion>();
            Assert.That(masking, Is.Not.Null, "The shipped field camera must own the NPC render scope.");
            Assert.That(masking.isActiveAndEnabled, Is.True);
            Vector3 physical = App.Player.Position; float physicalYaw = App.Player.FacingYaw;
            float zoom = rig.Zoom, yaw = rig.Yaw, pitch = rig.Pitch, distance = rig.ResolvedDistance;
            float staticPathDistance = rig.DesiredPathDistance; int staticObstructions = rig.ObstructionCount;
            bool staticAvoidance = rig.IsAvoidingObstruction;
            int named = App.People.NamedCount, ambient = App.People.AmbientCount, people = App.People.Count;
            var currentPerson = App.People.CurrentPerson;
            string slot = App.ActiveSaveSlot, quest = App.Chronicle.CurrentQuest.Id;
            string savedOriginal = backend.Read(slot);
            int discovered = App.Chronicle.DiscoveredCount; var stage = App.Chronicle.Stage;
            var travelerMeshes = App.Actor.SkinnedRenderers.ToArray();
            var travelerFlags = travelerMeshes.Select(mesh => mesh.forceRenderingOff).ToArray();
            Assert.That(travelerMeshes.Any(mesh => mesh.enabled && !mesh.forceRenderingOff), Is.True);
            var owner = new GameObject("NPR camera regression NPC physical owner").transform;
            owner.SetParent(App.People.transform, false);
            GospelPilgrimageActor blocker = null;
            CharacterController npcController = null;
            Action<ScriptableRenderContext, Camera> begin = null, end = null;
            try
            {
                // This is an independent actual NPR actor under the same field
                // population root, not a registered gameplay person or quest.
                blocker = GospelPilgrimageActor.BuildNpc(owner, camera, "mary");
                blocker.SetGroundSampler(App.World.HeightAt);
                blocker.SetMotion(Vector3.zero, true, yaw + 180);
                // Real People place CharacterController on the physical owner
                // and their Actor on a child. Reproduce that ownership shape;
                // a parent-only Actor query must not turn this NPC into a wall.
                npcController = owner.gameObject.AddComponent<CharacterController>();
                npcController.enabled = false;
                npcController.height = blocker.VisualHeight; npcController.center = Vector3.up * (blocker.VisualHeight * .5f);
                npcController.radius = .21f; npcController.skinWidth = .025f;
                npcController.minMoveDistance = 0; npcController.stepOffset = .26f; npcController.slopeLimit = 55;
                Assert.That(blocker.transform.parent, Is.SameAs(owner)); Assert.That(blocker.transform.localPosition, Is.EqualTo(Vector3.zero));
                var meshes = blocker.SkinnedRenderers.ToArray();
                var flags = meshes.Select(mesh => mesh.forceRenderingOff).ToArray();
                var enabled = meshes.Select(mesh => mesh.enabled).ToArray();
                var materials = meshes.Select(mesh => mesh.sharedMaterials).ToArray();
                var sourceMeshes = meshes.Select(mesh => mesh.sharedMesh).ToArray();
                Assert.That(meshes.Any(mesh => mesh.enabled && !mesh.forceRenderingOff), Is.True);
                var portrait = blocker.GetPortraitTexture() as RenderTexture;
                Assert.That(portrait, Is.Not.Null);
                var portraitCamera = blocker.CharacterView.PortraitCamera;
                Assert.That(portraitCamera, Is.Not.Null);
                var portraitMeshes = portraitCamera.transform.parent.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var portraitFlags = portraitMeshes.Select(mesh => mesh.forceRenderingOff).ToArray();
                Assert.That(portraitMeshes.Length, Is.GreaterThan(0));
                Assert.That(portraitMeshes.All(mesh => mesh.gameObject.layer == GospelNprCharacterView.PortraitLayer), Is.True);
                Assert.That(new HashSet<Mesh>(portraitMeshes.Select(mesh => mesh.sharedMesh)).SetEquals(sourceMeshes), Is.True);
                bool nearPhase = true, nearNpcMasked = true, movedNpcOriginal = true;
                bool travelerDrawState = true, portraitDrawState = true, restoredAfterWorld = true;
                bool observedCorrectWorldScope = true, preservedPeopleDuringRender = true, preservedSaveDuringRender = true;
                string renderPeoplePose = null, renderSave = null;
                int nearRenders = 0, movedRenders = 0, portraitRenders = 0, completedWorldRenders = 0;
                // The camera helper subscribed during scene initialization.
                // Observe its actual draw flags after its begin handler and its
                // exact restoration after its end handler, not private geometry.
                begin = (context, viewed) =>
                {
                    if (viewed == camera)
                    {
                        renderPeoplePose = JsonUtility.ToJson(App.People.Capture()); renderSave = backend.Read(slot);
                        observedCorrectWorldScope &= masking.LastViewedCamera == camera
                            && masking.LastObservedTarget == App.Player.transform;
                        travelerDrawState &= travelerMeshes.Select((mesh, index) => mesh.forceRenderingOff == travelerFlags[index]).All(value => value);
                        portraitDrawState &= portraitMeshes.Select((mesh, index) => mesh.forceRenderingOff == portraitFlags[index]).All(value => value);
                        if (nearPhase)
                        {
                            nearRenders++;
                            nearNpcMasked &= meshes.Select((mesh, index) => !enabled[index] || !mesh.gameObject.activeInHierarchy || mesh.forceRenderingOff).All(value => value)
                                && masking.LastOccludingNpcCount > 0 && masking.LastMaskedRendererCount > 0
                                && masking.OwnedRendererCount > 0;
                        }
                        else
                        {
                            movedRenders++;
                            movedNpcOriginal &= meshes.Select((mesh, index) => mesh.forceRenderingOff == flags[index]).All(value => value);
                        }
                    }
                    else if (viewed == portraitCamera)
                    {
                        portraitRenders++;
                        portraitDrawState &= portraitMeshes.Select((mesh, index) => mesh.forceRenderingOff == portraitFlags[index]).All(value => value)
                            && blocker.CharacterView.HasVisiblePortrait;
                    }
                };
                end = (context, viewed) =>
                {
                    if (viewed != camera) return;
                    completedWorldRenders++;
                    preservedPeopleDuringRender &= renderPeoplePose == JsonUtility.ToJson(App.People.Capture());
                    preservedSaveDuringRender &= renderSave == backend.Read(slot);
                    restoredAfterWorld &= meshes.Select((mesh, index) => mesh.forceRenderingOff == flags[index]).All(value => value)
                        && masking.OwnedRendererCount == 0;
                };
                RenderPipelineManager.beginCameraRendering += begin;
                RenderPipelineManager.endCameraRendering += end;
                // Arrange this one test-owned NPC and its controller together.
                // Camera solving must ignore that NPC's gameplay collider while
                // actual world rendering still masks its near-view robe.
                npcController.enabled = false;
                owner.position = camera.transform.position + camera.transform.forward * .85f
                    - Vector3.up * (blocker.VisualHeight * .54f);
                npcController.enabled = true; Physics.SyncTransforms();
                Vector3 blockingPose = owner.position; Quaternion ownerRotation = owner.rotation;
                string beforeCameraPeople = JsonUtility.ToJson(App.People.Capture()); string beforeCameraSave = backend.Read(slot);
                rig.Snap();
                Assert.That(JsonUtility.ToJson(App.People.Capture()), Is.EqualTo(beforeCameraPeople));
                Assert.That(backend.Read(slot), Is.EqualTo(beforeCameraSave));
                yield return null; yield return null;
                AssertInView(camera, App.Actor.CharacterView.Head.position);
                AssertInView(camera, App.Actor.VisualFootOrigin.position + Vector3.up * .04f);
                yield return Shot("near-camera-npc-keeps-traveler-visible");
                Assert.That(nearRenders, Is.GreaterThan(0)); Assert.That(nearNpcMasked, Is.True, "The actual near-camera NPC draw must be masked while the traveler remains visible.");
                Assert.That(Vector3.Distance(owner.position, blockingPose), Is.LessThan(.0001f), "Render occlusion must not relocate the NPC owner.");
                Assert.That(Quaternion.Angle(owner.rotation, ownerRotation), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(App.Player.Position, physical), Is.LessThan(.001f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, physicalYaw)), Is.LessThan(.001f));
                Assert.That(rig.ResolvedDistance, Is.EqualTo(distance).Within(.025f), "A passing NPC must not push the field camera into the player.");
                Assert.That(rig.DesiredPathDistance, Is.EqualTo(staticPathDistance).Within(.025f), "The People-owned NPC controller must not shorten the scenery probe.");
                Assert.That(rig.ObstructionCount, Is.EqualTo(staticObstructions)); Assert.That(rig.IsAvoidingObstruction, Is.EqualTo(staticAvoidance));
                Assert.That(npcController.enabled, Is.True);
                Assert.That(blocker.CharacterView.HasVisiblePortrait && portraitCamera.enabled, Is.True);
                AssertPortraitPixels(portrait);
                Assert.That(meshes.Select((mesh, index) => mesh.forceRenderingOff == flags[index]).All(value => value), Is.True);
                Assert.That(masking.OwnedRendererCount, Is.Zero);
                // Move only this test-owned visual out of the view and prove
                // that the same renderers recover their original draw state.
                nearPhase = false;
                npcController.enabled = false;
                owner.position = camera.transform.position + camera.transform.right * 8
                    + camera.transform.forward * 2 - Vector3.up * (blocker.VisualHeight * .54f);
                npcController.enabled = true; Physics.SyncTransforms();
                Vector3 clearPose = owner.position;
                yield return null; yield return null;
                AssertInView(camera, App.Actor.CharacterView.Head.position);
                AssertInView(camera, App.Actor.VisualFootOrigin.position + Vector3.up * .04f);
                yield return Shot("npc-moved-clear-restores-field-renderers");
                Assert.That(movedRenders, Is.GreaterThan(0)); Assert.That(movedNpcOriginal, Is.True, "An NPC leaving the view must recover its exact preceding renderer flags.");
                Assert.That(completedWorldRenders, Is.GreaterThan(0)); Assert.That(restoredAfterWorld, Is.True);
                Assert.That(travelerDrawState && portraitDrawState && observedCorrectWorldScope, Is.True);
                Assert.That(preservedPeopleDuringRender && preservedSaveDuringRender, Is.True);
                Assert.That(portraitRenders, Is.GreaterThan(0)); Assert.That(blocker.CharacterView.HasVisiblePortrait && portraitCamera.enabled, Is.True);
                AssertPortraitPixels(portrait);
                Assert.That(Vector3.Distance(owner.position, clearPose), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(owner.rotation, ownerRotation), Is.LessThan(.001f));
                Assert.That(blocker.Animator.enabled && !blocker.Animator.applyRootMotion, Is.True);
                for (int index = 0; index < meshes.Length; index++)
                {
                    Assert.That(meshes[index].enabled, Is.EqualTo(enabled[index]));
                    Assert.That(meshes[index].forceRenderingOff, Is.EqualTo(flags[index]));
                    Assert.That(meshes[index].sharedMesh, Is.SameAs(sourceMeshes[index]));
                    Assert.That(meshes[index].sharedMaterials, Is.EqualTo(materials[index]));
                }
                Assert.That(Vector3.Distance(App.Player.Position, physical), Is.LessThan(.001f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, physicalYaw)), Is.LessThan(.001f));
                Assert.That(rig.Zoom, Is.EqualTo(zoom)); Assert.That(rig.Yaw, Is.EqualTo(yaw)); Assert.That(rig.Pitch, Is.EqualTo(pitch));
                Assert.That(rig.ResolvedDistance, Is.EqualTo(distance).Within(.025f));
                Assert.That(App.People.Count, Is.EqualTo(people)); Assert.That(App.People.NamedCount, Is.EqualTo(named)); Assert.That(App.People.AmbientCount, Is.EqualTo(ambient));
                Assert.That(App.People.CurrentPerson, Is.SameAs(currentPerson)); Assert.That(App.ActiveSaveSlot, Is.EqualTo(slot));
                Assert.That(backend.Read(slot), Is.EqualTo(savedOriginal));
                Assert.That(rig.DesiredPathDistance, Is.EqualTo(staticPathDistance).Within(.025f));
                Assert.That(rig.ObstructionCount, Is.EqualTo(staticObstructions)); Assert.That(rig.IsAvoidingObstruction, Is.EqualTo(staticAvoidance));
                Assert.That(npcController.enabled, Is.True); Assert.That(blocker.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(App.Chronicle.CurrentQuest.Id, Is.EqualTo(quest)); Assert.That(App.Chronicle.Stage, Is.EqualTo(stage)); Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(discovered));
            }
            finally
            {
                if (begin != null) RenderPipelineManager.beginCameraRendering -= begin;
                if (end != null) RenderPipelineManager.endCameraRendering -= end;
                if (blocker != null) { blocker.SetPortraitVisible(false); blocker.CharacterView.ReleasePortrait(); }
                UnityEngine.Object.Destroy(owner.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpatialWorldAndBothChaptersUseThreeDimensionalImportedScenery()
        {
            Assert.That(App.World.NprEnvironmentReady, Is.True); Assert.That(GospelNprEnvironment.RequiredModels.Count, Is.EqualTo(9));
            foreach (string key in GospelNprEnvironment.RequiredModels)
            {
                var source = Resources.Load<GameObject>(GospelNprEnvironment.ResourceRoot + key); Assert.That(source, Is.Not.Null, key);
                var bounds = GospelNprEnvironment.SpatialBounds(source);
                Assert.That(Mathf.Min(bounds.size.x, Mathf.Min(bounds.size.y, bounds.size.z)), Is.GreaterThan(.001f), key);
                var filters = source.GetComponentsInChildren<MeshFilter>(true); Assert.That(filters.Length, Is.GreaterThan(0));
                Assert.That(filters.Sum(item => item.sharedMesh.vertexCount), Is.GreaterThan(12), key);
                Assert.That(filters.All(item => item.sharedMesh.isReadable), Is.True, "The genuine model source must support its static batches.");
            }
            Assert.That(App.World.SpatialSceneryCount, Is.GreaterThan(40)); Assert.That(App.World.SpatialSceneryBatchCount, Is.GreaterThan(0));
            Assert.That(App.World.VillageBuildingCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(App.World.PaintedSceneryCount, Is.Zero);
            foreach (string chapter in new[] { "temple", "baptism" })
            {
                App.World.SetChapter(chapter); yield return null;
                var root = chapter == "temple" ? App.World.TempleRoot : App.World.JordanRoot;
                Assert.That(root.gameObject.activeInHierarchy, Is.True);
                var renderers = root.GetComponentsInChildren<MeshRenderer>().Where(item => item.enabled).ToArray();
                Assert.That(renderers.Length, Is.GreaterThan(10));
                foreach (var material in renderers.SelectMany(item => item.sharedMaterials))
                {
                    Assert.That(material, Is.Not.Null); Assert.That(material.shader, Is.Not.Null); Assert.That(material.shader.isSupported, Is.True, material.name);
                    Assert.That(material.shader.name, Is.Not.EqualTo("FirstGame/Pilgrimage/PaintedScenery"));
                }
                var floor = renderers.Single(item => item.sharedMaterial != null && item.sharedMaterial.shader.name == "FirstGame/GospelNpr/Ground");
                Assert.That(floor.GetComponent<MeshCollider>(), Is.Not.Null);
                foreach (var quest in GospelChronicleCatalog.Quests.Where(item => !item.IsPrologue && item.ChapterId == chapter))
                {
                    Vector3 goal = App.World.PositionForQuest(quest);
                    Assert.That(goal.x, Is.EqualTo(quest.X)); Assert.That(App.World.IsInside(goal), Is.True);
                    Assert.That(Mathf.Abs(goal.y - App.World.HeightAt(goal.x, goal.z)), Is.LessThan(.0001f));
                }
                AssertRawHdrFinite(chapter);
            }
            App.World.SetChapter("temple"); App.CameraRig.Snap();
            yield return Shot("spatial-village-ground-and-scenery");
        }

        void AssertRawHdrFinite(string chapter)
        {
            Assert.That(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf), Is.True);
            Assert.That(SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat), Is.True);
            var camera = App.SceneCamera; var data = camera.GetUniversalAdditionalCameraData();
            bool post = data.renderPostProcessing, stop = data.stopNaN;
            var previous = camera.targetTexture; var active = RenderTexture.active; float aspect = camera.aspect;
            var target = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Texture2D pixels = null;
            try
            {
                // Inspect the new actual renderer before its defensive passes.
                // The HDR sky is allowed to contain genuine values above one;
                // finite source pixels must not depend on post/StopNaN clamping.
                data.renderPostProcessing = false; data.stopNaN = false;
                target.Create(); camera.targetTexture = target; camera.aspect = 640f / 360f;
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request); RenderTexture.active = target;
                pixels = new Texture2D(640, 360, TextureFormat.RGBAFloat, false, true);
                pixels.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); pixels.Apply();
                int invalid = 0; float peak = 0;
                foreach (var color in pixels.GetPixels())
                {
                    if (!Finite(color.r) || !Finite(color.g) || !Finite(color.b)) invalid++;
                    else peak = Mathf.Max(peak, Mathf.Max(color.r, Mathf.Max(color.g, color.b)));
                }
                Debug.Log("FIRSTGAME_NPR_RAW_HDR chapter=" + chapter + " pixels=230400 post=false stopNaN=false invalid=" + invalid
                    + " peak=" + peak.ToString("F6") + " graphics=" + SystemInfo.graphicsDeviceName);
                Assert.That(invalid, Is.Zero, "New NPR source HDR pixels must be finite before protective passes.");
                Assert.That(peak, Is.GreaterThan(.03f), "The actual raw scene cannot be an empty black target.");
            }
            finally
            {
                data.renderPostProcessing = post; data.stopNaN = stop;
                camera.targetTexture = previous; camera.aspect = aspect; RenderTexture.active = active;
                if (pixels != null) UnityEngine.Object.Destroy(pixels); target.Release(); UnityEngine.Object.Destroy(target);
            }
        }

        IEnumerator ReachFirstField()
        {
            yield return Click("새 여정 시작"); int turns = 0;
            while (App.Chronicle.CurrentQuest.IsPrologue && turns++ < 60) yield return Click("이야기 계속");
            Assert.That(turns, Is.EqualTo(51), "Retain the actual opening; do not inject a later scene state.");
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(11));
            yield return Click("이야기 속으로");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("journey")); Assert.That(App.People.NamedCount, Is.EqualTo(5));
            Assert.That(App.People.AmbientCount, Is.EqualTo(6));
            yield return new WaitForSecondsRealtime(.2f);
        }

        [UnityTest]
        public IEnumerator CameraRelativeTouchMovementDashAndJumpKeepTheSavedTravelerPhysical()
        {
            // Select the Editor touch-capability branch explicitly. The real
            // UI callbacks below do not claim native touch pointer hit testing.
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            Assert.That(Touchscreen.current, Is.SameAs(touchscreen));

            try
            {
                yield return ReachFirstField(); Assert.That(App.Ui.HasCameraTouchPad, Is.True);
                var map = App.Ui.Minimap; Assert.That(map, Is.Not.Null); Assert.That(map.HasLiveWorld, Is.True);
                Assert.That(map.WorldBounds, Is.EqualTo(App.World.Bounds)); Assert.That(map.PersonMarkerCount, Is.EqualTo(App.People.Count));
                Vector3 before = App.Player.Position;
                Vector3 desired = (Flat(App.SceneCamera.transform.right) * .6f + Flat(App.SceneCamera.transform.forward) * .4f).normalized;
                App.Ui.OnMove(new Vector2(.6f, .4f)); yield return SimulatedMovementWindow("first-direction-hold", .38f);
                App.Ui.OnMove(Vector2.zero); yield return SimulatedMovementWindow("first-direction-stop", .25f);
                var displacement = App.Player.Position - before; displacement.y = 0;
                Assert.That(displacement.magnitude, Is.GreaterThan(.35f)); Assert.That(Vector3.Dot(displacement.normalized, desired), Is.GreaterThan(.85f));
                Assert.That(App.Player.MeasuredSpeed, Is.LessThan(.05f)); Assert.That(App.Actor.CurrentState, Is.EqualTo("Idle"));
                Vector3 mapPose = App.Player.Position; var bounds = App.World.Bounds;
                Assert.That(map.PlayerMapPosition.x, Is.EqualTo((mapPose.x - bounds.xMin) / bounds.width).Within(.001f));
                Assert.That(map.PlayerMapPosition.y, Is.EqualTo((mapPose.z - bounds.yMin) / bounds.height).Within(.001f));
                App.CameraRig.OrbitBy(new Vector2(90, 0)); App.CameraRig.Snap();
                before = App.Player.Position; desired = Flat(App.SceneCamera.transform.forward);
                App.Ui.OnMove(Vector2.up); yield return SimulatedMovementWindow("orbited-direction-hold", .24f); App.Ui.OnMove(Vector2.zero);
                yield return SimulatedMovementWindow("orbited-direction-stop", .22f);
                displacement = App.Player.Position - before; displacement.y = 0;
                Assert.That(displacement.magnitude, Is.GreaterThan(.20f)); Assert.That(Vector3.Dot(displacement.normalized, desired), Is.GreaterThan(.80f));
                yield return SmokeUi.WaitUntil(() => App.Player.IsGrounded, 3, "The real traveler did not settle onto the field.");
                float baseY = App.Player.Position.y, cameraY = App.SceneCamera.transform.position.y, maxY = baseY, maxCameraDrift = 0;
                App.Ui.OnJump();
                yield return SimulatedMovementWindow("jump-flight", 1.8f, () =>
                {
                    maxY = Mathf.Max(maxY, App.Player.Position.y);
                    maxCameraDrift = Mathf.Max(maxCameraDrift, Mathf.Abs(App.SceneCamera.transform.position.y - cameraY));
                });
                Assert.That(maxY - baseY, Is.GreaterThan(.25f)); Assert.That(App.Player.IsGrounded, Is.True);
                Assert.That(maxCameraDrift, Is.LessThan((maxY - baseY) * .65f + .15f), "The perspective camera should follow a jump gently rather than lifting the entire field with the feet.");
                int sequence = App.Player.DashSequence; before = App.Player.Position; App.Ui.OnDash();
                yield return SmokeUi.WaitUntil(() => App.Player.DashSequence > sequence, 2, "The visible dash callback did not reach the physical motor.");
                yield return SimulatedMovementWindow("dash-finish", .4f);
                Assert.That(Planar(before, App.Player.Position), Is.GreaterThan(.4f)); Assert.That(App.Player.IsDashing, Is.False);
                var planted = App.Player.Position; yield return SimulatedMovementWindow("planted-idle", .35f);
                Assert.That(Planar(planted, App.Player.Position), Is.LessThan(.04f));
                Assert.That(App.Player.MeasuredSpeed, Is.LessThan(.05f)); Assert.That(App.World.IsInside(App.Player.Position), Is.True);
                yield return Shot("camera-relative-touch-dash-and-jump");
            }
            finally
            {
                var active = App;
                if (active != null && active.Ui != null) active.Ui.OnMove?.Invoke(Vector2.zero);
            }
        }

        // Movement is integrated with Time.deltaTime. Observe its stated game-
        // time interval while retaining an independent, fail-closed wall limit.
        IEnumerator SimulatedMovementWindow(string phase, float seconds, Action observeFrame = null)
        {
            float gameStart = Time.time, wallStart = Time.realtimeSinceStartup;
            int frameStart = Time.frameCount;
            float gameEnd = gameStart + seconds, wallEnd = wallStart + 8f;
            LogMovementWindow(phase, "start", seconds, gameStart, wallStart, frameStart);
            try
            {
                while (Time.time < gameEnd && Time.realtimeSinceStartup < wallEnd)
                {
                    yield return null;
                    observeFrame?.Invoke();
                }
            }
            finally { LogMovementWindow(phase, "end", seconds, gameStart, wallStart, frameStart); }
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(wallEnd), "Movement simulation exceeded its independent 8-second wall deadline: " + phase);
            Assert.That(Time.time, Is.GreaterThanOrEqualTo(gameEnd), "Movement simulation did not complete its unchanged game-time interval: " + phase);
        }

        void LogMovementWindow(string phase, string edge, float seconds, float gameStart, float wallStart, int frameStart)
        {
            var app = App; var player = app != null ? app.Player : null;
            var camera = app != null ? app.SceneCamera : null;
            Debug.Log("FIRSTGAME_NPR_MOVEMENT_WINDOW phase=" + phase + " edge=" + edge + " requiredGameSeconds=" + seconds
                + " frame=" + Time.frameCount + " frames=" + (Time.frameCount - frameStart)
                + " game=" + Time.time + " elapsedGame=" + (Time.time - gameStart)
                + " wall=" + Time.realtimeSinceStartup + " elapsedWall=" + (Time.realtimeSinceStartup - wallStart)
                + " dt=" + Time.deltaTime + " unscaledDt=" + Time.unscaledDeltaTime
                + " timeScale=" + Time.timeScale + " maximumDt=" + Time.maximumDeltaTime
                + " position=" + (player != null ? player.Position.ToString("F4") : "missing")
                + " velocity=" + (player != null ? player.Velocity.ToString("F4") : "missing")
                + " measuredSpeed=" + (player != null ? player.MeasuredSpeed.ToString() : "missing")
                + " inputEnabled=" + (player != null && player.Enabled) + " grounded=" + (player != null && player.IsGrounded)
                + " view=" + (app != null && app.Ui != null ? app.Ui.CurrentView : "missing")
                + " cameraTargetYaw=" + (app != null && app.CameraRig != null ? app.CameraRig.Yaw.ToString() : "missing")
                + " cameraForward=" + (camera != null ? Flat(camera.transform.forward).ToString("F4") : "missing"));
        }

        IEnumerator ReachCurrentPerson()
        {
            float end = Time.realtimeSinceStartup + 45, lastProgress = Time.realtimeSinceStartup;
            Vector3 previous = App.Player.Position;
            while (!App.CanInteract && Time.realtimeSinceStartup < end)
            {
                var delta = App.ObjectivePosition - App.Player.Position; delta.y = 0;
                if (delta.magnitude < .82f)
                { Keys(); yield return new WaitForSecondsRealtime(.06f); lastProgress = Time.realtimeSinceStartup; continue; }
                Vector3 right = Flat(App.SceneCamera.transform.right), forward = Flat(App.SceneCamera.transform.forward);
                float x = Vector3.Dot(delta, right), y = Vector3.Dot(delta, forward);
                var held = new List<Key> { Key.LeftShift };
                if (Mathf.Abs(x) > .25f) held.Add(x > 0 ? Key.D : Key.A);
                if (Mathf.Abs(y) > .25f) held.Add(y > 0 ? Key.W : Key.S);
                Keys(held.ToArray()); yield return new WaitForSecondsRealtime(.06f);
                if (Planar(previous, App.Player.Position) > .08f) { previous = App.Player.Position; lastProgress = Time.realtimeSinceStartup; }
                else if (Time.realtimeSinceStartup - lastProgress > 1.2f)
                {
                    // A short ordinary perpendicular step can clear a real
                    // column; never move the capsule or bypass Core gates.
                    Keys(Key.LeftShift, Mathf.Abs(x) > Mathf.Abs(y) ? Key.W : Key.D);
                    yield return new WaitForSecondsRealtime(.28f); Keys(); lastProgress = Time.realtimeSinceStartup;
                }
            }
            Keys(); yield return new WaitForSecondsRealtime(.12f);
            if (!App.CanInteract) yield return Shot("travel-failure-" + App.Chronicle.CurrentQuest.Id);
            Assert.That(App.CanInteract, Is.True, "Ordinary camera-relative input must reach the moving NPC and unchanged Core X range: " + App.Chronicle.CurrentQuest.Id);
            Assert.That(Planar(App.Player.Position, App.ObjectivePosition), Is.LessThanOrEqualTo(GospelChronicle.InteractionRadius));
        }
        IEnumerator AcceptCurrentPerson()
        {
            Keys(Key.E); yield return null; yield return null; Keys();
            yield return SmokeUi.WaitUntil(() => App.Chronicle.Stage == GospelQuestStage.Reading, 3, "The nearby E input did not accept its real encounter.");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("story"));
        }
        IEnumerator FinishCurrentEvent()
        {
            int index = App.Chronicle.QuestIndex; int guard = 0;
            while (!App.Chronicle.IsComplete && App.Chronicle.QuestIndex == index && guard++ < 40) yield return Click("이야기 계속");
            Assert.That(guard, Is.LessThan(40));
            if (App.Ui.CurrentView == "scripture-acquired") yield return Click("이야기 계속");
        }

        [UnityTest]
        public IEnumerator JosephAndAmbientPeopleRemainRealHumanoidsAcrossDialogueAndReload()
        {
            yield return ReachFirstField();
            var start = App.People.Markers.ToDictionary(person => person.Id, person => person.Position);
            var traveled = start.ToDictionary(pair => pair.Key, pair => 0f);
            var roots = App.People.Markers.ToDictionary(person => person.Id, person => person.RootTransform.GetInstanceID());
            float end = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                foreach (var person in App.People.Markers)
                {
                    traveled[person.Id] += Planar(start[person.Id], person.Position); start[person.Id] = person.Position;
                    Assert.That(person.RootTransform.GetInstanceID(), Is.EqualTo(roots[person.Id]));
                    var actor = person.RootTransform.GetComponentInChildren<GospelPilgrimageActor>();
                    Assert.That(actor.HasSkinnedModel, Is.True); Assert.That(actor.SkinnedRenderers.Count, Is.GreaterThan(0));
                    Assert.That(App.World.IsInside(person.Position), Is.True);
                    Assert.That(Mathf.Abs(person.Position.y - App.World.HeightAt(person.Position.x, person.Position.z)), Is.LessThan(.15f));
                }
            }
            Assert.That(App.People.Markers.Count(person => person.IsAmbient && traveled[person.Id] > .4f), Is.EqualTo(6));
            var joseph = App.People.CurrentPerson.RootTransform; int josephIdentity = joseph.GetInstanceID();
            yield return ReachCurrentPerson(); yield return AcceptCurrentPerson();
            yield return FinishCurrentEvent();
            Assert.That(App.Chronicle.CurrentQuest.Id, Is.EqualTo("pilgrim-spring"));
            Assert.That(joseph != null && joseph.gameObject.activeInHierarchy, Is.True);
            Assert.That(joseph.GetInstanceID(), Is.EqualTo(josephIdentity));
            Assert.That(App.Save(false), Is.True); string slot = App.ActiveSaveSlot;
            var pose = App.Player.Position; float yaw = App.Player.FacingYaw; int quest = App.Chronicle.QuestIndex;
            yield return SmokeUi.LoadBoot(); yield return Click("이어하기");
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(slot)); Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(quest));
            Assert.That(Planar(App.Player.Position, pose), Is.LessThan(.12f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, yaw)), Is.LessThan(.01f));
            Assert.That(App.People.Count, Is.EqualTo(11)); Assert.That(App.People.CurrentPerson.Id, Is.EqualTo("mary"));
            Assert.That(App.People.Markers.Single(person => person.Id == "joseph").RootTransform.GetComponentInChildren<GospelPilgrimageActor>().HasSkinnedModel, Is.True);
            yield return Shot("people-remain-after-dialogue-reload");
        }

        string StoryOpeningStateSnapshot(string phase)
        {
            var owner = App;
            var chronicle = owner != null ? owner.Chronicle : null;
            var quest = chronicle != null ? chronicle.CurrentQuest : null;
            var opening = owner != null ? owner.OpeningStage : null;
            var keys = Keyboard.current;
            string snapshot = "phase=" + phase + " frame=" + Time.frameCount
                + " realtime=" + Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " appId=" + (owner != null ? owner.GetInstanceID() : 0)
                + " inGame=" + (owner != null && owner.InGame)
                + " complete=" + (chronicle != null ? chronicle.IsComplete.ToString() : "<none>")
                + " prologue=" + (quest != null ? quest.IsPrologue.ToString() : "<none>")
                + " questId=" + (quest != null ? quest.Id : "<none>")
                + " stage=" + (chronicle != null ? chronicle.Stage.ToString() : "<none>")
                + " voiceKey=" + (chronicle != null && !chronicle.IsComplete ? chronicle.StoryVoiceKey : "<none>")
                + " uiView=" + (owner != null && owner.Ui != null ? owner.Ui.CurrentView : "<none>")
                + " openingId=" + (opening != null ? opening.GetInstanceID() : 0)
                + " openingVisible=" + (opening != null && opening.IsVisible)
                + " openingSet=" + (opening != null ? opening.CurrentSet : "<none>")
                + " openingActive=" + (opening != null && opening.gameObject.activeInHierarchy)
                + " openingEnabled=" + (opening != null && opening.enabled)
                + " actorActive=" + (owner != null && owner.Actor != null && owner.Actor.gameObject.activeInHierarchy)
                + " rigEnabled=" + (owner != null && owner.CameraRig != null && owner.CameraRig.enabled)
                + " keyM=" + (keys != null && keys.mKey.wasPressedThisFrame)
                + " keyI=" + (keys != null && keys.iKey.wasPressedThisFrame)
                + " keyB=" + (keys != null && keys.bKey.wasPressedThisFrame)
                + " keyEscape=" + (keys != null && keys.escapeKey.wasPressedThisFrame);
            Debug.Log("FIRSTGAME_NPR_STORY_OPENING_DIAGNOSTIC " + snapshot);
            return snapshot;
        }

        [UnityTest]
        public IEnumerator StoryDialogueUsesActualModelPortraitWithoutEditorialMetadata()
        {
            yield return Click("새 여정 시작"); int guard = 0;
            while (App.Chronicle.CurrentStoryBeat.Speaker != "마리아" && guard++ < 10) yield return Click("이야기 계속");
            Assert.That(App.Chronicle.CurrentStoryBeat.Speaker, Is.EqualTo("마리아"));
            Assert.That(App.OpeningStage, Is.Not.Null); Assert.That(App.OpeningStage.IsVisible, Is.True);
            Assert.That(App.OpeningStage.CurrentSet, Is.EqualTo("nazareth"));
            Assert.That(App.Actor.gameObject.activeInHierarchy, Is.False, "Do not show the future-age field traveler in the birth scene.");
            Assert.That(App.CameraRig.enabled, Is.False);
            Assert.That(App.Player.gameObject.activeInHierarchy && App.Player.Controller.enabled, Is.True, "Hide only the visual child, never the physical traveler.");
            Assert.That(App.People.Count, Is.Zero, "Opening performers must not invent physical story people.");
            string beat = App.Chronicle.StoryVoiceKey; string line = App.Chronicle.CurrentStoryBeat.Text;
            string reference = App.Chronicle.CurrentStoryReference;
            Assert.That(VisibleText(), Does.Contain(line)); Assert.That(VisibleText(), Does.Contain("마리아"));
            Assert.That(VisibleText(), Does.Not.Contain("극화 대화")); Assert.That(VisibleText(), Does.Not.Contain("성경 직접 인용"));
            Assert.That(VisibleText(), Does.Not.Contain(reference));
            var image = App.Ui.CanvasView.GetComponentsInChildren<RawImage>()
                .Single(item => item.gameObject.activeInHierarchy && item.gameObject.name == "Actual story speaker model portrait");
            yield return SmokeUi.WaitUntil(() => image.enabled && image.texture is RenderTexture, 3, "Mary's actual model portrait did not become visible.");
            var texture = (RenderTexture)image.texture; Assert.That(texture.name, Does.Contain("Mary"));
            AssertPortraitPixels(texture);
            var source = UnityEngine.Object.FindObjectsByType<GospelNprCharacterView>(FindObjectsSortMode.None)
                .Single(view => view.CachedPortraitTexture == texture);
            Assert.That(source.ModelIdentity, Is.EqualTo("Mary"));
            Assert.That(source, Is.SameAs(App.OpeningStage.ModelFor("mary")), "The portrait must match the actual visible opening performer.");
            var speakerModel = source.ModelResource; Assert.That(speakerModel, Is.EqualTo("Art/GospelNpr/Characters/Mary"));
            var openingDiagnostics = new List<string>(5);
            openingDiagnostics.Add(StoryOpeningStateSnapshot("before-shot"));
            yield return Shot("mary-actual-model-dialogue");
            openingDiagnostics.Add(StoryOpeningStateSnapshot("after-shot-before-show-bible"));
            var physicalRoot = App.Player.transform; int physicalIdentity = physicalRoot.GetInstanceID();
            Vector3 physicalPose = App.Player.Position; float physicalYaw = App.Player.FacingYaw;
            App.ShowBible();
            openingDiagnostics.Add(StoryOpeningStateSnapshot("immediate-after-show-bible"));
            yield return null;
            openingDiagnostics.Add(StoryOpeningStateSnapshot("after-existing-yield-1"));
            yield return null;
            openingDiagnostics.Add(StoryOpeningStateSnapshot("after-existing-yield-2"));
            Assert.That(App.OpeningStage.IsVisible, Is.True, "Keep the actual opening behind the book and its wide-screen gutters."
                + "\n" + string.Join("\n", openingDiagnostics));
            Assert.That(App.OpeningStage.CurrentSet, Is.EqualTo("nazareth"));
            Assert.That(App.Actor.gameObject.activeInHierarchy || App.CameraRig.enabled, Is.False);
            Assert.That(physicalRoot.gameObject.activeInHierarchy && App.Player.Controller.enabled, Is.True);
            Assert.That(Vector3.Distance(App.Player.Position, physicalPose), Is.LessThan(.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, physicalYaw)), Is.LessThan(.01f));
            Assert.That(source.PortraitCamera.enabled, Is.False, "A hidden speaker portrait must stop its extra render camera.");
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(beat)); Assert.That(App.Chronicle.DiscoveredCount, Is.Zero);
            yield return Click("여정으로");
            var resumed = App.Ui.CanvasView.GetComponentsInChildren<RawImage>()
                .Single(item => item.gameObject.activeInHierarchy && item.gameObject.name == "Actual story speaker model portrait");
            yield return SmokeUi.WaitUntil(() => resumed.texture == texture && resumed.enabled, 3, "The same story line did not reuse its actual portrait.");
            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(beat)); Assert.That(VisibleText(), Does.Contain(line));
            Assert.That(App.OpeningStage.IsVisible, Is.True); Assert.That(App.OpeningStage.CurrentSet, Is.EqualTo("nazareth"));
            Assert.That(App.Player.transform.GetInstanceID(), Is.EqualTo(physicalIdentity));
            Assert.That(physicalRoot.gameObject.activeInHierarchy && App.Player.Controller.enabled, Is.True);
            Assert.That(Vector3.Distance(App.Player.Position, physicalPose), Is.LessThan(.01f), "A visual opening/book round trip must not relocate the physical traveler.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, physicalYaw)), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator AllTwentyThreeEventsAndNineteenPassagesSurviveActualNprTravelAndReload()
        {
            App.GetComponent<GospelStorybookAudio>().NotifyUserGesture();
            Assert.That(RenderSettings.sun, Is.Not.Null);
            float fieldFov = App.SceneCamera.fieldOfView, fieldSunIntensity = RenderSettings.sun.intensity;
            Color fieldSunColor = RenderSettings.sun.color;
            Quaternion fieldSunRotation = RenderSettings.sun.transform.rotation;
            var fieldAmbientMode = RenderSettings.ambientMode;
            var fieldAmbient = new[] { RenderSettings.ambientLight, RenderSettings.ambientSkyColor,
                RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor };
            Action assertFieldPresentation = () =>
            {
                Assert.That(App.OpeningStage.IsVisible, Is.False);
                Assert.That(App.Actor.gameObject.activeInHierarchy && App.CameraRig.enabled, Is.True);
                Assert.That(App.SceneCamera.fieldOfView, Is.EqualTo(fieldFov).Within(.0001f));
                Assert.That(RenderSettings.sun, Is.Not.Null);
                Assert.That(RenderSettings.sun.intensity, Is.EqualTo(fieldSunIntensity).Within(.0001f));
                Assert.That(RenderSettings.sun.color, Is.EqualTo(fieldSunColor));
                Assert.That(Quaternion.Angle(RenderSettings.sun.transform.rotation, fieldSunRotation), Is.LessThan(.01f));
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(fieldAmbientMode));
                Assert.That(new[] { RenderSettings.ambientLight, RenderSettings.ambientSkyColor,
                    RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor }, Is.EqualTo(fieldAmbient));
            };
            yield return Click("새 여정 시작");
            var visited = new HashSet<string>(); var acquired = new HashSet<string>();
            var capturedOpeningSets = new HashSet<string>(); bool observedNight = false;
            int openingTurns = 0, acceptedPeople = 0, safety = 0; bool reloadedEncounter = false, equipped = false;
            while (!App.Chronicle.IsComplete && safety++ < 220)
            {
                if (App.Ui.CurrentView == "chapter")
                {
                    assertFieldPresentation();
                    Assert.That(openingTurns, Is.EqualTo(51)); Assert.That(observedNight, Is.True);
                    yield return Click("이야기 속으로"); continue;
                }
                var quest = App.Chronicle.CurrentQuest; visited.Add(quest.Id);
                if (quest.IsPrologue)
                {
                    Assert.That(App.OpeningStage.IsVisible, Is.True);
                    Assert.That(App.Actor.gameObject.activeInHierarchy || App.CameraRig.enabled, Is.False);
                    Assert.That(App.People.Count, Is.Zero);
                    Assert.That(App.OpeningStage.CurrentSet, Is.Not.Empty);
                    if (capturedOpeningSets.Add(App.OpeningStage.CurrentSet))
                    {
                        yield return Shot("opening-set-" + App.OpeningStage.CurrentSet);
                        Debug.Log("FIRSTGAME_NPR_CAPTURED_OPENING_SET id=" + App.OpeningStage.CurrentSet);
                    }
                    if (quest.Id == "birth-night-shepherds")
                    {
                        Assert.That(RenderSettings.sun.intensity, Is.LessThan(fieldSunIntensity), "Actually traverse a night scene before checking restored daylight.");
                        observedNight = true;
                    }
                }
                else assertFieldPresentation();
                if (App.Chronicle.Stage != GospelQuestStage.Reading)
                {
                    Assert.That(quest.IsPrologue, Is.False);
                    if (!equipped)
                    {
                        App.ShowInventory(); yield return null; yield return Click("등잔 장착");
                        Assert.That(App.Actor.LampEquipped, Is.True); yield return Click("여정으로"); equipped = true;
                    }
                    yield return ReachCurrentPerson(); yield return AcceptCurrentPerson(); acceptedPeople++;
                    Assert.That(App.People.CurrentPerson.RootTransform.GetComponentInChildren<GospelPilgrimageActor>().HasSkinnedModel, Is.True);
                    if (!reloadedEncounter)
                    {
                        reloadedEncounter = true;
                        var position = App.Player.Position; float yaw = App.Player.FacingYaw;
                        var person = App.People.CurrentPerson; Vector3 npcPosition = person.Position; float npcYaw = person.FacingYaw;
                        string personId = person.Id, beat = App.Chronicle.StoryVoiceKey;
                        Assert.That(App.Save(false), Is.True); string slot = App.ActiveSaveSlot;
                        App.ShowBible(); yield return null; yield return Click("여정으로");
                        Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(beat));
                        yield return SmokeUi.LoadBoot(); yield return Click("이어하기");
                        Assert.That(App.ActiveSaveSlot, Is.EqualTo(slot)); Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(beat));
                        Assert.That(Vector3.Distance(App.Player.Position, position), Is.LessThan(.12f));
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, yaw)), Is.LessThan(.01f));
                        Assert.That(App.People.CurrentPerson.Id, Is.EqualTo(personId));
                        Assert.That(Vector3.Distance(App.People.CurrentPerson.Position, npcPosition), Is.LessThan(.035f));
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.People.CurrentPerson.FacingYaw, npcYaw)), Is.LessThan(.2f));
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Actor.FacingYaw, yaw)), Is.LessThan(1f));
                        yield return Shot("same-accepted-3d-encounter-continued");
                        // A separate isolated older-format Reading fixture has
                        // no people sidecar and a deliberately different saved
                        // heading. Normal continue/book return must preserve
                        // those bytes and must not turn the saved traveler.
                        var legacy = App.Journey.CaptureSave();
                        legacy.playerY = App.World.HeightAt(legacy.playerX, legacy.playerZ);
                        legacy.playerYaw = Mathf.Repeat(yaw + 137, 360);
                        const string legacySlot = "npr-previous-reading";
                        Assert.That(PersistentSaves.TrySave(legacySlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                            GospelJourney.ContentVersion, GospelSaveCodec.Encode(legacy), out var legacyError), Is.True, legacyError);
                        string legacyOriginal = backend.Read(legacySlot);
                        PlayerPrefs.SetString("GOSPEL.activeSlot", legacySlot); PlayerPrefs.Save();
                        for (int reload = 0; reload < 2; reload++)
                        {
                            yield return SmokeUi.LoadBoot(); yield return Click("이어하기");
                            Assert.That(App.ActiveSaveSlot, Is.EqualTo(legacySlot)); Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(beat));
                            Assert.That(App.Journey.PlayerYaw, Is.EqualTo(legacy.playerYaw));
                            Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, legacy.playerYaw)), Is.LessThan(.01f));
                            Assert.That(Planar(App.Player.Position, new Vector3(legacy.playerX, legacy.playerY, legacy.playerZ)), Is.LessThan(.01f));
                            Assert.That(backend.Read(legacySlot), Is.EqualTo(legacyOriginal));
                            App.ShowBible(); yield return null; yield return Click("여정으로");
                            Assert.That(App.Chronicle.StoryVoiceKey, Is.EqualTo(beat));
                            Assert.That(Mathf.Abs(Mathf.DeltaAngle(App.Player.FacingYaw, legacy.playerYaw)), Is.LessThan(.01f));
                            Assert.That(backend.Read(legacySlot), Is.EqualTo(legacyOriginal));
                        }
                        yield return Shot("legacy-reading-original-heading-preserved");
                    }
                    continue;
                }
                string text = App.Chronicle.CurrentStoryBeat.Text, speaker = App.Chronicle.CurrentStoryBeat.Speaker;
                Assert.That(VisibleText(), Does.Contain(text)); Assert.That(VisibleText(), Does.Contain(speaker));
                int index = App.Chronicle.QuestIndex, count = App.Chronicle.DiscoveredCount;
                bool last = App.Chronicle.StoryBeatIndex + 1 == App.Chronicle.StoryBeatCount;
                bool earns = quest.Reading != null;
                if (quest.IsPrologue) openingTurns++;
                yield return Click("이야기 계속");
                Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(count + (last && earns ? 1 : 0)), "Only the last actual detail may award its retained Scripture.");
                if (last)
                {
                    Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(index + 1));
                    if (earns)
                    {
                        Assert.That(App.LastAcquiredReading, Is.Not.Null); Assert.That(acquired.Add(App.LastAcquiredReading.Id), Is.True);
                        Assert.That(App.Chronicle.HasReading(App.LastAcquiredReading.Id), Is.True);
                    }
                }
                if (App.Ui.CurrentView == "scripture-acquired")
                {
                    Assert.That(SmokeUi.HasText(App.LastAcquiredReading.Text), Is.True);
                    yield return Click("이야기 계속");
                }
            }
            Assert.That(safety, Is.LessThan(220)); Assert.That(App.Chronicle.IsComplete, Is.True);
            Assert.That(visited.Count, Is.EqualTo(23)); Assert.That(openingTurns, Is.EqualTo(51)); Assert.That(acceptedPeople, Is.EqualTo(12));
            Assert.That(capturedOpeningSets.Count, Is.GreaterThan(1)); Assert.That(observedNight, Is.True);
            Debug.Log("FIRSTGAME_NPR_OBSERVED_OPENING_SETS count=" + capturedOpeningSets.Count
                + " ids=" + string.Join(",", capturedOpeningSets.OrderBy(key => key, StringComparer.Ordinal)));
            assertFieldPresentation();
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(19));
            Assert.That(acquired.SetEquals(GospelChronicleCatalog.Readings.Select(reading => reading.Id)), Is.True);
            Assert.That(App.Actor.IsAdult, Is.True); Assert.That(App.Journey.LampEquipped, Is.True); AssertLamp(App.Actor);
            Assert.That(App.Save(false), Is.True); string finalSlot = App.ActiveSaveSlot; Vector3 finalPose = App.Player.Position;
            yield return Shot("all-events-adult-npr-field");
            yield return SmokeUi.LoadBoot(); yield return Click("이어하기");
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(finalSlot)); Assert.That(App.Chronicle.IsComplete, Is.True);
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(19)); Assert.That(Planar(App.Player.Position, finalPose), Is.LessThan(.12f));
            Assert.That(App.Actor.HasSkinnedModel && App.Actor.IsAdult && App.Actor.LampEquipped, Is.True);
            App.ShowBible(); yield return null;
            Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"));
            Assert.That(GameObject.Find("Left Scripture leaf"), Is.Not.Null); Assert.That(GameObject.Find("Right Scripture leaf"), Is.Not.Null);
            yield return Shot("retained-nineteen-passage-bible");
        }
    }
}
