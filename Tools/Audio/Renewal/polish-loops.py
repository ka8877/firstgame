#!/usr/bin/env python3
"""Rebuild only the documented title/battle music loops from preserved originals.

No runtime audio-graph changes: the circular overlap is baked identically into
native Ogg and Web Ogg/MP3. Original publisher MP3 bytes are never modified.
"""
from pathlib import Path
import array, hashlib, importlib.util, json, shutil, subprocess, tempfile
import numpy as np
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
BASE=HERE.parent/'FmFidelity'
GAME=ROOT/'Games/FantasyCards/Assets'
RATE=44100
RECIPE={
 'title': {'trimStartSeconds':.35,'trimEndSeconds':18.52952380952381,'overlapSeconds':.30},
 'battle': {'trimStartSeconds':.85,'trimEndSeconds':59.35,'overlapSeconds':.35},
}
spec=importlib.util.spec_from_file_location('fidelity',BASE/'generate-loops.py')
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def run(*args):return subprocess.check_output(args,stderr=subprocess.PIPE)
def decode(p):return np.frombuffer(run('ffmpeg','-v','error','-i',str(p),'-ar',str(RATE),'-ac','2','-f','f32le','pipe:1'),dtype='<f4').reshape(-1,2)
def metrics(p):
 a=decode(p);n=round(.01*RATE);v=np.array([np.sqrt(np.mean(a[i:i+n]**2))for i in range(0,len(a),n)]);active=np.flatnonzero(v>=.005)
 # Every 10ms window across a 1s circular seam is analysed, not just endpoint amplitude.
 seam=np.concatenate((a[-RATE//2:],a[:RATE//2]));sr=np.array([np.sqrt(np.mean(seam[i:i+n]**2))for i in range(0,len(seam)-n+1,n)])
 return {'seconds':len(a)/RATE,'peak':float(abs(a).max()),'rms':float(np.sqrt(np.mean(a*a))),'dc':float(a.mean()),'clippedSamples':int(np.count_nonzero(abs(a)>=1)),'finite':bool(np.isfinite(a).all()),'first100msRms':float(np.sqrt(np.mean(a[:RATE//10]**2))),'last100msRms':float(np.sqrt(np.mean(a[-RATE//10:]**2))),'quietIntroSeconds':float(active[0]*.01),'quietOutroSeconds':float((len(v)-1-active[-1])*.01),'minimum10msRmsAcrossLoopSeam':float(sr.min()),'below005RmsWindowsAcrossLoopSeam':int(np.count_nonzero(sr<.005)),'loopBoundarySampleJumpMax':float(abs(a[0]-a[-1]).max())}
def main():
 native=GAME/'Resources/Audio/ModernCard';web=GAME/'StreamingAssets/Audio/ModernCard';history=HERE/'History/before-loop-polish';history.mkdir(parents=True,exist_ok=True)
 manifestPath=BASE/'runtime-manifest.json';manifest=json.loads(manifestPath.read_text());loops=json.loads((BASE/'loop-spec.json').read_text());originals=json.loads((BASE/'source-files.json').read_text())
 for p in (manifestPath,BASE/'SOURCE-NOTICE.txt'):
  dst=history/p.name
  if not dst.exists():shutil.copyfile(p,dst)
 baseline={}
 for scene in RECIPE:
  for p in (native/(scene+'.ogg'),web/(scene+'.mp3')):
   dst=history/p.name
   if not dst.exists():shutil.copyfile(p,dst)
  baseline[scene]=metrics(history/(scene+'.ogg'))
 unchanged={str(p.relative_to(ROOT)):digest(p)for scene in ('lobby','deck')for p in (native/(scene+'.ogg'),web/(scene+'.ogg'),web/(scene+'.mp3'))}
 rows=[]
 with tempfile.TemporaryDirectory(prefix='firstgame-renewal-loop-',dir='/tmp')as td:
  temp=Path(td)
  for scene,recipe in RECIPE.items():
   track=next(t for t in loops['tracks']if t['scene']==scene);source=BASE/'Originals'/track['source'];proof=next(t for t in originals['tracks']if Path(t['file']).name==source.name);assert digest(source)==proof['sha256']
   pcm=base.decode(source,RATE)
   if track['segments']:pcm=base.instrumental_edit(pcm,track['segments'],RATE,loops['joinPreRollSeconds'])
   raw=temp/(scene+'-base.f32');raw.write_bytes(pcm.tobytes());normalized=temp/(scene+'-normalized.wav')
   run('ffmpeg','-v','error','-y','-f','f32le','-ar',str(RATE),'-ac','2','-i',str(raw),'-af',f'loudnorm=I={loops["targetLufs"]}:TP={loops["truePeakDb"]}:LRA=11','-ar',str(RATE),'-c:a','pcm_s16le',str(normalized))
   a=decode(normalized);start=round(recipe['trimStartSeconds']*RATE);end=min(len(a),round(recipe['trimEndSeconds']*RATE));n=round(recipe['overlapSeconds']*RATE);core=a[start:end];assert len(core)>n*3
   # Tail -> head overlap, followed at the loop boundary by the next head sample.
   # Starting after the consumed head prevents either a duplicated introduction
   # or a jump from the end of the overlap back to the start of the head.
   t=np.linspace(0,1,n)[:,None];blend=core[-n:]*np.sqrt(1-t)+core[:n]*np.sqrt(t)
   polished=np.concatenate((core[n:-n],blend)).astype('<f4');assert np.isfinite(polished).all() and abs(polished).max()<1
   raw=temp/(scene+'-loop.f32');raw.write_bytes(polished.tobytes())
   ogg=native/(scene+'.ogg');mp3=web/(scene+'.mp3')
   for path,encoder,args in [(ogg,'libvorbis',['-q:a','6']),(mp3,'libmp3lame',['-q:a','2'])]:
    run('ffmpeg','-v','error','-y','-f','f32le','-ar',str(RATE),'-ac','2','-i',str(raw),'-c:a',encoder,*args,str(path))
   shutil.copyfile(ogg,web/ogg.name)
   checks={'ogg':metrics(ogg),'mp3':metrics(mp3)}
   for m in checks.values():assert m['finite'] and m['clippedSamples']==0 and m['minimum10msRmsAcrossLoopSeam']>=.005
   detail={'scene':scene,'sourceFile':str(source.relative_to(ROOT)),'sourceSha256':proof['sha256'],'basis':'Same hash-checked publisher recording; title uses previously documented instrumental intervals, then normalization before the loop edit.','recipe':recipe,'effectiveFirstSourceSecondsAfterOriginalExcerptAssembly':(start+n)/RATE,'method':'Trim bounded low-energy edges, mix last/first N frames with sqrt(1-t)/sqrt(t), output core[N:-N] followed by mixed tail/head. No source separation, tempo stretch, new melody or speech.','beforeOgg':baseline[scene],'after':checks,'files':[{'file':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':digest(p)}for p in (ogg,web/ogg.name,mp3)]}
   rows.append(detail)
   current=next(t for t in manifest['tracks']if t['scene']==scene);current.update(base.measurements(ogg,RATE));current.update(sha256=digest(ogg),mp3Sha256=digest(mp3),loopEdit=recipe)
   if scene=='battle':current['sourceKind']='complete-public-publisher-recording-with-runtime-loop-edit'
 for p,sha in unchanged.items():assert digest(ROOT/p)==sha
 manifest.update(revision='fm-renewal-audio-2026-10-29',editMethod='Original source/excerpt and loudness pipeline retained. Title and battle now use documented low-energy trim and circular equal-power overlap; lobby/deck unchanged. Complete publisher originals remain preserved.',loopEditRecipe='Tools/Audio/Renewal/polish-loops.py')
 for p in (manifestPath,web/'manifest.json'):p.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
 report={'revision':'fm-renewal-audio-2026-10-29','generator':'Tools/Audio/Renewal/polish-loops.py','generatorSha256':digest(Path(__file__)),'scope':'Actual decoded Ogg and MP3 source-file PCM and circular seam analysis. No human listening, browser output or physical device performance claim.','tracks':rows,'unchangedFiles':unchanged,'history':str(history.relative_to(ROOT))}
 (HERE/'loop-polish-manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps({'tracks':[{ 'scene':r['scene'],'seconds':r['after']['ogg']['seconds'],'quietBefore':r['beforeOgg']['quietIntroSeconds']+r['beforeOgg']['quietOutroSeconds'],'quietAfter':r['after']['ogg']['quietIntroSeconds']+r['after']['ogg']['quietOutroSeconds'],'minimumLoopRms':r['after']['ogg']['minimum10msRmsAcrossLoopSeam']}for r in rows],'unchangedLobbyDeckFiles':len(unchanged)}))
if __name__=='__main__':main()
