#!/usr/bin/env python3
import urllib.request,urllib.parse,concurrent.futures,json,re,html,hashlib,datetime,pathlib
from html.parser import HTMLParser
ROOT=pathlib.Path(__file__).resolve().parent
class Links(HTMLParser):
 def __init__(self):super().__init__();self.current=None;self.links=[]
 def handle_starttag(self,tag,attrs):
  if tag=='a':self.current={'attrs':dict(attrs),'parts':[]}
 def handle_data(self,data):
  if self.current:self.current['parts'].append(data)
 def handle_endtag(self,tag):
  if tag=='a' and self.current:
   a=self.current;self.links.append({'url':a['attrs'].get('href',''),'text':' '.join(a['parts']).strip()});self.current=None
qs=['판타지마스터즈 2018 플레이 영상','판타지마스터즈 2017 전투 동영상','판타지마스터즈 서비스 종료 2018','판타지마스터즈 코인 게임화면','판타지마스터즈 2018 동영상','Fantasy Masters 2017 gameplay','판마 섭종 마지막 게임영상','판타지마스터즈 2018 3월 전투']
def f(pair):
 i,q=pair;u='https://search.naver.com/search.naver?'+urllib.parse.urlencode({'query':q})
 try:
  r=urllib.request.urlopen(u,timeout=25);b=r.read();p=Links();p.feed(b.decode('utf8','replace'))
  links=[];seen=set()
  for a in p.links:
   if not a['url'].startswith('http') or not a['text'] or any(x in a['url'] for x in ['search.naver.com','help.naver.com','policy.naver.com','shopping.naver.com','map.naver.com','dict.naver.com','mate.naver.com','mkt.naver.com']):continue
   parts=urllib.parse.urlsplit(a['url'])
   # Store stable public citations, not ephemeral signed cafe/search parameters.
   query=[(k,v) for k,v in urllib.parse.parse_qsl(parts.query) if k in ['v','vid','videoId','id','no']]
   a['url']=urllib.parse.urlunsplit((parts.scheme,parts.netloc,parts.path,urllib.parse.urlencode(query),''))
   if a['url'] not in seen:links.append(a);seen.add(a['url'])
  out={'query':q,'url':u,'finalUrl':r.url,'status':r.status,'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest(),'links':links}
  (ROOT/f'search-{i}.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');return out
 except Exception as e:return {'query':q,'error':str(e)}
if __name__=='__main__':
 for x in concurrent.futures.ThreadPoolExecutor(4).map(f,enumerate(qs)):print(json.dumps(x,ensure_ascii=False))
