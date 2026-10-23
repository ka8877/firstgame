using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PathOfDawn.SmokeTests
{
    internal sealed class MemorySaveBackend : ISaveBackend
    {
        readonly Dictionary<string, string> slots = new Dictionary<string, string>();
        public bool Exists(string slot) => slots.ContainsKey(slot);
        public string Read(string slot) => slots[slot];
        public void WriteAtomic(string slot, string contents) => slots[slot] = contents;
    }

    internal static class SmokeUi
    {
        public const string BootPath = "Assets/Scenes/Boot.unity";
        public static Button FindButton(string label) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.gameObject.activeInHierarchy && b.GetComponentsInChildren<TMP_Text>()
                .Any(t => t.text == label));
        public static bool HasText(string value) => UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Any(t => t.gameObject.activeInHierarchy && t.text.Contains(value));
        public static void Click(string label)
        {
            var button = FindButton(label);
            Assert.That(button, Is.Not.Null, "Visible UI button missing: " + label + " | " + VisibleUi());
            Assert.That(button.IsActive() && button.IsInteractable(), Is.True, "UI button disabled: " + label);
            // Exercise the real UI callback; pointer hit-testing is a separate manual acceptance check.
            button.onClick.Invoke();
        }
        public static string VisibleUi()
        {
            var labels = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(b => b.gameObject.activeInHierarchy)
                .SelectMany(b => b.GetComponentsInChildren<TMP_Text>()).Select(t => t.text).Distinct().Take(20);
            var texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Where(t => t.gameObject.activeInHierarchy).Select(t => t.text).Distinct().Take(30);
            return "Buttons: " + string.Join(" / ", labels) + " | Text: " + string.Join(" / ", texts);
        }
        public static IEnumerator WaitUntil(Func<bool> predicate, float seconds, string failure, Func<string> diagnostics = null)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, failure + (diagnostics == null ? "" : " | " + diagnostics()));
        }
        public static IEnumerator LoadBoot()
        {
            Assert.That(Application.isPlaying, Is.True, "Smoke flow must execute in actual PlayMode.");
            Assert.That(Application.CanStreamedLevelBeLoaded(BootPath), Is.True,
                "ProjectSetup.Ensure must include Boot in the enabled build scene list.");
            var existing = SceneManager.GetSceneByPath(BootPath);
            if (existing.IsValid() && existing.isLoaded) yield return UnloadBoot();
            // Use the same runtime scene-loading API as a player. The Editor API can retain
            // old instances and does not guarantee runtime active-scene ownership.
            var operation = SceneManager.LoadSceneAsync(BootPath, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, "The runtime could not start loading Boot.");
            yield return WaitUntil(() => operation.isDone, 20, "Runtime Boot scene load did not complete.");
            yield return null;
            yield return null; // Start creates the actual canvas, world and title.
            var loaded = SceneManager.GetSceneByPath(BootPath);
            Assert.That(loaded.IsValid() && loaded.isLoaded, Is.True, "Boot did not finish loading.");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BootPath));
        }
        public static IEnumerator UnloadBoot()
        {
            var boot = SceneManager.GetSceneByPath(BootPath);
            if (boot.IsValid() && boot.isLoaded)
            {
                var cleanup = SceneManager.GetSceneByName("Smoke Test Cleanup");
                if (!cleanup.IsValid() || !cleanup.isLoaded) cleanup = SceneManager.CreateScene("Smoke Test Cleanup");
                SceneManager.SetActiveScene(cleanup);
                var operation = SceneManager.UnloadSceneAsync(boot);
                Assert.That(operation, Is.Not.Null, "The runtime could not start unloading Boot.");
                yield return WaitUntil(() => operation.isDone, 10, "Runtime Boot unload did not complete.");
                yield return null;
                var previous = SceneManager.GetSceneByPath(BootPath);
                Assert.That(!previous.IsValid() || !previous.isLoaded, Is.True, "Previous Boot is still loaded.");
            }
        }
        public static IEnumerator WaitForVisibleKorean(string exactText)
        {
            yield return WaitUntil(() =>
            {
                Canvas.ForceUpdateCanvases();
                var label = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.gameObject.activeInHierarchy && t.text == exactText);
                if (label == null || label.font == null) return false;
                label.ForceMeshUpdate();
                return label.textInfo.characterCount > 0 &&
                    label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c => c.isVisible);
            }, 3, "New Korean UI label did not initialize visible TMP geometry: " + exactText, VisibleUi);
            AssertVisibleKorean(exactText);
        }
        public static void AssertVisibleKorean(string exactText)
        {
            Canvas.ForceUpdateCanvases();
            var text = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.gameObject.activeInHierarchy && t.text == exactText);
            Assert.That(text, Is.Not.Null, "Expected Korean label: " + exactText);
            Assert.That(text.font, Is.Not.Null, "Korean font asset is missing.");
            foreach (var character in exactText.Where(c => c >= '\uAC00' && c <= '\uD7A3').Distinct())
                Assert.That(text.font.HasCharacter(character, true, true), Is.True, "Missing Hangul glyph: " + character);
            text.ForceMeshUpdate();
            Assert.That(text.textInfo.characterCount, Is.GreaterThan(0), "TMP did not build text geometry.");
            Assert.That(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c => c.isVisible), Is.True);
        }
        public static IEnumerator Capture(string game, string name, int width = 1920, int height = 1080, Action validateLayout = null)
        {
            // Native URP camera render. Batch mode does not service WaitForEndOfFrame reliably.
            // Overlay canvases temporarily use the same camera so real uGUI/TMP is in the PNG.
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "Cannot capture without the actual scene camera.");
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var oldCameras = canvases.Select(c => c.worldCamera).ToArray();
            var oldDistances = canvases.Select(c => c.planeDistance).ToArray();
            var oldTarget = camera.targetTexture;
            var oldAspect = camera.aspect;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D pixels = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                foreach (var canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + .001f;
                }
                yield return null; // Allow CanvasScaler and Cinemachine to observe capture dimensions.
                Canvas.ForceUpdateCanvases();
                var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target };
                Assert.That(UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request), Is.True,
                    "The active render pipeline does not support native StandardRequest capture.");
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                var directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                    "../../../Artifacts/Screenshots", game));
                System.IO.Directory.CreateDirectory(directory);
                var path = System.IO.Path.Combine(directory, name + ".png");
                var png = pixels.EncodeToPNG();
                Assert.That(png.Length, Is.GreaterThan(10000), "Capture contains unexpectedly little image data.");
                System.IO.File.WriteAllBytes(path, png);
                System.IO.File.WriteAllText(path + ".txt", "Native Unity URP scene capture\n" +
                    "Unity " + Application.unityVersion + "\nResolution " + width + "x" + height +
                    "\nScene " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path +
                    "\nGraphics " + SystemInfo.graphicsDeviceName + "\nOverlay UI included through temporary camera canvas mode.\n");
                validateLayout?.Invoke();
            }
            finally
            {
                RenderTexture.active = oldActive;
                camera.targetTexture = oldTarget;
                camera.aspect = oldAspect;
                for (var i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] == null) continue;
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    canvases[i].worldCamera = oldCameras[i];
                    canvases[i].planeDistance = oldDistances[i];
                }
                target.Release(); UnityEngine.Object.Destroy(target);
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
                Canvas.ForceUpdateCanvases();
            }
            yield return null;
        }
        public static void AssertNoMissingScripts()
        {
            var scene = SceneManager.GetSceneByPath(BootPath);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(node.GetComponents<Component>().All(component => component != null), Is.True,
                        "Missing script on scene object " + node.name);
        }
    }
}
