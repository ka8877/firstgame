using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace FirstGame.Editor
{
    // PathOfDawn's authored presentation bootstrap. Keep this visual setup when refreshing build helpers.
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string Game = "PathOfDawn";
        const string AppType = "PathOfDawn.Presentation.PathOfDawnApp, PathOfDawn.Presentation";
        const string ScenePath = "Assets/Scenes/Boot.unity";
        static bool initializing;
        static ProjectSetup()
        {
            // The first import prepares missing assets; subsequent opens preserve user work.
            if (!Application.isBatchMode)
                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists("ProjectSettings/FirstGameSetup.version"))
                    { try { Ensure(); } catch (Exception e) { Debug.LogException(e); } }
                };
        }

        [MenuItem("FirstGame/Prepare First Slice")]
        public static void Ensure()
        {
            if (initializing) return;
            initializing = true;
            try
            {
                foreach (var folder in new[] { "Assets/Scenes", "Assets/Settings", "Assets/Settings/Materials", "Assets/Resources/Fonts", "Assets/Resources/Models", "Assets/Resources/Art", "Assets/Resources/Content" })
                    Directory.CreateDirectory(folder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorSettings.serializationMode = SerializationMode.ForceText;
                PlayerSettings.companyName = "FirstGame";
                PlayerSettings.productName = Game;
                PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
                PlayerSettings.runInBackground = true; PlayerSettings.resizableWindow = true;
                PlayerSettings.colorSpace = ColorSpace.Linear;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
                var desktop = CreatePipeline("Desktop", 1f, 2048, 40f, true);
                CreatePipeline("Web", .8f, 1024, 20f, false);
                GraphicsSettings.defaultRenderPipeline = desktop;
                QualitySettings.renderPipeline = desktop;
                QualitySettings.vSyncCount = 0;
                var font = CreateFont();
                PrepareIllustrations();
                CreateModels();
                CreateContent();
                var scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var type = Type.GetType(AppType, true);
                var app = UnityEngine.Object.FindFirstObjectByType(type) as Component;
                if (app == null) app = new GameObject(Game + " Bootstrap").AddComponent(type);
                SetField(app, "KoreanFont", font);
                SetField(app, "Chapter", AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/Content/Galilee.asset"));
                SetField(app, "EnvironmentPrefab", Resources.Load<GameObject>("Models/GalileeShore"));
                SetField(app, "JesusPrefab", Resources.Load<GameObject>("Models/JesusProxy"));
                SetField(app, "TravelerPrefab", Resources.Load<GameObject>("Models/TravelerProxy"));
                SetField(app, "MiriamPrefab", Resources.Load<GameObject>("Models/MiriamProxy"));
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.SaveAssets();
                Validate();
                File.WriteAllText("ProjectSettings/FirstGameSetup.version", "6000.3.26f1:first-slice:1\n");
                Debug.Log("FIRSTGAME_SETUP_OK " + Game);
            }
            finally { initializing = false; }
        }

        static UniversalRenderPipelineAsset CreatePipeline(string name, float scale, int shadow, float distance, bool hdr)
        {
            string path = "Assets/Settings/" + name + "URP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset != null) return asset;
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, "Assets/Settings/" + name + "Renderer.asset");
            asset = UniversalRenderPipelineAsset.Create(renderer);
            asset.name = name + "URP"; asset.renderScale = scale; asset.msaaSampleCount = 2;
            asset.supportsHDR = hdr; asset.shadowDistance = distance;
            asset.mainLightShadowmapResolution = shadow; asset.shadowCascadeCount = hdr ? 2 : 1;
            // URP17.3 exposes this setting read-only in its public runtime API.
            // The serialized field is verified in the installed package source.
            var serializedPipeline = new SerializedObject(asset);
            serializedPipeline.FindProperty("m_SoftShadowsSupported").boolValue = hdr;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static TMP_FontAsset CreateFont()
        {
            const string path = "Assets/Resources/Fonts/Korean.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/FirstGameKorean.otf");
                if (source == null) throw new InvalidOperationException("Korean source font is missing.");
                font = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (font == null) throw new InvalidOperationException("TMP font generation failed.");
                font.name = "Korean"; font.isMultiAtlasTexturesEnabled = true;
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
                string content = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ·—×→①②③";
                foreach (var file in Directory.GetFiles("Assets/Game", "*", SearchOption.AllDirectories))
                    if (file.EndsWith(".cs") || file.EndsWith(".ink")) content += File.ReadAllText(file);
                string characters = new string(content.Where(c => !char.IsControl(c)).Distinct().ToArray());
                if (!font.TryAddCharacters(characters, out string missing))
                    Debug.LogWarning("Font does not contain these optional source symbols: " + missing);
                foreach (var texture in font.atlasTextures)
                    if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
                EditorUtility.SetDirty(font);
            }
            TMP_Settings.defaultFontAsset = font;
            EditorUtility.SetDirty(TMP_Settings.instance);
            return font;
        }

        static void CreateModels()
        {
            string folder = "Assets/Art/Generated";
            if (!Directory.Exists(folder)) throw new InvalidOperationException("Generated Blender models are missing.");
            var toon = Shader.Find("PathOfDawn/Cel Lit");
            if (toon == null) throw new InvalidOperationException("PathOfDawn cel shader is missing.");
            var skyShader = Shader.Find("PathOfDawn/Storybook Sky");
            var waterShader = Shader.Find("PathOfDawn/Storybook Water");
            if (skyShader == null || waterShader == null) throw new InvalidOperationException("Storybook sky/water shaders are missing.");
            const string skyPath = "Assets/Resources/Art/DawnSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(skyShader); AssetDatabase.CreateAsset(sky, skyPath); }
            else sky.shader = skyShader;
            sky.SetColor("_Zenith", new Color(.26f,.55f,.86f));
            sky.SetColor("_Horizon", new Color(.79f,.91f,.96f));
            sky.SetColor("_CloudColor", new Color(1,.98f,.90f));
            EditorUtility.SetDirty(sky);
            foreach (var modelPath in Directory.GetFiles(folder, "*.fbx").OrderBy(p => p, StringComparer.Ordinal))
            {
                string path = modelPath.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                string prefabPath = "Assets/Resources/Models/" + name + ".prefab";
                bool character = name.EndsWith("Proxy", StringComparison.Ordinal);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importAnimation = character;
                // Preserve the authored smooth surface normals; never rebuild low-angle hard seams.
                importer.importNormals = ModelImporterNormals.Import;
                // Smooth characters exceed 65k vertices. A forced UInt16 import splits
                // them into several renderers and breaks the single skinned-body contract.
                importer.indexFormat = ModelImporterIndexFormat.UInt32;
                if (character) { importer.animationType = ModelImporterAnimationType.Generic; importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; }
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) throw new InvalidOperationException("Model did not import: " + path);
                var go = UnityEngine.Object.Instantiate(model); go.name = name;
                try
                {
                    foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            var old = materials[i];
                            string materialName = old != null ? old.name : "Default";
                            string safe = string.Concat(materialName.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
                            string materialPath = "Assets/Settings/Materials/" + safe + ".mat";
                            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                            bool create = material == null;
                            var selectedShader = IsGalileeWaterMaterial(materialName) ? waterShader : toon;
                            if (create) material = new Material(selectedShader); else material.shader = selectedShader;
                            material.name = materialName;
                            material.SetColor("_BaseColor", old != null && old.HasProperty("_BaseColor") ? old.GetColor("_BaseColor") : old != null && old.HasProperty("_Color") ? old.GetColor("_Color") : new Color(.65f, .60f, .48f));
                            if (selectedShader == toon)
                            {
                                material.SetColor("_ShadeColor", character ? new Color(.70f,.73f,.85f) : new Color(.72f,.79f,.86f));
                                material.SetColor("_RimColor", new Color(1,.94f,.76f));
                                material.SetFloat("_ShadeThreshold", .48f); material.SetFloat("_LightThreshold", .78f);
                                material.SetFloat("_BandSoftness", .10f);
                                material.SetFloat("_RimStrength", character ? .11f : .025f);
                                material.SetFloat("_InkStrength", character ? .16f : .025f);
                            }
                            material.enableInstancing = true;
                            if (create) AssetDatabase.CreateAsset(material, materialPath); else EditorUtility.SetDirty(material);
                            materials[i] = material;
                        }
                        renderer.sharedMaterials = materials;
                    }
                    if (name == "GalileeShore")
                    {
                        var waterMaterials = go.GetComponentsInChildren<Renderer>(true)
                            .SelectMany(r => r.sharedMaterials)
                            .Where(m => m != null && IsGalileeWaterMaterial(m.name)).ToArray();
                        if (waterMaterials.Length == 0 || waterMaterials.Any(m => m.shader != waterShader))
                            throw new InvalidOperationException("Galilee shore requires the authored lake material with the Storybook Water shader.");
                    }
                    if (character)
                    {
                        var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                        if (clips.Length == 0) throw new InvalidOperationException("Missing Blender idle take: " + name);
                        var sourceClip = clips.FirstOrDefault(c => c.name.Contains("Idle")) ?? clips[0];
                        string clipPath = "Assets/Settings/" + name + "Idle.anim";
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if (clip == null) { clip = UnityEngine.Object.Instantiate(sourceClip); AssetDatabase.CreateAsset(clip, clipPath); }
                        else EditorUtility.CopySerialized(sourceClip, clip);
                        clip.name = name + " Idle";
                        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);
                        EditorUtility.SetDirty(clip);
                        string controllerPath = "Assets/Settings/" + name + "Idle.controller";
                        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                        var states = controller.layers[0].stateMachine.states;
                        if (states.Length == 0) controller.AddMotion(clip);
                        else states[0].state.motion = clip;
                        EditorUtility.SetDirty(controller);
                        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                        // The revised source is one skinned body per actor. A stale proxy must never survive regeneration.
                        if (go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 1 || go.GetComponentsInChildren<MeshRenderer>(true).Length != 0)
                            throw new InvalidOperationException("Expected one skinned character visual without a duplicate static body: " + name);
                    }
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
        }

        static bool IsGalileeWaterMaterial(string materialName)
        {
            // Unity may prefix imported material names with the FBX name.
            const string sourceName = "POD_GalileeTeal";
            return string.Equals(materialName, sourceName, StringComparison.Ordinal)
                || materialName.EndsWith("-" + sourceName, StringComparison.Ordinal);
        }

        static void PrepareIllustrations()
        {
            foreach (string resource in new[] { "title-hero", "portrait-jesus", "portrait-miriam", "portrait-traveler" })
            {
                string path = "Assets/Resources/Art/" + resource + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing authored illustration: " + path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true; importer.mipmapEnabled = false;
                importer.alphaIsTransparency = resource.StartsWith("portrait", StringComparison.Ordinal);
                importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = resource == "title-hero" ? 2048 : 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        static void SetField(Component component, string fieldName, UnityEngine.Object value)
        {
            var field = component.GetType().GetField(fieldName);
            if (field != null && value != null) field.SetValue(component, value);
            EditorUtility.SetDirty(component);
        }

        static void CreateContent()
        {
            const string storyPath = "Assets/Game/Resources/Story/galilee.ink";
            // This standalone chapter is a master story. Explicitly set the official
            // importer override so batch execution does not depend on delayed graph discovery.
            var importer = AssetImporter.GetAtPath(storyPath);
            var importerSettings = new SerializedObject(importer);
            var master = importerSettings.FindProperty("compileAsMasterFileOverride");
            if (master == null) throw new InvalidOperationException("Official Ink master-file setting is missing.");
            if (!master.boolValue)
            {
                master.boolValue = true; importerSettings.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }
            var ink = AssetDatabase.LoadAssetAtPath<Ink.UnityIntegration.InkFile>(storyPath);
            if (ink == null || !ink.isCompiled || ink.hasErrors) throw new InvalidOperationException("Official Ink importer failed to compile galilee.ink.");
            const string path = "Assets/Resources/Content/Galilee.asset";
            var chapter = AssetDatabase.LoadAssetAtPath<PathOfDawn.Authoring.StoryChapter>(path);
            if (chapter == null)
            {
                chapter = ScriptableObject.CreateInstance<PathOfDawn.Authoring.StoryChapter>();
                chapter.story = ink; AssetDatabase.CreateAsset(chapter, path);
            }
        }

        [MenuItem("FirstGame/Validate First Slice")]
        public static void Validate()
        {
            if (Resources.Load<TMP_FontAsset>("Fonts/Korean") == null) throw new InvalidOperationException("Missing Korean font.");
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)) throw new InvalidOperationException("URP is not active.");
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Missing boot scene.");
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var transform in go.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidOperationException("Missing script on " + transform.name);
            var chapter = Resources.Load<PathOfDawn.Authoring.StoryChapter>("Content/Galilee");
            if (chapter == null || chapter.story == null || !chapter.story.isCompiled) throw new InvalidOperationException("Story chapter is missing or invalid.");
            Debug.Log("FIRSTGAME_ASSET_VALIDATION_OK " + Game);
        }
    }
}
