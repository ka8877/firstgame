#!/usr/bin/env python3
"""Original deterministic tactile-card/coin/stat layers, with no gameplay RNG."""
from pathlib import Path
import importlib.util, hashlib, json, wave
import numpy as np
from scipy.signal import butter, sosfilt
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
OUT=ROOT/'Games/FantasyCards/Assets/Resources/Audio/FmRenewal'
spec=importlib.util.spec_from_file_location('base',HERE.parent/'Restoration/render-sfx.py')
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
RATE=44100
DURATIONS={'coin-head':.50,'coin-tail':.38,'stat-up':.58,'stat-down':.48,'card-land':.40,'rare-reveal':1.24,'click':.16,'card-draw':.31,'card-shuffle':1.33}
def create(cue,index):
 s=base.Sound(DURATIONS[cue],20261029+index*773)
 peak=.54
 if cue.startswith('coin-'):
  bright=cue=='coin-head';pitch=1540 if bright else 980
  for onset,gain in ((0,.56),(.075,.27),(.14,.13)):
   s.noise(onset,.016,gain*.35,1300,9000)
   for ratio,weight in ((1,1),(1.57,.53),(2.31,.31),(3.12,.11)):
    s.tone(pitch*ratio,onset,.095 if bright else .053,gain*weight)
  if bright:s.tone(2936,.19,.10,.08)
 elif cue.startswith('stat-'):
  up=cue=='stat-up';notes=(392,493.88,587.33) if up else (392,349.23,293.66)
  s.noise(duration=.19,gain=.11,low=1800,high=7000)
  for i,hz in enumerate(notes):s.struck(hz,i*.067,.13,.30)
  peak=.42
 elif cue=='card-land':
  s.paper(duration=.105,gain=.45);s.noise(.04,.09,.28,90,1800)
  s.tone(145,.043,.043,.65,80);s.struck(310,.05,.025,.11)
 elif cue=='rare-reveal':
  s.noise(duration=.48,gain=.28,low=500,high=9000)
  for i,hz in enumerate((392,587.33,783.99,1174.66)):
   s.struck(hz,.10+i*.072,.24,.25)
  s.tone(98,.1,.19,.32);peak=.58
 elif cue=='click':
  s.noise(duration=.023,gain=.6,low=500,high=5000)
  s.struck(360,decay=.025,gain=.38);s.tone(168,decay=.018,gain=.29);peak=.40
 elif cue=='card-draw':
  s.paper(duration=.14,gain=.43);s.paper(.06,.11,.18)
  s.noise(.12,.032,.27,250,3500);s.tone(228,.13,.021,.25);peak=.48
 elif cue=='card-shuffle':
  for i in range(18):
   onset=.018+i*.061;s.paper(onset,.036+(i%3)*.009,.27+(i%5)*.025)
   s.tone(138+(i%4)*28,onset,.009,.11)
  s.paper(1.11,.12,.4);s.tone(138,1.13,.03,.48);peak=.55
 dry=sosfilt(butter(2,65,btype='highpass',fs=RATE,output='sos'),s.x)
 left,right=dry.copy(),dry.copy()
 for delay,gain in ((.014,.10),(.033,.045)):
  n=round(delay*RATE);left[n:]+=dry[:-n]*gain
  n+=round(.005*RATE);right[n:]+=dry[:-n]*gain
 audio=np.stack((left,right),axis=1)
 edge=round(.005*RATE);audio[:edge]*=np.linspace(0,1,edge)[:,None]
 tail=round(.045*RATE);audio[-tail:]*=np.linspace(1,0,tail)[:,None]
 audio*=peak/max(abs(audio).max(),1e-8)
 return np.rint(audio*32767).astype('<i2')
def main():
 OUT.mkdir(parents=True,exist_ok=True);base.meta(OUT,folder=True)
 rows=[]
 for i,cue in enumerate(DURATIONS):
  pcm=create(cue,i);p=OUT/(cue+'.wav')
  with wave.open(str(p),'wb') as w:w.setnchannels(2);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(pcm.tobytes())
  base.meta(p);m=Path(str(p)+'.meta');m.write_text(m.read_text().replace('Tools/Audio/Restoration/render-sfx.py','Tools/Audio/Renewal/render-sfx.py'))
  a=pcm.astype(float)/32768
  assert np.isfinite(a).all() and abs(a).max()<.6 and abs(a.mean())<.001
  assert (pcm[0]==0).all() and (pcm[-1]==0).all() and np.sqrt(np.mean(a*a))>.01
  rows.append({'cue':cue,'file':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'seconds':len(pcm)/RATE,'peak':float(abs(a).max()),'rms':float(np.sqrt(np.mean(a*a))),'dc':float(a.mean()),'clippedSamples':0,'endpointsZero':True})
 result={'revision':'fm-renewal-2026-10-29','source':'Original deterministic offline synthesis. No recovered FM sound files, voices or third-party samples.','generator':'Tools/Audio/Renewal/render-sfx.py','cues':rows,'scope':'Decoded PCM file analysis only; actual in-game output is separately verified by Release.'}
 (HERE/'sfx-manifest.json').write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n')
 print(json.dumps({'cues':len(rows),'bytes':sum(x['bytes'] for x in rows),'allChecksPassed':True}))
if __name__=='__main__':main()
