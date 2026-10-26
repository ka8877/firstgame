#!/usr/bin/env python3
"""One passive PNG compositor-burst diagnostic around real Space and UI jump input.
No Unity state, time or save data is read/changed. This is diagnostic evidence,
not a passed gameplay suite. Run exclusively with Unity and other browsers.
"""
from pathlib import Path
from types import SimpleNamespace
from datetime import datetime,timezone
import base64,functools,hashlib,http.server,importlib.util,json,threading,time

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('jump_diagnostic_helpers',Path(__file__).with_name('revival-smoke.py'))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

def now():return datetime.now(timezone.utc).isoformat()

def main():
    output=ROOT/'Artifacts/Validation/Modern'/('JumpDiagnostic-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ'));output.mkdir(parents=True)
    build=ROOT/'Artifacts/Builds';plan_path=ROOT/'Tools/Validation/modern-smoke-plans.json';plan=json.loads(plan_path.read_text())['PathOfDawn']
    args=SimpleNamespace(game='PathOfDawn',profile='desktop',build_root=build,plan=plan_path,timeout=180,base_url=None)
    server=http.server.ThreadingHTTPServer(('127.0.0.1',0),functools.partial(m.BASE.LocalHandler,directory=str(build)))
    threading.Thread(target=server.serve_forever,daemon=True).start()
    try:
        with m.BASE.sync_playwright() as p:
            browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=False,proxy=m.browser_proxy(),ignore_default_args=['--mute-audio'],args=['--no-sandbox','--disable-dev-shm-usage','--ozone-platform=x11','--use-gl=angle','--use-angle=gl','--ignore-gpu-blocklist'])
            context=browser.new_context(viewport={'width':1280,'height':720},device_scale_factor=1)
            try:
                page=context.new_page()
                page.add_init_script("""window.__jumpDomEvidence=[];['keydown','keyup'].forEach(type=>document.addEventListener(type,e=>{if(e.code==='Space')window.__jumpDomEvidence.push({type,code:e.code,key:e.key,isTrusted:e.isTrusted,epochMs:Date.now(),hasFocus:document.hasFocus(),activeId:document.activeElement?.id,targetId:e.target?.id});},true));""")
                smoke=m.JourneySmoke(page,args,plan,output)
                smoke.report.update(suite='modern-jump-diagnostic-v1',scope='Minimal ordinary title/new/birth/world route, passive focus/key metadata and original compositor frames only. Not a release pass.')
                page.goto(f'http://127.0.0.1:{server.server_port}/PathOfDawn/Web/',wait_until='domcontentloaded');smoke.loaded()
                for point in ([298/1280,446/720],*[ [1037/1280,646/720] for _ in range(3)]):
                    smoke.input({'type':'click','point':point,'label':'Minimal ordinary route to temple','settleMs':1800})
                page.bring_to_front();page.locator('#unity-canvas').click(position={'x':640,'y':330});page.wait_for_timeout(1800)
                smoke.screenshot('world-baseline')
                session=smoke.raw_capture_session;bursts=[];active=[]
                def frame(event):
                    if active:active[0]['raw'].append({'data':event['data'],'metadata':event['metadata'],'receivedUtc':now(),'receivedEpochMs':time.time()*1000})
                    session.send('Page.screencastFrameAck',{'sessionId':event['sessionId']})
                session.on('Page.screencastFrame',frame)
                focus=lambda:page.evaluate("() => ({hasFocus:document.hasFocus(),visibility:document.visibilityState,activeElement:{tag:document.activeElement?.tagName,id:document.activeElement?.id},trustedSpaceEvents:window.__jumpDomEvidence})")
                for label in ('space','button'):
                    burst={'label':label,'startedUtc':now(),'focusBefore':focus(),'raw':[]};active[:]=[burst]
                    session.send('Page.startScreencast',{'format':'png','maxWidth':1280,'maxHeight':720,'everyNthFrame':1})
                    page.wait_for_timeout(150)
                    burst['inputUtc']=now()
                    if label=='space':page.keyboard.down('Space')
                    else:page.mouse.click(*smoke.point(.9047,.7764),delay=180)
                    page.wait_for_timeout(2000)
                    if label=='space':page.keyboard.up('Space')
                    session.send('Page.stopScreencast');burst['endedUtc']=now();burst['focusAfter']=focus();active.clear()
                    records=[]
                    for index,raw in enumerate(burst.pop('raw')):
                        path=output/(label+'-'+str(index).zfill(3)+'.png');data=base64.b64decode(raw.pop('data'));path.write_bytes(data)
                        records.append({'file':str(path.relative_to(ROOT)),'sha256':hashlib.sha256(data).hexdigest(),'captureKind':'original-full-viewport-compositor-screencast-png',**raw})
                    burst['frames']=records;bursts.append(burst);page.wait_for_timeout(1500)
                smoke.report.update(status='diagnostic-only',finishedUtc=now(),bursts=bursts,domFocus=focus());smoke.persist()
                print(json.dumps({'status':'diagnostic-only','report':str((output/'report.json').relative_to(ROOT)),'frames':{b['label']:len(b['frames']) for b in bursts}},ensure_ascii=False),flush=True)
            finally:context.close();browser.close()
    finally:server.shutdown();server.server_close()

if __name__=='__main__':main()
