using System;
using System.Collections;
using System.IO;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PathOfDawn.SmokeTests
{
    /// <summary>The map follows actual travel and actors without changing the story.</summary>
    public sealed class GospelLivingUiSmokeTests
    {
        IDisposable storage;
        MemorySaveBackend backend;
        Keyboard keyboard;
        InputSettings previousInputs, testInputs;
        bool hadSlot;
        string oldSlot, attempt;
        GospelPilgrimageApp App => UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageApp>();

        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        IEnumerator Click(string label) { SmokeUi.Click(label); yield return null; yield return null; }
        IEnumerator Shot(string name, GospelPilgrimageMapGraphic map) => SmokeUi.Capture("PathOfDawn", "living-" + attempt + "-" + name,
            1280, 720, validateLayout: () => AssertRenderedMapPixels(name, map), settleSeconds: .25f);

        void AssertRenderedMapPixels(string name, GospelPilgrimageMapGraphic map)
        {
            // Inspect the actual immutable PNG produced by SmokeUi's native GPU
            // capture. Data-only marker assertions cannot detect a black map.
            Assert.That(map.GetComponents<Graphic>().Length, Is.EqualTo(1), "A map owns one Graphic and one CanvasRenderer.");
            Assert.That(map.GetComponents<CanvasRenderer>().Length, Is.EqualTo(1));
            string root = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root))
            {
                string validation = Environment.GetEnvironmentVariable("FIRSTGAME_VALIDATION_ROOT");
                root = string.IsNullOrEmpty(validation)
                    ? Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Artifacts/Screenshots"))
                    : Path.Combine(validation, "Screenshots");
            }
            string path = Path.Combine(root, "PathOfDawn", "living-" + attempt + "-" + name + ".png");
            Assert.That(File.Exists(path), Is.True, "The pixel check must read the just-captured original.");
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(image.LoadImage(File.ReadAllBytes(path)), Is.True);
                var plot = map.PlotBounds;
                var camera = map.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : map.canvas.worldCamera;
                Vector2 lower = RectTransformUtility.WorldToScreenPoint(camera,
                    map.rectTransform.TransformPoint(new Vector3(plot.xMin, plot.yMin, 0)));
                Vector2 upper = RectTransformUtility.WorldToScreenPoint(camera,
                    map.rectTransform.TransformPoint(new Vector3(plot.xMax, plot.yMax, 0)));
                int minX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(lower.x, upper.x)) + 1, 0, image.width - 1);
                int minY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(lower.y, upper.y)) + 1, 0, image.height - 1);
                int maxX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(lower.x, upper.x)) - 1, 0, image.width - 1);
                int maxY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(lower.y, upper.y)) - 1, 0, image.height - 1);
                Assert.That(maxX - minX, Is.GreaterThan(40)); Assert.That(maxY - minY, Is.GreaterThan(30));
                int samples = 0, visible = 0, people = 0, pathLight = 0, traveler = 0;
                double luminance = 0;
                var pixels = image.GetPixels32();
                for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
                {
                    var color = pixels[y * image.width + x];
                    float r = color.r / 255f, g = color.g / 255f, b = color.b / 255f;
                    samples++; luminance += r * .2126f + g * .7152f + b * .0722f;
                    if (Mathf.Max(r, Mathf.Max(g, b)) > .10f) visible++;
                    if (g > .35f && b > .35f && g > r * 1.2f && b > r * 1.2f) people++;
                    if (r > .38f && g > .25f && b < r * .85f) pathLight++;
                    if (Mathf.Min(r, Mathf.Min(g, b)) > .60f && Mathf.Max(r, Mathf.Max(g, b)) - Mathf.Min(r, Mathf.Min(g, b)) < .22f) traveler++;
                }
                Debug.Log("Actual captured map " + name + ": pixels=" + samples + " terrainVisible=" + visible +
                    " meanLuminance=" + (luminance / samples).ToString("0.000") + " people=" + people + " path=" + pathLight + " traveler=" + traveler);
                Assert.That(luminance / samples, Is.GreaterThan(.06), "The actual map terrain must not be black.");
                Assert.That(visible, Is.GreaterThan(samples / 3), "Visible terrain must cover the map, not isolated data-only markers.");
                Assert.That(people, Is.GreaterThan(8), "The GPU capture must contain the blue people markers.");
                Assert.That(pathLight, Is.GreaterThan(8), "The GPU capture must contain the golden authored trail/meeting marker.");
                Assert.That(traveler, Is.GreaterThan(3), "The GPU capture must contain the ivory player arrow.");
            }
            finally { UnityEngine.Object.Destroy(image); }
        }

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend = new MemorySaveBackend(); backend.WriteAtomic("living-map-preserved-player", "original player bytes");
            storage = PersistentSaves.UseBackendForTesting(backend);
            hadSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot"); oldSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            previousInputs = InputSystem.settings; testInputs = UnityEngine.Object.Instantiate(previousInputs);
            testInputs.hideFlags = HideFlags.HideAndDontSave;
            testInputs.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputs.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings = testInputs;
            keyboard = InputSystem.AddDevice<Keyboard>(); yield return SmokeUi.LoadBoot();
            yield return new WaitForSecondsRealtime(.25f);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Keys(); yield return SmokeUi.UnloadBoot();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousInputs != null) InputSystem.settings = previousInputs;
            storage?.Dispose();
            if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", oldSlot); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (testInputs != null && InputSystem.settings != testInputs) UnityEngine.Object.DestroyImmediate(testInputs);
        }

        [UnityTest] public IEnumerator LiveMinimapTracksRealTravelAndOpensTheSameWorldMapWithoutAdvancingStory()
        {
            yield return Click("새 여정 시작");
            int openingLines = 0;
            while (App.Chronicle.CurrentQuest.IsPrologue && openingLines++ < 60) yield return Click("이야기 계속");
            Assert.That(openingLines, Is.EqualTo(51), "Reach the world through its unchanged real story; never inject progress.");
            yield return Click("이야기 속으로");
            yield return new WaitForSecondsRealtime(.25f);
            var map = App.Ui.Minimap;
            Assert.That(map, Is.Not.Null); Assert.That(map.HasLiveWorld, Is.True);
            Assert.That(map.WorldBounds, Is.EqualTo(App.World.Bounds)); Assert.That(map.IsNorthUp, Is.True);
            Assert.That(map.PlotBounds.width / map.PlotBounds.height,
                Is.EqualTo(App.World.Bounds.width / App.World.Bounds.height).Within(.001f),
                "Map relief, trail and actor headings must share an undistorted north-up projection.");
            Assert.That(map.PersonMarkerCount, Is.EqualTo(App.People.Markers.Count));
            Assert.That(map.PersonMarkerCount, Is.GreaterThan(1)); Assert.That(map.AmbientMarkerCount, Is.GreaterThan(0));
            Vector2 beforeMap = map.PlayerMapPosition; Vector3 before = App.Player.Position;
            Keys(Key.D, Key.W); yield return new WaitForSecondsRealtime(.28f); Keys();
            yield return new WaitForSecondsRealtime(.18f);
            Vector3 after = App.Player.Position; Vector2 afterMap = map.PlayerMapPosition;
            Assert.That(Vector2.Distance(new Vector2(before.x, before.z), new Vector2(after.x, after.z)), Is.GreaterThan(.20f));
            Assert.That(afterMap.x, Is.GreaterThan(beforeMap.x)); Assert.That(afterMap.y, Is.GreaterThan(beforeMap.y));
            var bounds = App.World.Bounds;
            Assert.That(afterMap.x, Is.EqualTo((after.x - bounds.xMin) / bounds.width).Within(.0001f));
            Assert.That(afterMap.y, Is.EqualTo((after.z - bounds.yMin) / bounds.height).Within(.0001f));
            var target = App.ObjectivePosition;
            Assert.That(map.ObjectiveMapPosition.x, Is.EqualTo((target.x - bounds.xMin) / bounds.width).Within(.0001f));
            Assert.That(map.ObjectiveMapPosition.y, Is.EqualTo((target.z - bounds.yMin) / bounds.height).Within(.0001f));
            yield return Shot("minimap-live", map);

            int quest = App.Chronicle.QuestIndex, scripture = App.Chronicle.DiscoveredCount;
            var world = App.World; int people = App.People.Markers.Count;
            yield return Click("지도 펼치기");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("map")); Assert.That(App.IsModal, Is.True);
            yield return null;
            Assert.That(App.Player.Enabled, Is.False);
            var expanded = UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageMapGraphic>();
            Assert.That(expanded, Is.Not.Null); Assert.That(expanded.HasLiveWorld, Is.True);
            Assert.That(expanded.WorldBounds, Is.EqualTo(bounds));
            Assert.That(expanded.PlotBounds.width / expanded.PlotBounds.height,
                Is.EqualTo(bounds.width / bounds.height).Within(.001f));
            Assert.That(expanded.PlayerMapPosition, Is.EqualTo(afterMap));
            Assert.That(expanded.PersonMarkerCount, Is.EqualTo(people));
            Vector3 paused = App.Player.Position;
            Keys(Key.D); yield return new WaitForSecondsRealtime(.25f); Keys(); yield return null;
            Assert.That(Vector2.Distance(new Vector2(paused.x, paused.z), new Vector2(App.Player.Position.x, App.Player.Position.z)), Is.LessThan(.01f));
            Assert.That(App.World, Is.SameAs(world)); Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(quest));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(scripture));
            yield return Shot("field-map-expanded", expanded);
            yield return Click("여정으로"); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(App.Ui.CurrentView, Is.EqualTo("journey")); Assert.That(App.Player.Enabled, Is.True);
            Assert.That(App.Ui.Minimap, Is.Not.Null); Assert.That(App.Ui.Minimap.PersonMarkerCount, Is.EqualTo(people));
            Assert.That(App.Ui.Minimap.PlayerMapPosition, Is.EqualTo(afterMap));
            Assert.That(App.World, Is.SameAs(world)); Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(quest));
            Assert.That(backend.Read("living-map-preserved-player"), Is.EqualTo("original player bytes"));
            yield return Shot("minimap-return", App.Ui.Minimap); SmokeUi.AssertNoMissingScripts();
        }
    }
}
