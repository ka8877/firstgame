#!/usr/bin/env python3
"""Source-clip browser codec/loop PCM check; no Unity/game controller validation."""
from datetime import datetime,timezone
from pathlib import Path
import hashlib,http.server,importlib.util,json,math,subprocess,sys,threading,time,urllib.parse,urllib.request
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Artifacts/Validation/Renewal/AudioClipLoops';MEDIA=ROOT/'Games/FantasyCards/Assets/StreamingAssets/Audio/ModernCard'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def now():return datetime.now(timezone.utc).isoformat()
spec=importlib.util.spec_from_file_location('renewal_loop_observer_helpers',ROOT/'Tools/Validation/web-smoke.py');BASE=importlib.util.module_from_spec(spec);spec.loader.exec_module(BASE)
CLIPS=[name+'.'+ext for ext in ['ogg','mp3']for name in ['title','battle']]
HTML='''<!doctype html><meta charset="utf-8"><link rel="icon" href="data:,"><title>Source clip loop validation</title><style>body{font:22px sans-serif;background:#15202b;color:#fff;padding:48px}button{padding:20px;font:inherit}audio{display:block;margin-top:24px}</style><h1>Source clip codec / loop output</h1><p id="name"></p><button id="play">Play original local clip</button><audio id="track" controls loop preload="auto"></audio><script>
const file=new URLSearchParams(location.search).get('clip');document.querySelector('#name').textContent=file;const audio=document.querySelector('#track');audio.src='/'+file;let context,source,gain;document.querySelector('#play').onclick=async()=>{if(!context){context=new AudioContext();source=context.createMediaElementSource(audio);gain=context.createGain();gain.gain.value=.7;source.connect(gain);gain.connect(context.destination);}await context.resume();await audio.play();};
</script>'''
class Handler(http.server.SimpleHTTPRequestHandler):
 def __init__(self,*args,**kw):super().__init__(*args,directory=str(MEDIA),**kw)
 def log_message(self,*args):pass
 def do_GET(self):
  if urllib.parse.urlsplit(self.path).path=='/':
   body=HTML.encode();self.send_response(200);self.send_header('Content-Type','text/html; charset=utf-8');self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
  elif urllib.parse.urlsplit(self.path).path.lstrip('/')in CLIPS:
   name=urllib.parse.urlsplit(self.path).path.lstrip('/');path=MEDIA/name;size=path.stat().st_size;requested=self.headers.get('Range')
   if not requested:return super().do_GET()
   # Match static HTTPS hosting: OGG needs the final granule for an exact
   # initial duration; a server without byte ranges can report estimates.
   import re
   match=re.fullmatch(r'bytes=(\d*)-(\d*)',requested)
   if not match:return self.send_error(416)
   start=int(match[1])if match[1]else max(0,size-int(match[2]or 0));end=min(size-1,int(match[2]))if match[1]and match[2]else size-1
   if not 0<=start<=end<size:return self.send_error(416)
   self.send_response(206);self.send_header('Accept-Ranges','bytes');self.send_header('Content-Type','audio/ogg'if name.endswith('.ogg')else'audio/mpeg');self.send_header('Content-Range',f'bytes {start}-{end}/{size}');self.send_header('Content-Length',str(end-start+1));self.end_headers()
   try:
    with path.open('rb')as stream:stream.seek(start);self.wfile.write(stream.read(end-start+1))
   except(BrokenPipeError,ConnectionResetError):pass
  else:self.send_error(404)
def main():
 subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
 if OUT.exists():OUT.rename(OUT.with_name(OUT.name+'.previous-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')))
 OUT.mkdir(parents=True);observer=ROOT/'Tools/Validation/webaudio-observer.js'
 (OUT/'fixture.html').write_text(HTML);(OUT/'recorded-harness.py').write_bytes(Path(__file__).read_bytes());(OUT/'recorded-observer.js').write_bytes(observer.read_bytes())
 report={'status':'running','scope':'Media-only actual browser output of current source OGG/MP3 clips. No Unity player, game music-controller transitions, SFX integration, human listening or public deployment claim. Natural playback from zero through first wrap; never seeks.', 'startedUtc':now(),'gameLoaded':False,'gameControllerUsed':False,'gain':.7,'sources':[],'checks':[],'clips':[],'errors':[],'harnessSha256':sha(Path(__file__)),'observerSha256':sha(observer)}
 def save():(OUT/'report.json').write_text(json.dumps(report,indent=2)+'\n')
 def check(ok,label,detail=None):
  report['checks'].append({'passed':bool(ok),'name':label,'evidence':detail});save()
  if not ok:raise AssertionError(label)
 server=http.server.ThreadingHTTPServer(('127.0.0.1',0),Handler);threading.Thread(target=server.serve_forever,daemon=True).start();base='http://127.0.0.1:'+str(server.server_port)
 try:
  with sync_playwright()as p:
   browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=False,ignore_default_args=['--mute-audio'],args=['--no-sandbox','--disable-dev-shm-usage','--ozone-platform=x11','--use-gl=angle','--use-angle=gl'])
   try:
    for clip in CLIPS:
     source=MEDIA/clip;expected={'file':str(source.relative_to(ROOT)),'bytes':source.stat().st_size,'sha256':sha(source)};report['sources'].append(expected);save()
     with urllib.request.urlopen(base+'/'+clip)as response:body=response.read()
     check(len(body)==expected['bytes']and hashlib.sha256(body).hexdigest()==expected['sha256'],'Actual served clip is byte-exact '+clip)
     context=browser.new_context(viewport={'width':960,'height':540});page=context.new_page();page.add_init_script(path=str(observer));errors=[];page.on('pageerror',lambda e:errors.append(str(e)));page.on('console',lambda m:errors.append(m.text)if m.type=='error'else None)
     try:
      expected_duration=float(subprocess.check_output(['ffprobe','-v','error','-show_entries','format=duration','-of','csv=p=0',str(source)],text=True).strip())
      page.goto(base+'/?clip='+clip,wait_until='domcontentloaded')
      # Early OGG metadata can be a provisional bitrate-based duration. Wait
      # for the complete actual clip buffer before scheduling the natural seam.
      page.wait_for_function("expected => { const a=document.querySelector('audio'); return a.readyState===4 && a.buffered.length>0 && a.buffered.end(a.buffered.length-1)>=expected-.15 && Math.abs(a.duration-expected)<.15; }",arg=expected_duration)
      duration=page.locator('audio').evaluate('e=>e.duration')
      check(10<duration<65,'Bounded decoded clip duration '+clip,duration);page.locator('#play').click();page.wait_for_function("!document.querySelector('audio').paused && document.querySelector('audio').currentTime > .05")
      start=page.locator('audio').evaluate('e=>e.currentTime');check(start<1,'Ordinary click starts clip near zero '+clip,start);print('Observing natural first loop:',clip,duration,flush=True)
      wait_ms=max(0,int((duration-start-1.5)*1000));page.wait_for_timeout(wait_ms)
      snapshots=page.evaluate('() => window.__firstGameAudioEvidence.sampleWindow(3500,50)');path=OUT/(clip+'-wrap-pcm.json');path.write_text(json.dumps(snapshots,indent=2)+'\n')
      frames=[]
      for snapshot in snapshots:
       for ctx in snapshot['contexts']:
        for media in ctx['media']:
         if media['currentSrc']!=base+'/'+clip:continue
         reachable=BASE.audio_reachable(ctx,media['nodeId']);outputs={a for a,b,_,_ in ctx['connections']if a in reachable and b==ctx['destinationNodeId']}
         routes=[r for r in ctx['destinationRoutes']if r['sourceNodeId']in outputs];channels=[c for r in routes for c in r['channels']]
         if channels:frames.append({'time':media['currentTime'],'wallTimeMs':snapshot['wallTimeMs'],'contextTime':ctx['currentTime'],'contextState':ctx['state'],'nodeId':media['nodeId'],'loop':media['loop'],'paused':media['paused'],'rms':math.sqrt(sum(c['rms']**2 for c in channels)/len(channels)),'nonfinite':sum(c['nonfinite']for c in channels),'gainNodes':ctx['gains'],'destinationRoutes':routes})
      wraps=[i for i in range(1,len(frames))if frames[i]['time']<frames[i-1]['time']-.5]
      check(len(frames)>=35 and len(wraps)==1,'One naturally advancing audible-source wrap observed '+clip,{'samples':len(frames),'wrapIndices':wraps})
      idx=wraps[0];center=frames[idx]['wallTimeMs'];seam=[f for f in frames if abs(f['wallTimeMs']-center)<=650]
      check(len(seam)>=14 and len({f['nodeId'] for f in seam})==1 and all(f['loop']and not f['paused']and f['contextState']=='running'and f['nonfinite']==0 and any(abs(g['value']-.7)<1e-5 for g in f['gainNodes']) for f in seam),'Same running loop route and observed 0.7 gain span wrap '+clip)
      check(all(f['rms']>1e-5 for f in seam),'No silent destination PCM window around wrap '+clip,{'minimumRms':min(f['rms']for f in seam),'samples':len(seam)})
      check(frames[-1]['contextTime']>frames[0]['contextTime']+2.5 and not errors and not any(s['errors']for s in snapshots),'Live context advances without observer/browser errors '+clip,errors)
      report['clips'].append({'clip':clip,'duration':duration,'source':expected,'output':{'file':str(path.relative_to(ROOT)),'sha256':sha(path)},'wrapFrames':frames[max(0,idx-2):idx+3],'minimumSeamRms':min(f['rms']for f in seam),'status':'passed'});save()
     finally:context.close()
   finally:browser.close()
  check(all(sha(ROOT/r['file'])==r['sha256']for r in report['sources']),'All current clip source bytes unchanged throughout browser observation')
  report['status']='passed'
 except Exception as e:report['status']='failed';report['errors'].append(str(e))
 finally:server.shutdown();server.server_close();report['finishedUtc']=now();save()
 print(json.dumps({'status':report['status'],'clips':len(report['clips']),'report':str((OUT/'report.json').relative_to(ROOT))}));raise SystemExit(0 if report['status']=='passed'else 1)
if __name__=='__main__':main()
