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


def read_distance(image, tessdata, touch=False):
    read=visible_text(image,(306,420,655,445),tessdata)
    match=re.search(r'(\d+[.,]\d+)\s*[mM]',read)
    in_reach='대화' in read and '가능' in read
    if touch:
        # The visible interaction control changes to mint when the target enters reach.
        control = image.crop((23,132,99,151))
        colours = control.getcolors(control.width * control.height)
        mint = sum(count for count, (r,g,b) in colours if g > r+7 and g > b+1 and g > 110 and b > 94)
        in_reach = in_reach or mint > 75
    if touch and not match:
        mobile_distance=visible_text(image,(33,121,121,156),tessdata)
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
    if not match:
        # Both minimaps display a numeric readout. Crop only the digits so OCR
        # does not combine the Korean speaker name with the metre unit.
        if touch:
            digits = image.crop((59,127,105,151)).resize((240,108),Image.Resampling.LANCZOS)
            digits = ImageOps.autocontrast(digits.convert('L'))
        else:
            digits = ImageOps.autocontrast(image.crop((49,110,87,127)).convert('L')).resize((190,85),Image.Resampling.LANCZOS)
        data = BytesIO()
        digits.save(data,'PNG')
        text = subprocess.run(['tesseract','stdin','stdout','--tessdata-dir',str(tessdata),'-l','eng','--psm','7',
                               '-c','tessedit_char_whitelist=0123456789.,mM'], input=data.getvalue(),
                              capture_output=True,check=True,timeout=15).stdout.decode().strip()
        match = re.fullmatch(r'(\d{1,2}[.,]\d)\s*[mM]?',text)
        if touch and not match:
            compact = re.fullmatch(r'(\d{2,3})\s*[mM]', text)
            if compact:
                return int(compact[1])/10, in_reach
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
    def walk(frame):
        if touch:
            hint = visible_text(frame,(303,408,656,442),tessdata) if frame else ''
            x = int('오른' in hint) - int('왼' in hint)
            y = -1 if '아래' in hint else int('위' in hint or x == 0)
            length = max(1,(x*x+y*y)**.5)
            do_touch(session,page,(.09375,.79306),(.09375+.0235*x/length,.79306-.042*y/length),ms=620)
        else:
            page.keyboard.down('w')
            try:
                page.wait_for_timeout(1300)
            finally:
                page.keyboard.up('w')
        page.wait_for_timeout(400)
    def wait_for_frame(name, read_state):
        deadline = monotonic() + 90
        pixels, observed = None, None
        while monotonic() < deadline:
            pixels = page.screenshot(timeout=52000)
            frame = actual_canvas(page, BytesIO(pixels))
            matches, observed = read_state(frame)
            if matches:
                screenshot(page, report, name, pixels)
                return observed
            page.wait_for_timeout(450)
        if pixels is not None:
            screenshot(page, report, name + '-last-visible', pixels)
        raise AssertionError(f'{name} never became visible: {observed!r}')

    def child_chapter(frame):
        title = visible_text(frame, (41,145,530,221), tessdata, 11)
        subtitle = visible_text(frame, (45,285,397,327), tessdata, 11)
        action = visible_text(frame, (68,424,317,472), tessdata)
        named = any(word in title + subtitle for word in ('소년','유월절'))
        enter = '이야기속으로' in re.sub(r'\s+', '', action)
        return named or (enter and any(word in subtitle for word in ('예루살렘','열두 살'))), {
            'title': title, 'subtitle': subtitle, 'action': action}

    def opening_field(frame):
        metres, reach = read_distance(frame, tessdata, touch)
        return metres is not None and 3.5 <= metres <= 5.5, (metres, reach)

    def joseph_dialogue(frame):
        text = visible_text(frame, (182,362,867,435), tessdata, 11)
        words = re.sub(r'\s+', '', text)
        matches = '예루살렘' in words or ('요셉' in words and any(word in words for word in ('함께','유월절','채비')))
        return matches, text

    def scripture_book(frame):
        heading = visible_text(frame, (0,0,960,220), tessdata, 11)
        return_action = visible_text(frame, (756,479,914,518), tessdata)
        book_title = '성경전서' in re.sub(r'\s+', '', heading)
        has_pages = ('성경' in heading or '말씀' in heading) and '여정' in return_action
        return book_title or has_pages, (heading, return_action)

    screenshot(page,report,'title')
    press((.18086,.71181))
    screenshot(page,report,'opening')
    for number in range(51):
        press((.832,.903))
        if number in (9,43,50):
            print(profile,'opening turns',number+1,flush=True)
    report['childChapterText'] = wait_for_frame('child-chapter', child_chapter)
    press((.198,.8292))
    metres, reach = wait_for_frame('field', opening_field)
    report['metresToJoseph']=[metres]
    field_image = actual_canvas(page, report['screens']['field']['file'])
    for index in range(1,10):
        if reach or (metres is not None and metres<=2.15):
            break
        walk(field_image)
        frame=screenshot(page,report,f'walk-{index}')
        field_image=actual_canvas(page,frame)
        metres,reach=read_distance(field_image,tessdata,touch)
        report['metresToJoseph'].append(metres)
        print(profile,'distance',metres,'interaction available',reach,flush=True)
    assert reach or (metres is not None and metres<=2.15), report['metresToJoseph']
    if touch: press((.917,.909))
    else:
        page.keyboard.press('e',delay=160)
        page.wait_for_timeout(350)
    report['confirmedJosephDialogueText']=wait_for_frame('joseph-dialogue', joseph_dialogue)
    # Reading the Scripture book and returning to the real encounter exercises the same persistent journey.
    press((.902,.075))
    report['scriptureBookText'] = wait_for_frame('scripture-book', scripture_book)
    press((.86,.928))
    wait_for_frame('dialogue-return', joseph_dialogue)
    page.wait_for_timeout(1700)
    page.reload(wait_until='domcontentloaded',timeout=35000)
    wait_title(page)
    press((.18086,.82292))
    report['confirmedAfterReload']=wait_for_frame('continued-dialogue', joseph_dialogue)
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
