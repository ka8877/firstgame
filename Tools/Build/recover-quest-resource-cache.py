#!/usr/bin/env python3
"""Force reimport explicit retained game resources after removing derived Artifact blobs.

ArtifactDB alone does not regenerate missing imported data on a normal Refresh.
Use only this bounded Editor operation; original import settings are never reset.
"""
from pathlib import Path
from datetime import datetime,timezone
import argparse,hashlib,json,os,shutil,subprocess,sys,uuid
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import capture,verify,digest
p=argparse.ArgumentParser();p.add_argument('attempt');p.add_argument('--game',choices=('PathOfDawn','FantasyCards'),default='PathOfDawn');a=p.parse_args()
assert a.attempt.isalnum()
subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
out=ROOT/'Artifacts/Validation/Quest'/a.game/a.attempt;assert not out.exists();out.mkdir(parents=True)
before=capture(a.game);(out/'inputs-before.json').write_text(json.dumps(before,indent=2)+'\n')
source=ROOT/'Games'/a.game/'Assets/Editor/QuestGeneratedCacheRecovery.cs';meta=Path(str(source)+'.meta');assert not source.exists() and not meta.exists()
guid=uuid.uuid4().hex
code='''using System;
using UnityEditor;
using UnityEngine;
namespace FirstGame.Editor {
 public static class QuestGeneratedCacheRecovery {
  public static void Import() {
   string[] roots={"Assets/Settings","Assets/UniversalRenderPipelineGlobalSettings.asset",
    "Assets/Resources/Art/Gospel3D/TempleMaterials","Assets/Resources/Art/GospelAuthoredProps",
    "Assets/Resources/Art/GospelWell","Assets/Resources/Art/GospelPilgrimage",
    "Assets/Resources/Art/GospelLuminous/World/SoftGlow.png","Assets/Resources/Art/GospelLuminous/Actors/npcs.png",
    "Assets/Resources/Art/GospelChronicle","Assets/Resources/Art/GospelRenewal/Opening",
    "Assets/Resources/Fonts","Assets/Resources/Audio"};
   var flags=ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate|ImportAssetOptions.ImportRecursive;
   foreach(var path in roots)AssetDatabase.ImportAsset(path,flags);
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   var paving=Resources.Load<Texture2D>("Art/Gospel3D/TempleMaterials/paving-albedo");
   var normal=Resources.Load<Texture2D>("Art/Gospel3D/TempleMaterials/paving-normal");
   var actor=Resources.Load<Texture2D>("Art/GospelPilgrimage/Actors/jesus-child-directions");
   if(paving==null||normal==null)throw new InvalidOperationException("Retained paving maps did not regenerate from original source.");
   Debug.Log("FIRSTGAME_QUEST_RESOURCE_CACHE_RECOVERED roots="+roots.Length+" paving="+paving.width+"x"+paving.height);
  }
 }
}
'''
if a.game=='FantasyCards':
 code='''using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TMPro;
namespace FirstGame.Editor {
 public static class QuestGeneratedCacheRecovery {
  public static void Import() {
   string[] roots={"Assets/Resources","Assets/Settings","Assets/TextMesh Pro",
    "Assets/UniversalRenderPipelineGlobalSettings.asset","Assets/Scenes/Boot.unity"};
   var flags=ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate|ImportAssetOptions.ImportRecursive;
   var assetRoots=roots.Where(AssetDatabase.IsValidFolder).SelectMany(root=>AssetDatabase.FindAssets("",new[]{root})).Select(AssetDatabase.GUIDToAssetPath)
    .Concat(roots.Where(path=>!AssetDatabase.IsValidFolder(path))).Where(path=>path.StartsWith("Assets/")).Distinct().ToArray();
   var dependencies=AssetDatabase.GetDependencies(assetRoots,true).Where(path=>path.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(path)).Distinct().OrderBy(path=>path).ToArray();
   foreach(var path in dependencies)AssetDatabase.ImportAsset(path,flags);
   foreach(var path in roots)if(AssetDatabase.IsValidFolder(path)||System.IO.File.Exists(path))AssetDatabase.ImportAsset(path,flags);
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   var display=Resources.Load<TMP_FontAsset>("Fonts/Display");
   var korean=Resources.Load<TMP_FontAsset>("Fonts/Korean");
   var cards=Resources.LoadAll<ScriptableObject>("Data/Cards");
   if(display==null||korean==null||cards.Length<2646)throw new InvalidOperationException("Retained Card fonts or complete generated CardAsset resources did not regenerate.");
   Debug.Log("FIRSTGAME_QUEST_RESOURCE_CACHE_RECOVERED game=FantasyCards roots="+roots.Length+" dependencies="+dependencies.Length+" cards="+cards.Length+" display="+display.name);
  }
 }
}
'''
source.write_text(code);meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
shutil.copyfile(source,out/'QuestGeneratedCacheRecovery.cs');shutil.copyfile(meta,out/'QuestGeneratedCacheRecovery.cs.meta')
record={'status':'prepared','scope':'Explicit ForceSynchronousImport|ForceUpdate|ImportRecursive of only retained runtime resource roots; no importer setting reset, AssetDB deletion or source/original deletion. Temporary Editor entry and metadata are fully retained in evidence then removed.','sourceFingerprint':before['fingerprint'],'generatedEditorSource':str(source.relative_to(ROOT)),'generatedEditorSourceSha256':digest(source),'generatedGuid':guid,'startedUtc':datetime.now(timezone.utc).isoformat(),'game':a.game,'observedReason':'After derived Artifact blobs were reclaimed while preserving ArtifactDB, Gospel ordinary Refresh previously left Resources.Load(paving) null despite original PNG/meta existing; explicit resource/dependency recovery prevents the same stale imported-artifact condition.'};(out/'recovery.json').write_text(json.dumps(record,indent=2)+'\n')
editor=os.environ.get('UNITY_EDITOR','/workspace/tools/unity-editors/6000.3.26f1/Editor/Unity')
command=[editor,'-batchmode','-nographics','-projectPath','Games/'+a.game,'-executeMethod','FirstGame.Editor.QuestGeneratedCacheRecovery.Import','-quit','-logFile',str(out/'import.log')]
record['command']=command;result=1
try:result=subprocess.run(command,cwd=ROOT).returncode
finally:
 assert source.is_file() and digest(source)==record['generatedEditorSourceSha256']
 shutil.copyfile(meta,out/'generated-editor-meta-after.meta');assert 'guid: '+guid in meta.read_text()
 source.unlink();meta.unlink()
 record.update(exitCode=result,finishedUtc=datetime.now(timezone.utc).isoformat())
 try:record['sourceInputsUnchanged']=verify(before)==before['fingerprint']
 except AssertionError:record['sourceInputsUnchanged']=False
 record['status']='recovered' if result==0 and record['sourceInputsUnchanged'] else 'failed-preserved'
 (out/'recovery.json').write_text(json.dumps(record,indent=2)+'\n')
print(json.dumps({k:record[k] for k in ('status','exitCode','sourceFingerprint','sourceInputsUnchanged')}));sys.exit(0 if record['status']=='recovered' else 1)
