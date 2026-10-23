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
    // Generated per independent project by Tools/Build/materialize.py.
    // All game rules and authored content live outside this bootstrap.
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string Game = "FantasyCards";
        const string AppType = "FantasyCards.Presentation.FantasyCardsApp, FantasyCards.Presentation";
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
                foreach (var folder in new[] { "Assets/Scenes", "Assets/Settings", "Assets/Settings/Materials", "Assets/Resources/Fonts", "Assets/Resources/Models", "Assets/Resources/Data/Cards", "Assets/Resources/Content" })
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
                CreateModels();
                CreateContent();
                var scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var type = Type.GetType(AppType, true);
                var app = UnityEngine.Object.FindFirstObjectByType(type) as Component;
                if (app == null) app = new GameObject(Game + " Bootstrap").AddComponent(type);
                SetField(app, "KoreanFont", font);
                if (Game == "PathOfDawn")
                {
                    SetField(app, "Chapter", AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/Content/Galilee.asset"));
                    SetField(app, "EnvironmentPrefab", Resources.Load<GameObject>("Models/GalileeShore"));
                    SetField(app, "JesusPrefab", Resources.Load<GameObject>("Models/JesusProxy"));
                    SetField(app, "TravelerPrefab", Resources.Load<GameObject>("Models/TravelerProxy"));
                    SetField(app, "MiriamPrefab", Resources.Load<GameObject>("Models/MiriamProxy"));
                }
                else if (GameObject.Find("StarfallArena") == null)
                {
                    var prefab = Resources.Load<GameObject>("Models/StarfallArena");
                    if (prefab != null) { var arena = (GameObject)PrefabUtility.InstantiatePrefab(prefab); arena.name = "StarfallArena"; }
                }
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
            foreach (var modelPath in Directory.GetFiles(folder, "*.fbx"))
            {
                string path = modelPath.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                string prefabPath = "Assets/Resources/Models/" + name + ".prefab";
                if (File.Exists(prefabPath)) continue;
                bool character = name.EndsWith("Proxy", StringComparison.Ordinal);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importAnimation = character;
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
                            if (material == null)
                            {
                                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                                material.name = materialName;
                                material.color = old != null && old.HasProperty("_Color") ? old.color : new Color(.65f, .60f, .48f);
                                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
                                AssetDatabase.CreateAsset(material, materialPath);
                            }
                            materials[i] = material;
                        }
                        renderer.sharedMaterials = materials;
                    }
                    if (character)
                    {
                        var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                        if (clips.Length == 0) throw new InvalidOperationException("Missing Blender idle take: " + name);
                        var clip = UnityEngine.Object.Instantiate(clips.FirstOrDefault(c => c.name.Contains("Idle")) ?? clips[0]);
                        clip.name = name + " Idle";
                        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);
                        AssetDatabase.CreateAsset(clip, "Assets/Settings/" + name + "Idle.anim");
                        var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/Settings/" + name + "Idle.controller");
                        controller.AddMotion(clip); animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                    }
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
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
            const string atlasPath = "Assets/Resources/Art/card-atlas.png";
            var atlasImporter = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (atlasImporter == null) throw new InvalidOperationException("Card illustration texture importer is missing.");
            if (atlasImporter.textureShape != TextureImporterShape.Texture2D || atlasImporter.textureType != TextureImporterType.Default)
            {
                atlasImporter.textureShape = TextureImporterShape.Texture2D;
                atlasImporter.textureType = TextureImporterType.Default;
                atlasImporter.mipmapEnabled = false;
                atlasImporter.npotScale = TextureImporterNPOTScale.None;
                atlasImporter.sRGBTexture = true;
                atlasImporter.wrapMode = TextureWrapMode.Clamp;
                atlasImporter.SaveAndReimport();
            }
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            if (atlas == null) throw new InvalidOperationException("Card illustrations must import as a 2D texture.");
            foreach (var definition in FantasyCards.Core.CardCatalog.All)
            {
                string path = "Assets/Resources/Data/Cards/" + definition.id + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<FantasyCards.Data.CardAsset>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<FantasyCards.Data.CardAsset>();
                    asset.stableId = definition.id; asset.displayName = definition.name; asset.description = definition.text;
                    asset.kind = definition.kind; asset.element = definition.element; asset.cost = definition.cost;
                    asset.power = definition.attack; asset.health = definition.health; asset.coinBonus = definition.coinBonus;
                    asset.effect = definition.effect; asset.magnitude = definition.magnitude;
                    AssetDatabase.CreateAsset(asset, path);
                }
                if (asset.illustration == null)
                {
                    int index = Array.IndexOf(FantasyCards.Core.CardCatalog.All, definition);
                    float sizeX = atlas.width / 4f, sizeY = atlas.height / 4f;
                    var sprite = Sprite.Create(atlas, new Rect(index % 4 * sizeX, (3 - index / 4) * sizeY, sizeX, sizeY), new Vector2(.5f,.5f));
                    sprite.name = definition.id + " Illustration"; AssetDatabase.AddObjectToAsset(sprite, asset); asset.illustration = sprite;
                }
                EditorUtility.SetDirty(asset);
            }
            if (!File.Exists("Assets/Resources/Data/Rules.asset"))
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FantasyCards.Data.RuleSetAsset>(), "Assets/Resources/Data/Rules.asset");
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
            var cards = Resources.LoadAll<FantasyCards.Data.CardAsset>("Data/Cards");
            if (cards.Length != 16 || cards.Any(card => card.illustration == null)) throw new InvalidOperationException("Expected sixteen card assets with connected illustrations.");
            if (Resources.Load<Texture2D>("Art/card-atlas") == null) throw new InvalidOperationException("Card atlas is not a runtime 2D texture.");
            Debug.Log("FIRSTGAME_ASSET_VALIDATION_OK " + Game);
        }
    }
}
