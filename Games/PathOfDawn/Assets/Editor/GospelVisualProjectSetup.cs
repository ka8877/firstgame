#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Firstgame.GospelNpr.Editor;
using PathOfDawn.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FirstGame.Editor
{
    /// <summary>PROPOSAL: validate prepared resources and existing Boot; never rebuild legacy actors/scene.</summary>
    public static class GospelVisualProjectSetup
    {
        public const string Revision = "gospel-galilee-figure-2026-11-28";
        const string Boot = "Assets/Scenes/Boot.unity";

        public static void ValidateAndActivate()
        {
            Need(Environment.GetEnvironmentVariable("FIRSTGAME_RELEASE_PROFILE") == "gospel-visual", "Explicit Gospel visual activation required.");
            Need(!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode && !BuildPipeline.isBuildingPlayer, "Idle Gospel editor required.");
            Need(SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore, "Preserve real force-glcore; never switch this prepared lilToon project to Null.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                Need(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, "Preserve existing dirty editor scenes.");
            Need(EditorSettings.serializationMode == SerializationMode.ForceText && PlayerSettings.colorSpace == ColorSpace.Linear,
                "Prepared text serialization and Linear settings must already be correct.");
            Need(PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL) == ScriptingImplementation.IL2CPP,
                "Prepared WebGL IL2CPP target required.");
            foreach (var target in new[] { UnityEditor.Build.NamedBuildTarget.Standalone, UnityEditor.Build.NamedBuildTarget.WebGL })
                Need(PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Contains("LILTOON_DISABLE_OPTIMIZATION"),
                    "Retain the actual Resources-loaded shader-feature preservation define.");
            var desktop = RequiredPipeline("Desktop");
            RequiredPipeline("Web");
            RequireHero();
            Need(AssetDatabase.LoadAssetAtPath<SceneAsset>(Boot), "Existing reviewed Boot scene required; no replacement is generated.");
            Need(EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].enabled && EditorBuildSettings.scenes[0].path == Boot,
                "Retain exactly the existing reviewed Boot build entry.");
            // Explicit current settings only. No old preparer, prefab, model,
            // material, font, scene, importer, legacy Resources or skeleton rewrite.
            GraphicsSettings.defaultRenderPipeline = desktop;
            QualitySettings.renderPipeline = desktop;
            QualitySettings.skinWeights = SkinWeights.Unlimited;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.vSyncCount = 0;
            var scene = EditorSceneManager.OpenScene(Boot, OpenSceneMode.Single);
            var apps = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GospelPilgrimageApp>(true)).ToArray();
            Need(apps.Length == 1 && apps[0].enabled, "One existing current Gospel runtime bootstrap required.");
            PilgrimageProjectSetup.Validate();
            AssetDatabase.SaveAssets();
            string marker = "6000.3.26f1:" + Revision + "\n";
            const string markerPath = "ProjectSettings/FirstGameSetup.version";
            if (!File.Exists(markerPath) || File.ReadAllText(markerPath) != marker)
                File.WriteAllText(markerPath, marker);
            Debug.Log("FIRSTGAME_GOSPEL_VISUAL_SETUP_VALIDATED " + Revision);
        }

        static UniversalRenderPipelineAsset RequiredPipeline(string profile)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Quest" + profile + "URP.asset");
            Need(pipeline && Mathf.Approximately(pipeline.renderScale, 1) && pipeline.msaaSampleCount >= 4 && pipeline.supportsHDR,
                "Prepared full-resolution MSAA/HDR URP asset required: " + profile);
            var serialized = new SerializedObject(pipeline);
            var data = serialized.FindProperty("m_RendererDataList");
            Need(data != null && data.arraySize > 0 && data.GetArrayElementAtIndex(0).objectReferenceValue is UniversalRendererData,
                "Prepared URP 3D renderer required: " + profile);
            Need(serialized.FindProperty("m_DefaultRendererIndex")?.intValue == 0, "Prepared current 3D renderer must remain the default: " + profile);
            return pipeline;
        }

        static void RequireHero()
        {
            Need(GospelHeroModelProfile.TryResolve("JesusChild", out var prefab, out string error), error ?? "Prepared independent new hero resource required.");
            var profile = prefab.GetComponent<GospelHeroModelProfile>();
            Need(profile.SourceSha256 == GospelHeroChildPreparation.SourceSha256, "Actual selected original child identity required.");
            var animators = prefab.GetComponentsInChildren<Animator>(true);
            Need(animators.Length == 1 && animators[0].avatar && animators[0].avatar.isHuman && animators[0].avatar.isValid
                && animators[0].runtimeAnimatorController && animators[0].runtimeAnimatorController.animationClips.Distinct().Count() >= 7,
                "Prepared valid humanoid and retained actual motion controller required; no retargeting runs here.");
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Need(renderers.Length > 0 && renderers.All(r => r.sharedMesh && r.sharedMesh.vertexCount > 0 && r.bones.Length > 0
                && r.bones.All(b => b) && r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m && m.shader && m.shader.isSupported)),
                "Prepared real skinned meshes, bones and supported materials required.");
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            var models = AssetDatabase.GetDependencies(prefabPath, true).Where(p => p.StartsWith("Assets/Art/GospelVisualRebuild20261119/", StringComparison.Ordinal)
                && p.EndsWith("/Models/JesusChild.fbx", StringComparison.Ordinal)).ToArray();
            Need(models.Length == 1 && Hash(Path.Combine(Directory.GetParent(Application.dataPath).FullName, models[0])) == profile.ExportedModelSha256,
                "Actual prepared exported model must match the runtime profile source identity.");
        }

        static string Hash(string path)
        {
            using (var hash = SHA256.Create())
            using (var input = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
