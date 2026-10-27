using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FirstGame.Editor
{
    /// <summary>Persist actual stone/wood/rope/water variants used by the modeled well and travel lamp.</summary>
    public static class GospelWellSetup
    {
        const string Folder="Assets/Resources/Art/GospelWell";
        [Serializable] sealed class Palette { public Entry[] materials; public string[] models; }
        [Serializable] sealed class Entry
        {
            public string name,baseMap,normalMap;
            public float[] baseColor;
            public float normalStrength,smoothness,metallic;
        }
        public static void Prepare()
        {
            string specification=Path.GetFullPath("../../Tools/Art/GospelWell/material-spec.json");
            if(!Directory.Exists(Folder)||!File.Exists(specification))throw new InvalidOperationException("Gospel well source/specification missing.");
            var palette=JsonUtility.FromJson<Palette>(File.ReadAllText(specification));
            if(palette?.materials==null||palette.models==null)throw new InvalidOperationException("Invalid well material specification.");
            Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new InvalidOperationException("Well materials require URP Lit.");
            foreach(var entry in palette.materials)
            {
                if(entry.baseColor==null||entry.baseColor.Length!=4)throw new InvalidOperationException("Invalid well color: "+entry.name);
                var albedo=Texture(entry.baseMap,false);var normal=Texture(entry.normalMap,true);
                string path=Folder+"/Materials/"+entry.name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
                material.name=entry.name;material.shader=shader;
                material.SetColor("_BaseColor",new Color(entry.baseColor[0],entry.baseColor[1],entry.baseColor[2],entry.baseColor[3]));
                material.SetTexture("_BaseMap",albedo);material.SetTexture("_BumpMap",normal);
                material.SetFloat("_BumpScale",entry.normalStrength);material.SetFloat("_Smoothness",entry.smoothness);material.SetFloat("_Metallic",entry.metallic);
                material.SetFloat("_Surface",0);material.SetFloat("_AlphaClip",0);material.DisableKeyword("_ALPHATEST_ON");
                if(normal!=null)material.EnableKeyword("_NORMALMAP");else material.DisableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
            }
            foreach(string name in palette.models)
            {
                string path=Folder+"/"+name+".fbx";
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null)throw new InvalidOperationException("Cannot import well model: "+path);
                importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
                importer.importNormals=ModelImporterNormals.Import;importer.isReadable=false;
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
                foreach(var embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    var entry=palette.materials.FirstOrDefault(e=>embedded.name==e.name||embedded.name.EndsWith("_"+e.name,StringComparison.Ordinal));
                    if(entry==null)throw new InvalidOperationException("Unknown well material: "+embedded.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),embedded.name),AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+entry.name+".mat"));
                }
                // Keep the FBX axis/file-unit transform; runtime preserves it before measured sizing.
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }
        static Texture2D Texture(string resource,bool normal)
        {
            if(string.IsNullOrEmpty(resource))return null;
            if(resource.Contains("..")||!resource.StartsWith("Art/",StringComparison.Ordinal))throw new InvalidOperationException("Invalid well texture resource.");
            string folder="Assets/Resources/"+Path.GetDirectoryName(resource).Replace('\\','/');
            var files=Directory.GetFiles(folder,Path.GetFileName(resource)+".*").Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)).ToArray();
            if(files.Length!=1)throw new InvalidOperationException("Missing or ambiguous well texture: "+resource);
            string path=files[0].Replace('\\','/');
            // Existing limestone maps already have the temple's established convention.
            // Only the newly authored cedar maps receive a new import policy.
            if(path.StartsWith(Folder+"/",StringComparison.Ordinal))
            {
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null)throw new InvalidOperationException("Cannot import well texture: "+path);
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=!normal;importer.flipGreenChannel=false;importer.maxTextureSize=512;
                importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=4;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;
                var web=importer.GetPlatformTextureSettings("WebGL");web.name="WebGL";web.overridden=true;web.maxTextureSize=512;
                web.format=TextureImporterFormat.ASTC_4x4;web.textureCompression=TextureImporterCompression.CompressedHQ;web.compressionQuality=100;
                importer.SetPlatformTextureSettings(web);importer.SaveAndReimport();
            }
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture==null)throw new InvalidOperationException("Missing imported well texture: "+path);
            return texture;
        }
    }
}
