#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Firstgame.GospelNpr.Editor;
using PathOfDawn.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Firstgame.GospelVisualRebuild.Editor
{
    /// <summary>Direct, bounded Editor PREVIEW of actual Boot/App/UI. Never a release admission.</summary>
    [InitializeOnLoad]
    public static class GospelHeroVisualPreview
    {
        const string Pending = "FirstGame.GospelHero20261119.Preview.Pending";
        const string ScenesKey = "FirstGame.GospelHero20261119.Preview.PreviousScenes";
        const string ConfigSnapshotKey = "FirstGame.GospelHero20261119.Preview.ConfigSnapshot";
        const string DeferredExitCleanupKey = "FirstGame.GospelHero20261119.Preview.DeferredExitCleanup";
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static object waiting;
        static GospelHeroPreviewSupport.Config config;
        static GospelHeroPreviewSession session;
        static Dictionary<string, string> sources;
        static Index report;
        static ExitCleanup exitCleanupRecord;
        static bool running, cleaning;
        static double deadline;
        static GospelPilgrimageApp App => Object.FindFirstObjectByType<GospelPilgrimageApp>();
        [Serializable] sealed class PreviousScene { public string path; public bool loaded, active; }
        [Serializable] sealed class PreviousScenes { public PreviousScene[] scenes; }
        [Serializable] sealed class Frame
        {
            public string file, sha256, scope, resource, uiView, quest, stage;
            public int width, height;
            public Vector3 player, camera;
            public float playerYaw, orbitYaw, orbitPitch;
        }
        [Serializable] sealed class Index
        {
            public string status = "running-preview-only-not-approved", attempt, unityVersion, graphics, error;
            public bool sequenceReachedEnd, originalSourcesUnchanged, preferencesRestored;
            public bool visualApproved = false, releaseApproved = false;
            public string limitations = "Actual Boot/App and UI callbacks; native URP camera captures temporarily include existing overlay canvases through camera mode. No pointer hit-testing, Web, mobile, full-story, art acceptance or release pass is claimed. Face backend shot is the actual rigged portrait clone, not an illustration.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] sealed class ExitCleanup
        {
            public string phase = "actual-EnteredEditMode-cleanup", error;
            public bool deferredCleanupRequested, managedSessionDisposed, preferenceCheckpointRecovered, preferencesRestored;
            public bool visualApproved = false, releaseApproved = false;
        }
        static GospelHeroVisualPreview() { EditorApplication.update += Tick; }

        // Do not pass -quit: this method starts a bounded asynchronous PlayMode sequence.
        public static void PreviewFromEnvironment()
        {
            GospelHeroPreviewSupport.Need(!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode
                && !BuildPipeline.isBuildingPlayer && !SessionState.GetBool(Pending, false), "Idle Gospel Editor required.");
            config = GospelHeroPreviewSupport.ReadConfig();
            exitCleanupRecord = null;
            SessionState.SetString(ConfigSnapshotKey, JsonUtility.ToJson(config));
            SessionState.SetBool(DeferredExitCleanupKey, false);
            GospelHeroPreviewSupport.Need(!Directory.Exists(config.outputDirectory) && !File.Exists(config.outputDirectory), "Use a NEW preview output; preserve partial attempts.");
            for (int i = 0; i < SceneManager.sceneCount; i++) GospelHeroPreviewSupport.Need(!SceneManager.GetSceneAt(i).isDirty, "Preserve dirty Editor scenes.");
            GospelHeroPreviewSupport.ConfigureEnvironmentImporters(config);
            if (config.prepareChild)
            {
                GospelHeroPreviewSupport.Need(Path.GetFullPath(Environment.GetEnvironmentVariable("FIRSTGAME_HERO_CHILD_SETTINGS") ?? "")
                    == Path.GetFullPath(config.preparationSettings.file), "Explicit child-preparer environment must match its pinned settings.");
                GospelHeroChildPreparation.PrepareFromEnvironment();
            }
            GospelHeroPreviewSupport.RequireHero();
            GospelHeroPreviewSupport.Need(QualitySettings.skinWeights == SkinWeights.Unlimited, "Keep active Unlimited skin weights.");
            GospelHeroPreviewSupport.Need(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "A real graphics device is required; do not use -nographics.");
            var previous = new PreviousScenes { scenes = EditorSceneManager.GetSceneManagerSetup().Select(s => new PreviousScene { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray() };
            SessionState.SetString(ScenesKey, JsonUtility.ToJson(previous));
            Directory.CreateDirectory(config.outputDirectory);
            GospelHeroPreviewSupport.WriteNew(Path.Combine(config.outputDirectory, "scope-started.json"), Encoding.UTF8.GetBytes("{\"status\":\"started-preview-not-approved\",\"visualApproved\":false,\"releaseApproved\":false}\n"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            SessionState.SetFloat("FirstGame.GospelHero20261119.Preview.EnterDeadline", (float)EditorApplication.timeSinceStartup + 60);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            try
            {
                if (!Application.isPlaying)
                {
                    if (EditorApplication.timeSinceStartup <= SessionState.GetFloat("FirstGame.GospelHero20261119.Preview.EnterDeadline", float.MaxValue)) return;
                    config = GospelHeroPreviewSupport.ReadConfig();
                    report = new Index { attempt = config.attempt, unityVersion = Application.unityVersion, error = "PlayMode did not enter within its explicit 60-second bound." };
                    report.preferencesRestored = GospelHeroPreviewSession.RecoverPreferencesAfterUnexpectedExit(); Finish(); return;
                }
                if (!running)
                {
                    config = GospelHeroPreviewSupport.ReadConfig();
                    report = new Index { attempt = config.attempt, unityVersion = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName };
                    session = new GospelHeroPreviewSession(config.attempt);
                    sources = GospelHeroPreviewSupport.SnapshotSources(config);
                    running = true; cleaning = false; deadline = EditorApplication.timeSinceStartup + config.maximumRuntimeSeconds;
                    stack.Push(Sequence());
                }
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(cleaning ? "Preview cleanup deadline exceeded." : "Preview runtime deadline exceeded.");
                if (waiting is CustomYieldInstruction custom && custom.keepWaiting) return;
                if (waiting is AsyncOperation operation && !operation.isDone) return;
                waiting = null;
                if (stack.Count == 0)
                {
                    if (!cleaning) { BeginCleanup(null); return; }
                    Finish(); return;
                }
                var routine = stack.Peek();
                if (!routine.MoveNext()) { (routine as IDisposable)?.Dispose(); stack.Pop(); return; }
                if (routine.Current is IEnumerator nested && !(nested is CustomYieldInstruction)) stack.Push(nested);
                else waiting = routine.Current;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (config == null) config = JsonUtility.FromJson<GospelHeroPreviewSupport.Config>(SessionState.GetString(ConfigSnapshotKey, "{}"));
                if (report == null) report = new Index { attempt = config?.attempt, unityVersion = Application.unityVersion };
                if (!Application.isPlaying) { report.error = error.ToString(); report.preferencesRestored = GospelHeroPreviewSession.RecoverPreferencesAfterUnexpectedExit(); Finish(); return; }
                if (!cleaning) BeginCleanup(error);
                else
                {
                    if (report != null) report.error = (report.error ?? "") + "\ncleanup: " + error;
                    // Boot may still be alive when unloading timed out/failed.
                    // Keep the memory backend and temporary preference keys
                    // until the engine has actually stopped PlayMode. Restoring
                    // here could expose real storage to one more App.Update.
                    SessionState.SetBool(DeferredExitCleanupKey, true);
                    Finish();
                }
            }
        }

        static IEnumerator Sequence()
        {
            GospelHeroPreviewSupport.Need(Application.CanStreamedLevelBeLoaded(GospelHeroPreviewSupport.Boot), "Existing enabled Boot scene required; preview never creates/ships a replacement Boot.");
            var load = SceneManager.LoadSceneAsync(GospelHeroPreviewSupport.Boot, LoadSceneMode.Single);
            GospelHeroPreviewSupport.Need(load != null, "Runtime Boot load did not start."); yield return load;
            yield return new WaitForSecondsRealtime(1.2f);
            GospelHeroPreviewSupport.Need(App && App.World && App.World.NazarethSlice && App.World.NazarethSlice.Ready
                && App.World.NazarethSlice.OptionalTexturePairsApplied == 3, "Actual installed environment slice and all three 2K pairs required.");
            GospelHeroPreviewSupport.Need(App.Actor.CharacterView.ModelResource == GospelHeroPreviewSupport.HeroResource, "Actual player must use the new admitted child resource.");
            GospelHeroPreviewSupport.Click("새 여정 시작"); yield return null; yield return null;
            int opening = 0;
            while (App.Chronicle.CurrentQuest.IsPrologue && opening++ < 80)
            { GospelHeroPreviewSupport.Click("이야기 계속"); yield return null; yield return null; }
            GospelHeroPreviewSupport.Need(!App.Chronicle.CurrentQuest.IsPrologue, "Real opening did not reach the child story within the bound.");
            GospelHeroPreviewSupport.Click("이야기 속으로"); yield return new WaitForSecondsRealtime(1.0f);
            GospelHeroPreviewSupport.Need(App.Ui.CurrentView == "journey" && !App.Chronicle.IsAdult, "Actual age-appropriate field HUD required.");
            yield return Shot("01-field-default.png", "actual child field/default camera/HUD/minimap");
            float yaw = App.CameraRig.Yaw, pitch = App.CameraRig.Pitch, zoom = App.CameraRig.Zoom;
            App.CameraRig.SetZoom(GospelPilgrimageCamera.MinimumZoom); App.CameraRig.SetOrbit(App.Player.FacingYaw + 180, 7); App.CameraRig.Snap();
            yield return Shot("02-field-near-hero.png", "actual field camera at its supported near zoom; same player and ground");
            var face = App.Actor.CharacterView.GetPortraitTexture(1024) as RenderTexture;
            yield return new WaitForSecondsRealtime(.6f);
            GospelHeroPreviewSupport.Need(face && face.IsCreated() && App.Actor.CharacterView.HasVisiblePortrait, "Actual current rigged portrait backend must render.");
            SaveTexture(face, "03-actual-rigged-face.png", "actual new child rig/material/expression portrait backend, not a painted UI portrait");
            App.Actor.CharacterView.SetPortraitVisible(false);
            App.CameraRig.SetOrbit(yaw, pitch); App.CameraRig.SetZoom(zoom); App.CameraRig.Snap();
            float walkDeadline = Time.realtimeSinceStartup + 35;
            App.Ui.OnRun?.Invoke(true);
            while (!App.CanInteract && Time.realtimeSinceStartup < walkDeadline)
            {
                Vector3 delta = App.ObjectivePosition-App.Player.Position; delta.y = 0;
                Vector3 right = App.SceneCamera.transform.right; right.y = 0; right.Normalize();
                Vector3 forward = App.SceneCamera.transform.forward; forward.y = 0; forward.Normalize();
                App.Ui.OnMove?.Invoke(new Vector2(Vector3.Dot(delta, right), Vector3.Dot(delta, forward)).normalized);
                yield return new WaitForSecondsRealtime(.06f);
            }
            App.Ui.OnMove?.Invoke(Vector2.zero); App.Ui.OnRun?.Invoke(false);
            GospelHeroPreviewSupport.Need(App.CanInteract, "Actual controller/proximity travel failed; no teleport or story cursor injection is allowed.");
            App.Ui.OnInteract?.Invoke(); yield return null; yield return null;
            GospelHeroPreviewSupport.Need(App.Ui.CurrentView == "story", "Actual nearby interaction must open real dialogue.");
            int turn = 0, quest = App.Chronicle.QuestIndex;
            while (App.Chronicle.QuestIndex == quest && App.Chronicle.CurrentStoryBeat.Speaker != "요셉" && turn++ < 20)
            { GospelHeroPreviewSupport.Click("이야기 계속"); yield return null; yield return null; }
            GospelHeroPreviewSupport.Need(App.Chronicle.QuestIndex == quest && App.Chronicle.CurrentStoryBeat.Speaker == "요셉", "Capture a real Joseph turn, not a synthetic dialogue overlay.");
            yield return Shot("04-actual-joseph-dialogue.png", "actual accepted Joseph encounter, world and existing dialogue UI");
            report.sequenceReachedEnd = true;
        }

        static IEnumerator Shot(string name, string scope)
        {
            var camera = App.SceneCamera;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var oldCameras = canvases.Select(c => c.worldCamera).ToArray(); var distances = canvases.Select(c => c.planeDistance).ToArray();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active; float aspect = camera.aspect; int mask = camera.cullingMask;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                GospelHeroPreviewSupport.Need(target.Create(), "Native target allocation failed."); camera.targetTexture = target; camera.aspect = 1920f/1080; camera.cullingMask |= 1 << 5;
                foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane+.5f; }
                yield return new WaitForSecondsRealtime(.6f); Canvas.ForceUpdateCanvases();
                var request = new RenderPipeline.StandardRequest { destination = target };
                GospelHeroPreviewSupport.Need(RenderPipeline.SupportsRenderRequest(camera, request), "Real URP native camera request unsupported.");
                RenderPipeline.SubmitRenderRequest(camera, request); SaveTexture(target, name, scope);
            }
            finally
            {
                camera.targetTexture = oldTarget; camera.aspect = aspect; camera.cullingMask = mask; RenderTexture.active = oldActive;
                for (int i = 0; i < canvases.Length; i++) if (canvases[i]) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = oldCameras[i]; canvases[i].planeDistance = distances[i]; }
                target.Release(); Object.Destroy(target); Canvas.ForceUpdateCanvases();
            }
        }

        static void SaveTexture(RenderTexture target, string name, string scope)
        {
            var previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                RenderTexture.active = target; pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(); byte[] png = pixels.EncodeToPNG();
                GospelHeroPreviewSupport.Need(png.Length > 10000, "PNG is unexpectedly small; no successful visual result is inferred.");
                string path = Path.Combine(config.outputDirectory, name); GospelHeroPreviewSupport.WriteNew(path, png);
                var frame = new Frame { file = name, sha256 = GospelHeroPreviewSupport.HashFile(path), scope = scope,
                    width = target.width, height = target.height, resource = App.Actor.CharacterView.ModelResource,
                    uiView = App.Ui.CurrentView, quest = App.Chronicle.CurrentQuest?.Id ?? "", stage = App.Chronicle.Stage.ToString(),
                    player = App.Player.Position, playerYaw = App.Player.FacingYaw, camera = App.SceneCamera.transform.position,
                    orbitYaw = App.CameraRig.Yaw, orbitPitch = App.CameraRig.Pitch };
                report.frames.Add(frame);
                GospelHeroPreviewSupport.WriteNew(path + ".json", Encoding.UTF8.GetBytes(JsonUtility.ToJson(frame, true)+"\n"));
            }
            finally { RenderTexture.active = previous; if (pixels) Object.Destroy(pixels); }
        }

        static void BeginCleanup(Exception error)
        {
            if (report != null && error != null) report.error = error.ToString();
            while (stack.Count > 0) { try { (stack.Pop() as IDisposable)?.Dispose(); } catch (Exception dispose) { if (report != null) report.error = (report.error ?? "")+"\ndispose: "+dispose; } }
            waiting = null; running = true; cleaning = true; deadline = EditorApplication.timeSinceStartup + 20; stack.Push(Cleanup());
        }
        static IEnumerator Cleanup()
        {
            if (App && App.Ui) { App.Ui.OnMove?.Invoke(Vector2.zero); App.Ui.OnRun?.Invoke(false); }
            var boot = SceneManager.GetSceneByPath(GospelHeroPreviewSupport.Boot);
            if (boot.IsValid() && boot.isLoaded)
            {
                var empty = SceneManager.CreateScene("Hero preview cleanup only"); SceneManager.SetActiveScene(empty);
                var unload = SceneManager.UnloadSceneAsync(boot); if (unload != null) yield return unload; yield return null;
            }
            session?.Dispose(); session = null; report.preferencesRestored = true;
            GospelHeroPreviewSupport.AssertUnchanged(sources); report.originalSourcesUnchanged = true;
        }
        static void Finish()
        {
            bool ok = report != null && report.sequenceReachedEnd && report.originalSourcesUnchanged && report.preferencesRestored && string.IsNullOrEmpty(report.error);
            if (report != null)
            {
                report.status = ok ? "preview-sequence-completed-root-original-review-pending" : "preview-failed-not-approved";
                GospelHeroPreviewSupport.WriteNew(Path.Combine(config.outputDirectory, "preview-index.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)+"\n"));
            }
            SessionState.SetBool(Pending, false); running = false; cleaning = false; waiting = null; stack.Clear();
            bool exit = config != null ? config.exitEditorWhenDone : Application.isBatchMode;
            SessionState.SetInt("FirstGame.GospelHero20261119.Preview.ExitCode", ok ? 0 : 2);
            SessionState.SetBool("FirstGame.GospelHero20261119.Preview.ExitEditor", exit);
            EditorApplication.playModeStateChanged -= AfterExit; EditorApplication.playModeStateChanged += AfterExit;
            if (Application.isPlaying) EditorApplication.isPlaying = false;
            else AfterExit(PlayModeStateChange.EnteredEditMode);
        }
        [InitializeOnLoadMethod] static void RehookExit() { EditorApplication.playModeStateChanged -= AfterExit; EditorApplication.playModeStateChanged += AfterExit; }
        static void AfterExit(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            var exitCleanup = exitCleanupRecord ?? (exitCleanupRecord = new ExitCleanup {
                deferredCleanupRequested = SessionState.GetBool(DeferredExitCleanupKey, false),
                preferencesRestored = report != null && report.preferencesRestored });
            // Domain Reload disabled retains these managed references. Dispose
            // their actual override scope only now, with the runtime stopped.
            // With Domain Reload enabled, the preference checkpoint survives
            // and the managed storage statics have already returned to default.
            if (session != null)
            {
                try { session.Dispose(); exitCleanup.managedSessionDisposed = true; exitCleanup.preferencesRestored = true; }
                catch (Exception error) { exitCleanup.error = "managed session restore: " + error; }
                finally { session = null; }
            }
            try
            {
                exitCleanup.preferenceCheckpointRecovered |= GospelHeroPreviewSession.RecoverPreferencesAfterUnexpectedExit();
                exitCleanup.preferencesRestored |= exitCleanup.preferenceCheckpointRecovered;
            }
            catch (Exception error) { exitCleanup.error = (exitCleanup.error ?? "") + "\npreference checkpoint restore: " + error; }
            if (report != null) report.preferencesRestored = exitCleanup.preferencesRestored;
            if (SessionState.GetBool(Pending, false))
            {
                config = config ?? JsonUtility.FromJson<GospelHeroPreviewSupport.Config>(SessionState.GetString(ConfigSnapshotKey, "{}"));
                report = report ?? new Index { attempt = config.attempt, unityVersion = Application.unityVersion };
                report.error = (report.error ?? "") + "\nPlayMode exited before preview cleanup completed.";
                if (!string.IsNullOrEmpty(exitCleanup.error)) report.error += "\n" + exitCleanup.error;
                report.preferencesRestored = exitCleanup.preferencesRestored; Finish(); return;
            }
            string json = SessionState.GetString(ScenesKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                // The pre-exit index remains the original result (including
                // failure). This separate actual-phase record never promotes it.
                if (config == null) config = JsonUtility.FromJson<GospelHeroPreviewSupport.Config>(SessionState.GetString(ConfigSnapshotKey, "{}"));
                GospelHeroPreviewSupport.WriteNew(Path.Combine(config.outputDirectory, "preview-exit-cleanup.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(exitCleanup, true) + "\n"));
                if (!string.IsNullOrEmpty(exitCleanup.error) || !exitCleanup.preferencesRestored && exitCleanup.deferredCleanupRequested)
                    SessionState.SetInt("FirstGame.GospelHero20261119.Preview.ExitCode", 2);
                SessionState.SetBool(DeferredExitCleanupKey, false);
                var previous = JsonUtility.FromJson<PreviousScenes>(json); SessionState.EraseString(ScenesKey);
                if (previous?.scenes != null && previous.scenes.Length > 0 && previous.scenes.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(previous.scenes.Select(s => new SceneSetup { path = s.path, isLoaded = s.loaded, isActive = s.active }).ToArray());
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            exitCleanupRecord = null;
            bool exit = SessionState.GetBool("FirstGame.GospelHero20261119.Preview.ExitEditor", false);
            SessionState.SetBool("FirstGame.GospelHero20261119.Preview.ExitEditor", false);
            if (exit) EditorApplication.Exit(SessionState.GetInt("FirstGame.GospelHero20261119.Preview.ExitCode", 2));
        }
    }
}
#endif
