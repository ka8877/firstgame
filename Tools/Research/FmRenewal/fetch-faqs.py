from pathlib import Path
from html.parser import HTMLParser
import urllib.request,concurrent.futures,json,hashlib,re
root=Path(__file__).resolve().parent
class Text(HTMLParser):
 def __init__(self):super().__init__();self.out=[];self.skip=0
 def handle_starttag(self,tag,attrs):
  if tag in ('script','style'):self.skip+=1
 def handle_endtag(self,tag):
  if tag in ('script','style'):self.skip=max(0,self.skip-1)
 def handle_data(self,t):
  if not self.skip and t.strip():self.out.append(t.strip())
def f(no):
 url='https://web.archive.org/web/20171201000000id_/http://www.fantasymasters.co.kr/HelpDesk/FAQ_view.asp?category=106&Nowpage=1&faqNo='+str(no)
 try:
  r=urllib.request.urlopen(url,timeout=25);b=r.read();t=b.decode('cp949','replace');p=Text();p.feed(t);txt='\n'.join(p.out);(root/f'faq{no}.html').write_text(t);(root/f'faq{no}-text.txt').write_text(txt)
  d={'url':url,'finalUrl':r.url,'status':r.status,'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest()};(root/f'faq{no}.json').write_text(json.dumps(d,indent=2));terms=[]
  for term in ['코인','공격력','방어력','체력']:
   start=txt.find(term)
   if start>=0:terms.append(txt[max(0,start-100):start+700])
  return no,d,'\n'.join(terms)
 except Exception as e:
  d={'url':url,'error':str(e)};(root/f'faq{no}.json').write_text(json.dumps(d,indent=2));return no,d
for x in concurrent.futures.ThreadPoolExecutor(4).map(f,[727,734,722,741,740,966,736,735,739,738,737,965]):print(x,flush=True)
