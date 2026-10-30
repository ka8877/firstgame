using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FirstGame.Editor
{
    /// <summary>Current free-field HD-2D entry. All old renderer assets remain as history.</summary>
    public static class PilgrimageProjectSetup
    {
        public const string Revision = "gospel-journey-2026-10-30";
        public const string AppType = "PathOfDawn.Presentation.GospelPilgrimageApp, PathOfDawn.Presentation";
        const string ScenePath = "Assets/Scenes/Boot.unity";
        static bool preparing;

        public static void Ensure()
        {
            if (preparing) return;
            preparing = true;
            try
            {
                Directory.CreateDirectory("Assets/Settings");
                Directory.CreateDirectory("Assets/Scenes");
                Directory.CreateDirectory("Assets/Resources/Art/GospelPilgrimage/Rendering");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorSettings.serializationMode = SerializationMode.ForceText;
                PlayerSettings.companyName = "FirstGame";
                PlayerSettings.productName = "PathOfDawn";
                PlayerSettings.defaultScreenWidth = 1600;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.runInBackground = true;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.colorSpace = ColorSpace.Linear;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
                var desktop = Pipeline("Desktop");
                Pipeline("Web");
                GraphicsSettings.defaultRenderPipeline = desktop;
                QualitySettings.renderPipeline = desktop;
                QualitySettings.vSyncCount = 0;
                PreserveShaders();
                PrepareSpriteImports();
                var font = ProjectSetup.CreateFont();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var type = Type.GetType(AppType, true);
                var app = new GameObject("Pilgrimage HD2D Bootstrap").AddComponent(type);
                type.GetField("KoreanFont")?.SetValue(app, font);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.SaveAssets();
                Validate();
                File.WriteAllText("ProjectSettings/FirstGameSetup.version", "6000.3.26f1:" + Revision + "\n");
                Debug.Log("FIRSTGAME_PILGRIMAGE_SETUP_OK " + Revision);
            }
            finally { preparing = false; }
        }

        static UniversalRenderPipelineAsset Pipeline(string profile)
        {
            const string rendererPath = "Assets/Settings/QuestRenderer.asset";
            string pipelinePath = "Assets/Settings/Quest" + profile + "URP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var rendererSettings = new SerializedObject(renderer);
            var post = rendererSettings.FindProperty("postProcessData") ?? rendererSettings.FindProperty("m_PostProcessData");
            if (post != null)
                post.objectReferenceValue = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            bool create = pipeline == null;
            if (create) pipeline = UniversalRenderPipelineAsset.Create(renderer);
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.name = "Quest" + profile + "URP";
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = true;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.useSRPBatcher = true;
            pipeline.shadowDistance = profile == "Web" ? 30 : 42;
            pipeline.mainLightShadowmapResolution = profile == "Web" ? 1024 : 2048;
            pipeline.shadowCascadeCount = 2;
            if (create) AssetDatabase.CreateAsset(pipeline, pipelinePath);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        static void PreserveShaders()
        {
            string[] names = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit" };
            string[] files = { "QuestLit", "QuestUnlit" };
            for (int i = 0; i < names.Length; i++)
            {
                var shader = Shader.Find(names[i]);
                if (shader == null) throw new InvalidOperationException("Missing field shader: " + names[i]);
                string path = "Assets/Resources/Art/GospelPilgrimage/Rendering/" + files[i] + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) AssetDatabase.CreateAsset(new Material(shader) { name = files[i] }, path);
                else { material.shader = shader; EditorUtility.SetDirty(material); }
            }
            if (Resources.Load<Shader>("Art/GospelPilgrimage/Rendering/PilgrimageSprite") == null)
                throw new InvalidOperationException("Missing retained HD2D sprite shader.");
        }

        static void PrepareSpriteImports()
        {
            const string folder = "Assets/Resources/Art/GospelPilgrimage/Actors";
            foreach (var path in Directory.GetFiles(folder, "*.png"))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing original pixel-art importer: " + path);
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = path.Contains("portraits") ? FilterMode.Bilinear : FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        public static void Validate()
        {
            if (UnityEngine.Object.FindFirstObjectByType(Type.GetType(AppType, true)) == null)
                throw new InvalidOperationException("Missing current HD2D field entry.");
            if (UnityEngine.Object.FindFirstObjectByType<PathOfDawn.Presentation.GospelLuminousApp>() != null ||
                UnityEngine.Object.FindFirstObjectByType<PathOfDawn.Presentation.GospelJourneyApp>() != null ||
                UnityEngine.Object.FindFirstObjectByType<PathOfDawn.Presentation.PathOfDawnApp>() != null)
                throw new InvalidOperationException("A historical presentation is running beside the field.");
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset pipeline))
                throw new InvalidOperationException("The free field requires URP.");
            var settings = new SerializedObject(pipeline);
            if (!(settings.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue is UniversalRendererData))
                throw new InvalidOperationException("The free field requires the URP 3D renderer.");
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var node in go.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0)
                        throw new InvalidOperationException("Missing field script: " + node.name);
            Debug.Log("FIRSTGAME_PILGRIMAGE_ASSET_VALIDATION_OK");
        }
    }
}
