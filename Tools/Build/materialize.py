#!/usr/bin/env python3
"""Materialize checked-in bootstrap scripts/scenes and missing deterministic .meta IDs.

Never overwrite an existing scene, project-specific setup script, or meta. Unity imports/generates engine-owned assets
(fonts, renderers, prefabs, authoring data) through ProjectSetup, not guessed YAML.
"""
from pathlib import Path
import argparse
import json
import uuid

ROOT = Path(__file__).resolve().parents[2]


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, "https://firstgame.local/" + str(path.relative_to(ROOT)).replace("\\", "/")).hex


def meta(path):
    target = Path(str(path) + ".meta")
    if target.exists():
        return
    value = "fileFormatVersion: 2\nguid: " + guid(path) + "\n"
    if path.is_dir():
        value += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif path.suffix == ".cs":
        value += "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif path.suffix == ".asmdef":
        value += "AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    # Model, font, texture and scripted importers populate settings on first import.
    target.write_text(value, encoding="utf-8")


CARD_CONTENT = '''            const string atlasPath = "Assets/Resources/Art/card-atlas.png";
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
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FantasyCards.Data.RuleSetAsset>(), "Assets/Resources/Data/Rules.asset");'''

STORY_CONTENT = '''            const string storyPath = "Assets/Game/Resources/Story/galilee.ink";
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
            }'''


def materialize():
    for game in ["FantasyCards", "PathOfDawn"]:
        project = ROOT / "Games" / game
        editor = project / "Assets/Editor"
        editor.mkdir(parents=True, exist_ok=True)
        app_type = game + ".Presentation." + game + "App"
        content = CARD_CONTENT if game == "FantasyCards" else STORY_CONTENT
        validation = ('            var cards = Resources.LoadAll<FantasyCards.Data.CardAsset>("Data/Cards");\n            if (cards.Length != 16 || cards.Any(card => card.illustration == null)) throw new InvalidOperationException("Expected sixteen card assets with connected illustrations.");\n            if (Resources.Load<Texture2D>("Art/card-atlas") == null) throw new InvalidOperationException("Card atlas is not a runtime 2D texture.");'
                      if game == "FantasyCards" else '            var chapter = Resources.Load<PathOfDawn.Authoring.StoryChapter>("Content/Galilee");\n            if (chapter == null || chapter.story == null || !chapter.story.isCompiled) throw new InvalidOperationException("Story chapter is missing or invalid.");')
        replacements = {"__GAME__": game, "__APP_TYPE__": app_type, "__APP_ASSEMBLY__": game + ".Presentation", "__CREATE_CONTENT__": content, "__VALIDATE_CONTENT__": validation}
        for name in ["ProjectSetup.cs", "BuildCommands.cs"]:
            # Each game's art import/setup evolves independently after bootstrap.
            if name == "ProjectSetup.cs" and (editor / name).exists():
                continue
            data = (ROOT / "Tools/Build" / (name + ".template")).read_text()
            for key, value in replacements.items():
                data = data.replace(key, value)
            (editor / name).write_text(data, encoding="utf-8")
        web_template = ROOT / "Tools/Web/unity-template.html"
        if web_template.exists():
            template_dir = project / "Assets/WebGLTemplates/FirstGame"
            template_dir.mkdir(parents=True, exist_ok=True)
            (template_dir / "index.html").write_text(web_template.read_text(), encoding="utf-8")
        refs = ["Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime", "Unity.TextMeshPro", "Unity.ugui", game + ".Core", game + ".Presentation"]
        refs += ["FantasyCards.Data"] if game == "FantasyCards" else ["Ink", "Ink-Libraries", "PathOfDawn.Authoring"]
        (editor / (game + ".Editor.asmdef")).write_text(json.dumps({"name": game + ".Editor", "references": refs, "includePlatforms": ["Editor"]}, indent=2) + "\n")
        scene = project / "Assets/Scenes/Boot.unity"
        scene.parent.mkdir(parents=True, exist_ok=True)
        app_file = project / "Assets/Game/Presentation" / (game + "App.cs")
        if not scene.exists():
            scene.write_text('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 1001}
  - component: {fileID: 1002}
  m_Layer: 0
  m_Name: ''' + game + ''' Bootstrap
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &1001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
--- !u!114 &1002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ''' + guid(app_file) + ''', type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {fileID: 1001}
''')
        settings = project / "ProjectSettings/EditorBuildSettings.asset"
        if not settings.exists():
            settings.write_text('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes:
  - enabled: 1
    path: Assets/Scenes/Boot.unity
    guid: ''' + guid(scene) + '''
  m_configObjects: {}
''')


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--meta-only", action="store_true")
    args = parser.parse_args()
    if not args.meta_only:
        materialize()
    for project in (ROOT / "Games").iterdir():
        assets = project / "Assets"
        for path in assets.rglob("*.meta"):
            asset = Path(str(path)[:-5])
            if not asset.exists() and ("guid: " + guid(asset)) in path.read_text():
                path.unlink()  # Only our generated metadata for an asset subsequently removed.
        for path in sorted(assets.rglob("*")):
            if path.suffix != ".meta" and not path.name.startswith("."):
                meta(path)
    print("Bootstrap sources and missing asset metadata materialized; existing scenes/metas preserved.")
