using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FirstGame.Editor
{
    public static class BuildCommands
    {
        [MenuItem("FirstGame/Build Windows")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Windows", "FantasyCards.exe");
        [MenuItem("FirstGame/Build Web")]
        public static void Web() => Build(BuildTarget.WebGL, "Web", "");

        static void Build(BuildTarget target, string profile, string executable)
        {
            ProjectSetup.Ensure();
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new BuildFailedException("Install the Unity module for " + target + ".");
            // BuildPlayer can unload asset objects. Keep persistent paths across the build.
            var previousPipelinePath = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);
            var previousQualityPipelinePath = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline);
            int previousTextureLimit = QualitySettings.globalTextureMipmapLimit;
            try
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/" + (target == BuildTarget.WebGL ? "Web" : "Desktop") + "URP.asset");
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
                QualitySettings.globalTextureMipmapLimit = target == BuildTarget.WebGL ? 1 : 0;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.dataCaching = true;
                // Content-addressed files prevent mixed cached versions after a web update.
                PlayerSettings.WebGL.nameFilesAsHashes = true;
                PlayerSettings.WebGL.template = "PROJECT:FirstGame";
                var root = Environment.GetEnvironmentVariable("FIRSTGAME_BUILD_ROOT");
                if (string.IsNullOrEmpty(root)) root = Path.GetFullPath("../../Artifacts/Builds");
                string output = Path.Combine(root, "FantasyCards", profile);
                Directory.CreateDirectory(output);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { "Assets/Scenes/Boot.unity" }, target = target,
                    locationPathName = string.IsNullOrEmpty(executable) ? output : Path.Combine(output, executable),
                    options = BuildOptions.StrictMode
                });
                File.WriteAllText(Path.Combine(output, "BuildEvidence.txt"),
                    "Engine: " + Application.unityVersion + "\nTarget: " + target + "\nResult: " + report.summary.result +
                    "\nBytes: " + report.summary.totalSize + "\nDuration: " + report.summary.totalTime +
                    "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings +
                    "\nContent: " + FantasyCards.Core.CardCatalog.Version + "\nRevision: fm-remaster-2026-10-26\n");
                if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Build failed: " + report.summary.result);
                Debug.Log("FIRSTGAME_BUILD_OK " + output);
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = LoadPreviousPipeline(previousPipelinePath);
                QualitySettings.renderPipeline = LoadPreviousPipeline(previousQualityPipelinePath);
                QualitySettings.globalTextureMipmapLimit = previousTextureLimit; AssetDatabase.SaveAssets();
            }
        }

        static RenderPipelineAsset LoadPreviousPipeline(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (pipeline == null) throw new BuildFailedException("Could not restore render pipeline asset: " + path);
            return pipeline;
        }
    }
}
