using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FirstGame.Editor
{
    /// <summary>Build the current authored 2D scene; legacy 3D sources remain intact.</summary>
    public static class LuminousProjectSetup
    {
        public const string Revision = "gospel-luminous-2026-10-28";
        public const string AppType = "PathOfDawn.Presentation.GospelLuminousApp, PathOfDawn.Presentation";
        public const string ScenePath = "Assets/Scenes/Boot.unity";
        static bool preparing;

        public static void Ensure()
        {
            if (preparing) return;
            preparing = true;
            try
            {
                foreach (string folder in new[] { "Assets/Scenes", "Assets/Settings", "Assets/Resources/Fonts", "Assets/Resources/Art/GospelLuminous" })
                    Directory.CreateDirectory(folder);
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
                Physics2D.gravity = new Vector2(0, -9.81f);
                KeepSpriteShaders();
                PrepareNormalPointLight();
                var font = ProjectSetup.CreateFont();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var type = Type.GetType(AppType, true);
                var app = new GameObject("Gospel Luminous Bootstrap").AddComponent(type);
                type.GetField("KoreanFont")?.SetValue(app, font);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.SaveAssets();
                Validate();
                File.WriteAllText("ProjectSettings/FirstGameSetup.version", "6000.3.26f1:" + Revision + "\n");
                Debug.Log("FIRSTGAME_LUMINOUS_SETUP_OK " + Revision);
            }
            finally { preparing = false; }
        }

        static UniversalRenderPipelineAsset Pipeline(string profile)
        {
            string rendererPath = "Assets/Settings/Luminous" + profile + "Renderer.asset";
            string pipelinePath = "Assets/Settings/Luminous" + profile + "URP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var settings = new SerializedObject(renderer);
            settings.FindProperty("m_LightRenderTextureScale").floatValue = 1f;
            settings.FindProperty("m_HDREmulationScale").floatValue = 2f;
            settings.FindProperty("m_PostProcessData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            settings.ApplyModifiedPropertiesWithoutUndo();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.name = "Luminous" + profile + "URP";
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = true;
            pipeline.useSRPBatcher = true;
            pipeline.shadowDistance = 0f;
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        static void KeepSpriteShaders()
        {
            // The scene is constructed at runtime. Explicit Resources materials
            // retain its shader dependencies in players as well as in the Editor.
            const string folder = "Assets/Resources/Art/GospelLuminous/Rendering";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string[] names = { "Universal Render Pipeline/2D/Sprite-Lit-Default", "Universal Render Pipeline/2D/Sprite-Unlit-Default", "Sprites/Default", "Universal Render Pipeline/2D/Mesh2D-Lit-Default", "Universal Render Pipeline/2D/Mesh2D-Unlit-Default", "FirstGame/Luminous Cloth 2D Lit" };
            string[] files = { "SpriteLit", "SpriteUnlit", "LegacySpriteCloth", "MeshLit", "MeshUnlit", "LuminousCloth" };
            for (int i = 0; i < names.Length; i++)
            {
                var shader = Shader.Find(names[i]);
                if (shader == null) throw new InvalidOperationException("Missing current sprite shader: " + names[i]);
                string path = folder + "/" + files[i] + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) AssetDatabase.CreateAsset(new Material(shader) { name = files[i] }, path);
                else if (material.shader != shader) { material.shader = shader; EditorUtility.SetDirty(material); }
            }
        }

        static void PrepareNormalPointLight()
        {
            const string path = "Assets/Resources/Art/GospelLuminous/Rendering/NormalPointLight.prefab";
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            var root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Authored normal-lit point light");
            try
            {
                var light = root.GetComponent<Light2D>();
                if (light == null) light = root.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.pointLightOuterRadius = 4f;
                light.intensity = 1f;
                var settings = new SerializedObject(light);
                var quality = settings.FindProperty("m_NormalMapQuality");
                if (quality == null) throw new InvalidOperationException("URP 2D normal-light authoring schema changed.");
                quality.enumValueIndex = (int)Light2D.NormalMapQuality.Fast;
                settings.ApplyModifiedPropertiesWithoutUndo();
                if (light.normalMapQuality != Light2D.NormalMapQuality.Fast)
                    throw new InvalidOperationException("The authored point light did not retain normal-map quality.");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void Validate()
        {
            var app = UnityEngine.Object.FindFirstObjectByType(Type.GetType(AppType, true)) as Component;
            if (app == null) throw new InvalidOperationException("Missing current 2D Gospel entry.");
            if (UnityEngine.Object.FindFirstObjectByType<PathOfDawn.Presentation.GospelJourneyApp>() != null ||
                UnityEngine.Object.FindFirstObjectByType<PathOfDawn.Presentation.PathOfDawnApp>() != null)
                throw new InvalidOperationException("A retired presentation must not run beside the 2D entry.");
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset pipeline))
                throw new InvalidOperationException("Current Gospel requires URP.");
            var settings = new SerializedObject(pipeline);
            if (!(settings.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue is Renderer2DData))
                throw new InvalidOperationException("The current scene requires the real URP 2D renderer.");
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var node in go.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0)
                        throw new InvalidOperationException("Missing current script on " + node.name);
            Debug.Log("FIRSTGAME_LUMINOUS_ASSET_VALIDATION_OK");
        }
    }
}
