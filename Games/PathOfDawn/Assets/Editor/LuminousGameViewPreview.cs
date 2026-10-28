using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FirstGame.Editor
{
    /// <summary>Capture the actual GUI GameView, without changing cameras or UI render mode.</summary>
    [InitializeOnLoad]
    public static class LuminousGameViewPreview
    {
        const string Pending = "FirstGame.Luminous.GameViewPending";
        const string Destination = "FirstGame.Luminous.GameViewDestination";
        static double readyAt = -1, deadline;
        static bool requested;
        static LuminousGameViewPreview() { EditorApplication.update += Tick; }

        public static void Capture()
        {
            string path = Environment.GetEnvironmentVariable("FIRSTGAME_GAMEVIEW_CAPTURE");
            if (string.IsNullOrEmpty(path) || File.Exists(path)) throw new InvalidOperationException("A new task-owned capture path is required.");
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Artifacts/Validation/Luminous"));
            path = Path.GetFullPath(path);
            if (!path.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("GameView capture must stay inside current Luminous evidence.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            SessionState.SetString(Destination, path);
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene(LuminousProjectSetup.ScenePath, OpenSceneMode.Single);
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.GameView")).FirstOrDefault(t => t != null);
            if (type == null) throw new InvalidOperationException("Unity GameView type is unavailable.");
            var gameView = EditorWindow.GetWindow(type);
            gameView.maximized = true; gameView.Show(); gameView.Focus();
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            if (readyAt < 0) readyAt = EditorApplication.timeSinceStartup + 5;
            string path = SessionState.GetString(Destination, "");
            if (!requested && EditorApplication.timeSinceStartup >= readyAt)
            {
                // Normal player screenshot: no RenderTexture/StandardRequest,
                // target MSAA/format override or canvas conversion is involved.
                ScreenCapture.CaptureScreenshot(path);
                requested = true; deadline = EditorApplication.timeSinceStartup + 15;
            }
            if (requested && File.Exists(path) && new FileInfo(path).Length > 10000)
            {
                SessionState.SetBool(Pending, false);
                Debug.Log("FIRSTGAME_LUMINOUS_ACTUAL_GAMEVIEW_OK " + path);
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
            else if (requested && EditorApplication.timeSinceStartup > deadline)
            {
                SessionState.SetBool(Pending, false);
                Debug.LogError("Actual GameView screenshot did not complete within the bounded capture window.");
                EditorApplication.Exit(2);
            }
        }
    }
}
