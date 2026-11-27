"""Verify the published game's normal controls through the visible Unity canvas."""
import argparse
from functools import partial
from hashlib import file_digest
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from io import BytesIO
import json
from pathlib import Path
import re
import subprocess
from threading import Thread
from time import monotonic

from PIL import Image, ImageOps
from playwright.sync_api import sync_playwright


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *_):
        pass


def canvas(page):
    return page.locator('#unity-canvas')


def point(page, xy):
    box = canvas(page).bounding_box()
    assert box and min(box['width'], box['height']) > 0
    return {'x': round(box['x'] + box['width'] * xy[0], 2), 'y': round(box['y'] + box['height'] * xy[1], 2)}


def actual_canvas(page, png):
    with Image.open(png) as screen:
        box = canvas(page).bounding_box()
        bounds = (round(box['x']), round(box['y']), round(box['x']+box['width']), round(box['y']+box['height']))
        return screen.crop(bounds).convert('RGB').resize((960, 540), Image.Resampling.BILINEAR)


def visible_text(img, bounds, tessdata, psm=7):
    text = ImageOps.autocontrast(img.crop(bounds).convert('L'))
    text = text.resize((text.width*4,text.height*4))
    png = BytesIO()
    text.save(png, 'PNG')
    return subprocess.run(['tesseract','stdin','stdout','--tessdata-dir',str(tessdata),
                           '-l','kor+eng','--psm',str(psm)], input=png.getvalue(), capture_output=True,
                         timeout=15, check=True).stdout.decode().strip()


def screenshot(page, report, name, pixels=None):
    path = Path(report['output']) / f'{name}.png'
    assert not path.exists(), name
    if pixels is None:
        page.screenshot(path=str(path), timeout=55000)
    else:
        path.write_bytes(pixels)
    with path.open('rb') as f:
        report['screens'][name]={'file':str(path),'bytes':path.stat().st_size,'sha256':file_digest(f,'sha256').hexdigest()}
    return path


def wait_title(page):
    page.wait_for_function("['ready','error'].includes(document.querySelector('#unity-canvas').dataset.unityState)", timeout=190000)
    assert canvas(page).get_attribute('data-unity-state') == 'ready', page.locator('#unity-warning').inner_text()
    pause = monotonic()+85
    while monotonic()<pause:
        with Image.open(BytesIO(page.screenshot(timeout=55000))) as img:
            thumb=img.convert('RGB').resize((60,35))
            count=sum(max(rgb)-min(rgb)>22 for rgb in thumb.get_flattened_data())
            if count>180:
                return
        page.wait_for_timeout(600)
    raise AssertionError('The visible title never cleared the engine splash.')


def do_touch(session, page, origin, dest=None, ms=180):
    session.send('Input.dispatchTouchEvent', {'type':'touchStart','touchPoints':[point(page,origin)]})
    try:
        if dest is not None:
            page.wait_for_timeout(100)
            session.send('Input.dispatchTouchEvent', {'type':'touchMove','touchPoints':[point(page,dest)]})
        page.wait_for_timeout(ms)
    finally:
        session.send('Input.dispatchTouchEvent', {'type':'touchEnd','touchPoints':[]})


def distance(page, path, tessdata, touch=False):
    image=actual_canvas(page,path)
    read=visible_text(image,(306,420,655,445),tessdata)
    match=re.search(r'(\d+[.,]\d+)\s*[mM]',read)
    in_reach='대화' in read and '가능' in read
    if touch and not match:
        mobile_distance=visible_text(image,(55,130,126,156),tessdata)
        match=re.search(r'(\d+[.,]\d+)\s*[mM]',mobile_distance)
        if not match:
            # This HUD always draws one decimal digit. The decimal point can
            # disappear at a small canvas size, even when both digits are legible.
            rounded=re.fullmatch(r'\s*(\d{2,3})\s*[mM]\s*',mobile_distance)
            if rounded:
                return int(rounded[1])/10, in_reach
    if not match:
        read=visible_text(image,(23,104,167,146),tessdata,11)
        match=re.search(r'(\d+[.,]\d+)\s*[mM]',read)
    if not match and not touch:
        # The small field minimap remains readable when the Korean control hint fades.
        digits = ImageOps.autocontrast(image.crop((49,110,87,127)).convert('L')).resize((190,85),Image.Resampling.LANCZOS)
        data = BytesIO()
        digits.save(data,'PNG')
        text = subprocess.run(['tesseract','stdin','stdout','--tessdata-dir',str(tessdata),'-l','eng','--psm','7',
                               '-c','tessedit_char_whitelist=0123456789.,mM'], input=data.getvalue(),
                              capture_output=True,check=True,timeout=15).stdout.decode().strip()
        match = re.fullmatch(r'(\d{1,2}[.,]\d)\s*[mM]?',text)
    return (float(match[1].replace(',','.')) if match else None), in_reach


def play(page, browser, profile, report, tessdata):
    touch = profile == 'mobile'
    session=page.context.new_cdp_session(page) if touch else None
    def press(xy):
        if touch:
            do_touch(session,page,xy)
        else:
            page.mouse.click(**point(page,xy),delay=180)
        page.wait_for_timeout(190)
    def walk():
        if touch:
            do_touch(session,page,(.09375,.79306),(.09375,.697),ms=1400)
        else:
            page.keyboard.down('w')
            try:
                page.wait_for_timeout(1300)
            finally:
                page.keyboard.up('w')
        page.wait_for_timeout(400)
    def wait_for_joseph(name):
        last_text=''
        pixels=None
        deadline=monotonic()+100
        while monotonic()<deadline:
            pixels=page.screenshot(timeout=52000)
            frame=actual_canvas(page,BytesIO(pixels))
            last_text=visible_text(frame,(182,362,867,435),tessdata,11)
            words=re.sub(r'\s+','',last_text)
            if '예루살렘' in words or ('요셉' in words and any(word in words for word in ('함께','유월절','채비'))):
                screenshot(page,report,name,pixels)
                return last_text
            page.wait_for_timeout(650)
        if pixels is not None:
            screenshot(page,report,name+'-last-visible',pixels)
        raise AssertionError(f'{name} never showed the resumed Joseph dialogue: {last_text!r}')

    screenshot(page,report,'title')
    press((.18086,.71181))
    screenshot(page,report,'opening')
    for number in range(51):
        press((.832,.903))
        if number in (9,43,50):
            print(profile,'opening turns',number+1,flush=True)
    panel=screenshot(page,report,'child-chapter')
    chapter_frame=actual_canvas(page,panel)
    chapter=visible_text(chapter_frame,(41,145,530,221),tessdata,11)
    subtitle=visible_text(chapter_frame,(45,285,397,327),tessdata,11)
    action=visible_text(chapter_frame,(68,424,317,472),tessdata,7)
    named_chapter=any(word in chapter+subtitle for word in ('소년','유월절'))
    enter_chapter='이야기속으로' in re.sub(r'\s+','',action)
    assert named_chapter or (enter_chapter and any(word in subtitle for word in ('예루살렘','열두 살'))), (chapter,subtitle,action)
    report['childChapterText']={'title':chapter,'subtitle':subtitle,'action':action}
    press((.198,.8292))
    first=screenshot(page,report,'field')
    metres,reach=distance(page,first,tessdata,touch)
    assert metres is not None and 3.5<=metres<=5.5, metres
    report['metresToJoseph']=[metres]
    for index in range(1,8):
        if reach or (metres is not None and metres<=2.15):
            break
        walk()
        frame=screenshot(page,report,f'walk-{index}')
        metres,reach=distance(page,frame,tessdata,touch)
        report['metresToJoseph'].append(metres)
        print(profile,'distance',metres,'interaction available',reach,flush=True)
    assert reach or (metres is not None and metres<=2.15), report['metresToJoseph']
    if touch: press((.917,.909))
    else:
        page.keyboard.press('e',delay=160)
        page.wait_for_timeout(350)
    report['confirmedJosephDialogueText']=wait_for_joseph('joseph-dialogue')
    # Reading the Scripture book and returning to the real encounter exercises the same persistent journey.
    press((.902,.075))
    scripture=screenshot(page,report,'scripture-book')
    book_text=visible_text(actual_canvas(page,scripture),(0,0,960,220),tessdata,11)
    assert '성경' in book_text or '말씀' in book_text, book_text
    press((.86,.928))
    wait_for_joseph('dialogue-return')
    page.wait_for_timeout(1700)
    page.reload(wait_until='domcontentloaded',timeout=35000)
    wait_title(page)
    press((.18086,.82292))
    report['confirmedAfterReload']=wait_for_joseph('continued-dialogue')
    report['normalInput']='touch events' if touch else 'keyboard and mouse'
    if session:
        session.detach()


def main():
    p=argparse.ArgumentParser()
    p.add_argument('player',type=Path)
    p.add_argument('output',type=Path)
    p.add_argument('profile',choices=('desktop','mobile'))
    p.add_argument('--tessdata',type=Path,default=Path('/workspace/tools/tesseract-best'))
    p.add_argument('--base-url')
    p.add_argument('--title-only',action='store_true')
    a=p.parse_args()
    a.output.mkdir(parents=True)
    report={'status':'running','profile':a.profile,'output':str(a.output),'pageErrors':[],'requestFailures':[],
            'screens':{},'responses':{}}
    server=None
    endpoint=a.base_url
    if not endpoint:
        server=ThreadingHTTPServer(('127.0.0.1',0),partial(QuietHandler,directory=a.player))
        Thread(target=server.serve_forever,daemon=True).start()
        endpoint=f'http://127.0.0.1:{server.server_port}/'
    try:
        with sync_playwright() as automation:
            browser=automation.chromium.launch(executable_path='/usr/bin/chromium',headless=False,
                args=['--no-sandbox','--disable-dev-shm-usage','--ozone-platform=x11','--use-gl=angle','--use-angle=gl','--ignore-gpu-blocklist'])
            if a.profile=='mobile':
                options=dict(viewport={'width':844,'height':390},is_mobile=True,has_touch=True,device_scale_factor=1,
                             user_agent=automation.devices['Pixel 7']['user_agent'])
            else:
                options=dict(viewport={'width':960,'height':540},device_scale_factor=1)
            context=browser.new_context(**options)
            page=context.new_page()
            page.on('pageerror',lambda e:report['pageErrors'].append(str(e)[:1000]))
            page.on('requestfailed',lambda req:report['requestFailures'].append([req.url,req.failure]))
            page.on('response',lambda resp:report['responses'].update({resp.url:resp.status}))
            page.goto(endpoint,wait_until='domcontentloaded',timeout=35000)
            wait_title(page)
            if a.title_only: screenshot(page,report,'title')
            else: play(page,browser,a.profile,report,a.tessdata)
            assert not report['pageErrors'],report['pageErrors']
            assert page.locator('#unity-warning .error').count()==0
            paths={key:value for key,value in report['responses'].items() if '/Build/' in key}
            assert len(paths)>=4 and all(value in (200,206,304) for value in paths.values()),paths
            context.close()
            browser.close()
        report['status']='passed'
    except Exception as error:
        report.update(status='failed',error=repr(error))
        raise
    finally:
        if server:
            server.shutdown()
            server.server_close()
        (a.output/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
        print(a.profile,report['status'],flush=True)


if __name__=='__main__': main()
