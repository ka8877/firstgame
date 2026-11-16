using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PathOfDawn.SmokeTests
{
    /// <summary>
    /// Render the five prepared actors and their same-model portraits under a
    /// controlled key light. Native images still require Root visual review;
    /// material/framing checks are not an aesthetic or device-performance grade.
    /// </summary>
    public sealed class GospelNprCharacterPolishSmokeTests
    {
        GameObject fixture;
        Material groundMaterial;
        AmbientMode ambientMode;
        Color ambientSky, ambientEquator, ambientGround;
        Light previousSun;
        string attempt;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            fixture = new GameObject("Five actual NPR character polish fixture");
            ambientMode = RenderSettings.ambientMode;
            ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor;
            previousSun = RenderSettings.sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.40f, .46f, .56f);
            RenderSettings.ambientEquatorColor = new Color(.30f, .32f, .35f);
            RenderSettings.ambientGroundColor = new Color(.24f, .22f, .20f);
            var sunObject = new GameObject("Controlled warm three-quarter key", typeof(Light));
            sunObject.transform.SetParent(fixture.transform, false);
            sunObject.transform.rotation = Quaternion.Euler(42, -35, 0);
            var sun = sunObject.GetComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.05f;
            sun.color = new Color(1, .96f, .90f); sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            var cameraObject = new GameObject("Actual actor lineup camera", typeof(Camera));
            cameraObject.transform.SetParent(fixture.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 1.45f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.75f, .80f, .83f);
            camera.nearClipPlane = .05f; camera.farClipPlane = 30;
            camera.cullingMask = ~(1 << GospelNprCharacterView.PortraitLayer);
            camera.transform.position = new Vector3(0, .85f, 8);
            camera.transform.LookAt(new Vector3(0, .85f, 0));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Controlled neutral shadow receiver";
            ground.transform.SetParent(fixture.transform, false);
            ground.transform.position = new Vector3(0, -.01f, 0);
            groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMaterial.SetColor("_BaseColor", new Color(.78f, .76f, .70f));
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (fixture != null) UnityEngine.Object.Destroy(fixture);
            if (groundMaterial != null) UnityEngine.Object.Destroy(groundMaterial);
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.sun = previousSun;
            yield return null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PreparedCharactersAndPortraitsKeepVisibleRealSkinsAndReadableFaces()
        {
            string[] identities = { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" };
            var actors = new List<GospelNprCharacterView>();
            var feet = new List<Vector3>();
            foreach (string identity in identities)
            {
                var owner = new GameObject("Actual prepared actor " + identity);
                owner.transform.SetParent(fixture.transform, false);
                owner.transform.localPosition = new Vector3((actors.Count - 2) * 1.10f, 0, 0);
                var actor = owner.AddComponent<GospelNprCharacterView>();
                actor.LoadModel(identity, GospelNprCharacterView.HeightFor(identity), Color.white);
                actor.SetMotion(Vector3.zero, true, 0); actor.Tick(0);
                actors.Add(actor); feet.Add(owner.transform.position);
            }
            yield return null; yield return null;
            foreach (var actor in actors)
            {
                Assert.That(actor.IsReady && actor.Animator.avatar.isHuman && actor.Animator.avatar.isValid, Is.True, actor.ModelIdentity);
                Assert.That(actor.SkinnedRenderers.All(renderer => renderer.enabled && !renderer.forceRenderingOff), Is.True,
                    "The clothing/skin seam must not be fixed by hiding the real renderer.");
                var materials = actor.SkinnedRenderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray();
                var skin = materials.Where(material => material.name.Contains("__WarmSkin")).ToArray();
                Assert.That(skin, Is.Not.Empty, actor.ModelIdentity);
                Assert.That(skin.All(material => Enumerable.Range(0, material.passCount)
                    .All(pass => material.GetPassName(pass).IndexOf("outline", StringComparison.OrdinalIgnoreCase) < 0)), Is.True,
                    "An inverted anatomical skin hull must not protrude through opaque linen.");
                Assert.That(materials.All(material => material.shader.isSupported && material.FindPass("FORWARD") >= 0), Is.True,
                    "Each actual material must retain a supported NPR forward pass.");
            }
            yield return Capture("character-polish-five-real-actors");

            var canvasObject = new GameObject("Five same-model portrait inspection canvas", typeof(Canvas));
            canvasObject.transform.SetParent(fixture.transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var portraitTextures = new List<RenderTexture>();
            for (int index = 0; index < actors.Count; index++)
            {
                var actor = actors[index];
                var texture = actor.GetPortraitTexture(512) as RenderTexture;
                Assert.That(texture, Is.Not.Null); Assert.That(texture.IsCreated(), Is.True);
                portraitTextures.Add(texture);
                var camera = actor.PortraitCamera;
                var portraitMeshes = camera.transform.parent.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(new HashSet<Mesh>(portraitMeshes.Select(renderer => renderer.sharedMesh))
                    .SetEquals(actor.SkinnedRenderers.Select(renderer => renderer.sharedMesh)), Is.True,
                    "Each portrait must retain its own actual world mesh, not a painted replacement.");
                Assert.That(portraitMeshes.All(renderer => renderer.gameObject.layer == GospelNprCharacterView.PortraitLayer), Is.True);
                var rig = camera.transform.parent.GetComponentInChildren<Animator>(true);
                Assert.That(rig, Is.Not.Null);
                var head = rig.GetBoneTransform(HumanBodyBones.Head);
                var leftEye = rig.GetBoneTransform(HumanBodyBones.LeftEye);
                var rightEye = rig.GetBoneTransform(HumanBodyBones.RightEye);
                Assert.That(head && leftEye && rightEye, Is.True, "Actual humanoid face landmarks are required.");
                var headPoint = camera.WorldToViewportPoint(head.position);
                var leftPoint = camera.WorldToViewportPoint(leftEye.position);
                var rightPoint = camera.WorldToViewportPoint(rightEye.position);
                Assert.That(headPoint.z, Is.GreaterThan(camera.nearClipPlane));
                Assert.That(headPoint.x, Is.InRange(.20f, .80f));
                Assert.That(headPoint.y, Is.InRange(.25f, .75f));
                Assert.That(leftPoint.x, Is.InRange(.08f, .92f)); Assert.That(rightPoint.x, Is.InRange(.08f, .92f));
                Assert.That(leftPoint.y, Is.InRange(.12f, .94f)); Assert.That(rightPoint.y, Is.InRange(.12f, .94f));
                float eyePixels = Vector2.Distance(new Vector2(leftPoint.x, leftPoint.y), new Vector2(rightPoint.x, rightPoint.y)) * texture.width;
                Assert.That(eyePixels, Is.GreaterThan(36), "The actual face must remain readable in the portrait render target.");
                var cell = new GameObject("Portrait inspection cell " + actor.ModelIdentity, typeof(RectTransform));
                cell.transform.SetParent(canvasObject.transform, false);
                var rect = cell.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(index * .20f + .01f, .20f);
                rect.anchorMax = new Vector2((index + 1) * .20f - .01f, .80f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var imageObject = new GameObject("Actual portrait " + actor.ModelIdentity, typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                imageObject.transform.SetParent(cell.transform, false);
                imageObject.GetComponent<RawImage>().texture = texture;
                var fitter = imageObject.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 1;
            }
            yield return null; yield return null;
            foreach (var texture in portraitTextures) AssertRenderedPortrait(texture);
            yield return Capture("character-polish-five-actual-portraits");
            canvasObject.SetActive(false);
            for (int index = 0; index < actors.Count; index++)
            {
                var actor = actors[index]; var camera = actor.PortraitCamera;
                var texture = portraitTextures[index];
                actor.SetPortraitVisible(false);
                Assert.That(camera.enabled, Is.False, "Hidden UI must stop its extra rendering camera.");
                Assert.That(actor.GetPortraitTexture(), Is.SameAs(texture), "Reopen must reuse the same actual portrait.");
                Assert.That(Vector3.Distance(actor.FootRoot.position, feet[index]), Is.LessThan(.00001f),
                    "Portrait framing and shader polish must never relocate a real actor.");
                actor.ReleasePortrait();
                Assert.That(texture == null || !texture.IsCreated(), Is.True);
            }
        }

        static void AssertRenderedPortrait(RenderTexture target)
        {
            var previous = RenderTexture.active;
            var resolved = RenderTexture.GetTemporary(target.width, target.height, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(target, resolved); RenderTexture.active = resolved;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                var colors = pixels.GetPixels32(); var buckets = new HashSet<int>();
                foreach (var color in colors) buckets.Add((color.r / 16 << 8) | (color.g / 16 << 4) | color.b / 16);
                Assert.That(buckets.Count, Is.GreaterThan(12), "A uniform or empty render target is not an actual model portrait.");
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(resolved);
                UnityEngine.Object.Destroy(pixels);
            }
        }

        IEnumerator Capture(string name) => SmokeUi.Capture("PathOfDawn", "npr-" + attempt + "-" + name, 1280, 720, settleSeconds: .15f);
    }
}
