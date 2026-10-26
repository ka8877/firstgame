#!/usr/bin/env python3
"""Inspect decoded shipping audio. Does not claim hardware listening or browser playback."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
production=json.loads((ROOT/'Tools/Audio/production.json').read_text())
report={'status':'passed','checkedUtc':datetime.now(timezone.utc).isoformat(),'scope':'Shipping Ogg decode, musical duration, channel/signal/headroom and loop-edge measurements. Browser interaction and device listening are separate.','generatorSha256':hashlib.sha256((ROOT/'Tools/Audio/compose-soundtracks.py').read_bytes()).hexdigest(),'soundfontSha256':production['soundfontSha256'],'tracks':[]}
for track in production['tracks']:
 p=ROOT/track['file']
 meta=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_streams','-of','json',str(p)]))['streams'][0]
 data=subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-f','f32le','-acodec','pcm_f32le','-ar','44100','-ac','2','-'])
 samples=np.frombuffer(data,dtype='<f4').reshape(-1,2)
 duration=len(samples)/44100;peak=float(np.max(np.abs(samples)));rms=float(np.sqrt(np.mean(samples*samples)))
 window=4410;windows=[float(np.sqrt(np.mean(x*x))) for x in np.array_split(samples[:len(samples)//window*window],len(samples)//window)]
 begin=float(np.sqrt(np.mean(samples[:2205]**2)));end=float(np.sqrt(np.mean(samples[-2205:]**2)))
 seam=float(np.max(np.abs(samples[-1]-samples[0])))
 assert meta['codec_name']=='vorbis' and int(meta['channels'])==2 and int(meta['sample_rate'])==44100
 assert 90<=duration<=120 and abs(duration-track['seconds'])<.03
 assert np.isfinite(samples).all() and 0.1<peak<.95 and rms>.025
 assert min(windows)>.001 and begin>.001 and end>.001 and seam<.01
 r={'file':track['file'],'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'codec':meta['codec_name'],'sampleRate':44100,'channels':2,'decodedDurationSeconds':round(duration,6),'decodedPeakDbFS':round(20*np.log10(peak),3),'decodedRmsDbFS':round(20*np.log10(rms),3),'minimum100msRmsDbFS':round(20*np.log10(min(windows)),3),'first50msRmsDbFS':round(20*np.log10(begin),3),'last50msRmsDbFS':round(20*np.log10(end),3),'edgeSampleStep':round(seam,8),'clippedSamples':int(np.count_nonzero(np.abs(samples)>=1))}
 assert r['sha256']==track['sha256']
 report['tracks'].append(r)
(ROOT/'Tools/Audio/verification.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
