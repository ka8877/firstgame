using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Data;
using FantasyCards.Presentation;
using FirstGame.Shared;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FantasyCards.SmokeTests
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
        public const string BattleSlot = "fantasy-battle-classic-v3", DeckSlot = "fantasy-deck-classic-v3", GameId = "starlight-oath";
        public static IDisposable PreserveDemoPreferences() => new DemoPreferenceScope();
        sealed class DemoPreferenceScope : IDisposable
        {
            readonly string[] stringKeys = { "FC.demo.leader", "FC.demo.deck", "FC.profile.name", "FC.profile.avatar" };
            readonly string[] intKeys = { "FC.demo.encounter", "FC.demo.won.0", "FC.demo.won.1", "FC.demo.won.2", "FC.profile.wins", "FC.profile.losses", "FC.profile.draws" };
            readonly Dictionary<string, string> strings = new Dictionary<string, string>();
            readonly Dictionary<string, int> ints = new Dictionary<string, int>();
            public DemoPreferenceScope()
            {
                foreach (string key in stringKeys) if (PlayerPrefs.HasKey(key)) strings[key] = PlayerPrefs.GetString(key);
                foreach (string key in intKeys) if (PlayerPrefs.HasKey(key)) ints[key] = PlayerPrefs.GetInt(key);
            }
            public void Dispose()
            {
                foreach (string key in stringKeys) { if (strings.TryGetValue(key, out var value)) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
                foreach (string key in intKeys) { if (ints.TryGetValue(key, out var value)) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key); }
                PlayerPrefs.Save();
            }
        }
        public static Button FindButton(string label) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.gameObject.activeInHierarchy && b.GetComponentsInChildren<TMP_Text>()
                .Any(t => t.text == label && t.GetComponentInParent<Button>() == b));
        public static bool HasText(string value) => UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Any(t => t.gameObject.activeInHierarchy && t.text.Contains(value));
        public static void Click(string label)
        {
            var button = FindButton(label);
            Assert.That(button, Is.Not.Null, "Visible UI button missing: " + label + ". Active labels: " +
                string.Join(" | ", UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                    .Where(text => text.gameObject.activeInHierarchy).Select(text => text.text).Take(30)));
            Assert.That(button.IsActive() && button.IsInteractable(), Is.True, "UI button disabled: " + label);
            // Exercise the real UI callback; pointer hit-testing is a separate manual acceptance check.
            button.onClick.Invoke();
        }
        public static IEnumerator WaitUntil(Func<bool> predicate, float seconds, string failure)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, failure);
        }
        public static IEnumerator StartDemoBattle(string leaderId = "lyria", string deckId = "forest", int encounterIndex = 0)
        {
            Click("새로운 결투");
            yield return null;
            Assert.That(VisibleNamedButtons("Leader "), Is.EqualTo(4), "New game must offer all four leaders.");
            Click(DemoCatalog.GetLeader(leaderId).name);
            yield return null;
            Click("기본 덱 선택");
            yield return null;
            Assert.That(VisibleNamedButtons("Starter deck "), Is.EqualTo(4), "New game must offer all four starter decks.");
            Click(DemoCatalog.GetDeck(deckId).name);
            yield return null;
            Click("상대 선택");
            yield return null;
            Assert.That(VisibleNamedButtons("Encounter "), Is.EqualTo(3), "The demo must offer three encounters.");
            Click(DemoCatalog.GetEncounter(encounterIndex).name);
            yield return null;
            yield return StartPreparedBattle();
        }
        public static IEnumerator StartPreparedBattle()
        {
            // Use the currently selected encounter. Result-flow tests rely on this not
            // overriding the choice that the actual next/retry callback just made.
            Click("덱을 섞고 시작");
            yield return WaitUntil(() => FindButton("이대로 시작") != null && FindButton("이대로 시작").interactable,
                8, "The visible shuffle did not reach opening-hand selection.");
            AssertVisibleKorean("시작 손패 확인");
            Click("이대로 시작");
            yield return WaitUntil(() => FindButton("저장") != null && FindButton("저장").interactable,
                8, "Opening-hand confirmation and the battle Timeline did not release input.");
            Assert.That(FindButton("이대로 시작"), Is.Null, "Opening-hand selection must close after confirmation.");
        }
        static int VisibleNamedButtons(string prefix) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Count(button => button.gameObject.activeInHierarchy && button.gameObject.name.StartsWith(prefix, StringComparison.Ordinal));
        public static BattleEngine ReadEngine()
        {
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            Assert.That(app, Is.Not.Null);
            var field = typeof(FantasyCardsApp).GetField("engine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var engine = field?.GetValue(app) as BattleEngine;
            Assert.That(engine, Is.Not.Null);
            return engine;
        }
        public static void TraceUiState(string point)
        {
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            var callback = app == null ? null : typeof(FantasyCardsApp)
                .GetField("redraw", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(app) as Action;
            string keyboards = string.Join(";", InputSystem.devices.OfType<Keyboard>().Select(keyboard =>
                "id=" + keyboard.deviceId + ",current=" + (keyboard == Keyboard.current) +
                ",updated=" + keyboard.wasUpdatedThisFrame + ",pressed=" + keyboard.escapeKey.isPressed + ",pressedThisFrame=" + keyboard.escapeKey.wasPressedThisFrame));
            TestContext.Out.WriteLine("UI_STATE " + point + " frame=" + Time.frameCount + " app=" +
                (app == null ? "none" : app.GetInstanceID().ToString()) + " apps=" +
                string.Join(",", UnityEngine.Object.FindObjectsByType<FantasyCardsApp>(FindObjectsSortMode.None).Select(value=>value.GetInstanceID())) +
                " scene=" + SceneManager.GetActiveScene().name + " inputUpdate=" + UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType +
                " redraw=" + callback?.Method.Name + " keyboards=" + keyboards);
        }
        public static BattleState ReadSavedBattle()
        {
            Assert.That(PersistentSaves.TryLoad(BattleSlot, GameId, 1, CardCatalog.Version, out var payload, out var error), Is.True, error);
            var state = JsonUtility.FromJson<BattleState>(payload);
            BattleEngine.Validate(state);
            return state;
        }
        public static IEnumerator StartPlayableBattle(bool requireUnit = false)
        {
            // A deterministic *legal* opening isolates persistence/layout checks
            // from the deliberately unguaranteed classic opening hand. The
            // separate selection/shuffle test exercises the public New flow.
            var rules = Resources.Load<RuleSetAsset>("Data/Rules").CreateRuntimeRules();
            uint selected = 0;
            for (uint seed = 1; seed <= 512 && selected == 0; seed++)
            {
                var candidate = new BattleEngine(seed, rules, playerDeck: CardCatalog.DefaultDeck());
                Assert.That(candidate.ConfirmOpeningHand(0, out var reason), Is.True, reason);
                foreach (var terrain in candidate.State.players[0].hand.Where(card => CardCatalog.Get(card.definitionId).kind == CardKind.Terrain))
                {
                    var trial = new BattleEngine(candidate.State.Copy());
                    if (!trial.Play(0, terrain.instanceId, -1, out _)) continue;
                    if (!requireUnit || trial.State.players[0].hand.Any(card => CardCatalog.Get(card.definitionId).kind == CardKind.Unit && trial.CanPlay(0, card.instanceId, 0, out _)))
                    { selected = seed; break; }
                }
            }
            Assert.That(selected, Is.Not.Zero, "Find an ordinary shuffled opening with a legal terrain" + (requireUnit ? " and affordable unit." : "."));
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            typeof(FantasyCardsApp).GetMethod("StartBattle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(app, new object[] { selected });
            yield return WaitUntil(() => FindButton("저장") != null && FindButton("저장").interactable, 8, "The real battle entry must release its Timeline lock.");
            Assert.That(ReadEngine().State.randomState, Is.EqualTo(ReadSavedBattle().randomState));
            TestContext.Out.WriteLine("CLASSIC_LEGAL_OPENING seed=" + selected + " requireUnit=" + requireUnit);
        }
        public static CardInstance TerrainForUnit()
        {
            var engine = ReadEngine();
            foreach (var card in engine.State.players[0].hand.Where(card => CardCatalog.Get(card.definitionId).kind == CardKind.Terrain))
            {
                var trial = new BattleEngine(engine.State.Copy());
                if (trial.Play(0, card.instanceId, -1, out _) && trial.State.players[0].hand.Any(unit =>
                    CardCatalog.Get(unit.definitionId).kind == CardKind.Unit && trial.CanPlay(0, unit.instanceId, 0, out _))) return card;
            }
            Assert.Fail("The selected legal opening must support a terrain and unit deployment."); return null;
        }
        public static IEnumerator LoadBoot()
        {
            // Explicitly unload first so continue tests always deserialize into a fresh app instance.
            var existing = SceneManager.GetSceneByPath(BootPath);
            if (existing.IsValid() && existing.isLoaded) yield return UnloadBoot();
            Assert.That(Application.CanStreamedLevelBeLoaded(BootPath), Is.True, "Boot must be enabled in the actual build scene list.");
            var operation = SceneManager.LoadSceneAsync(BootPath, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, "Generate the real Boot scene with ProjectSetup.Ensure before testing.");
            // Establish native completion before examining scene ownership and object identity
            // in these nested setup/test helpers.
            yield return WaitUntil(() => operation.isDone, 15, "Boot scene load did not finish.");
            var loaded = SceneManager.GetSceneByPath(BootPath);
            Assert.That(loaded.IsValid() && loaded.isLoaded, Is.True, "Boot must actually finish loading.");
            yield return null; // Entry component Start creates the actual canvas, world and title.
            yield return null;
            Assert.That(Application.isPlaying, Is.True, "Smoke flow must execute in actual PlayMode.");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BootPath));
        }
        public static IEnumerator UnloadBoot()
        {
            var boot = SceneManager.GetSceneByPath(BootPath);
            if (boot.IsValid() && boot.isLoaded)
            {
                var cleanup = SceneManager.CreateScene("Smoke Test Cleanup");
                SceneManager.SetActiveScene(cleanup);
                var operation = SceneManager.UnloadSceneAsync(boot);
                Assert.That(operation, Is.Not.Null, "Boot unload must start.");
                yield return WaitUntil(() => operation.isDone, 15, "Boot scene unload did not finish.");
                Assert.That(SceneManager.GetSceneByPath(BootPath).isLoaded, Is.False, "The previous Boot must be completely unloaded.");
                yield return null;
            }
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
        public static IEnumerator Capture(string game, string name, int width = 1920, int height = 1080, Action inspect = null, Action beforeCapture = null)
        {
            yield return new WaitForSecondsRealtime(.35f); // Let authored panel entrance/hover easing settle.
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
                yield return null; // Allow CanvasScaler, responsive UI, and Cinemachine to observe capture dimensions.
                yield return null;
                yield return new WaitForSecondsRealtime(.35f); // A responsive redraw can start its own card entrance animation.
                Canvas.ForceUpdateCanvases();
                if (beforeCapture != null) beforeCapture();
                else
                {
                    // Build labels at the actual capture viewport, as a player
                    // launched at these dimensions does. A temporary target can
                    // otherwise retain the Editor GameView's mobile font floor.
                    var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
                    var redraw = app == null ? null : typeof(FantasyCardsApp).GetField("redraw",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(app) as Action;
                    redraw?.Invoke();
                }
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
                // Preserve the actual failing image before a layout assertion throws.
                inspect?.Invoke();
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
