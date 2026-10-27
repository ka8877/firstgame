#!/usr/bin/env python3
"""Compare actual generated animator graphs independently of volatile local IDs."""
import argparse,hashlib,json,re
from pathlib import Path
import yaml

def graph(path):
 text=Path(path).read_text();headers=list(re.finditer(r'^--- !u!(\d+) &(-?\d+)\s*$',text,re.M));docs={}
 for i,m in enumerate(headers):
  body=yaml.safe_load(text[m.end():headers[i+1].start()if i+1<len(headers)else len(text)])
  kind,value=next(iter(body.items()));docs[int(m[2])]=(kind,value)
 keys={k:kind+':'+str(v.get('m_Name','')) for k,(kind,v)in docs.items()if kind!='AnimatorStateTransition'}
 for k,(kind,v)in docs.items():
  if kind=='AnimatorState':
   for i,t in enumerate(v.get('m_Transitions',[])):
    key=t['fileID'];to=docs[key][1].get('m_DstState',{}).get('fileID',0)
    keys[key]='AnimatorStateTransition:'+v['m_Name']+'->'+(docs[to][1].get('m_Name','')if to else'EXIT')+':'+str(i)
 root=next(k for k,(kind,_)in docs.items()if kind=='AnimatorController');seen=set()
 def norm(value):
  if isinstance(value,list):return[norm(x)for x in value]
  if not isinstance(value,dict):return value
  if set(value)=={'fileID'} and value['fileID']in docs:
   target=value['fileID'];seen.add(target);return{'local':keys[target]}
  return{k:norm(v)for k,v in value.items()}
 seen.add(root);result={};done=set()
 while seen-done:
  key=next(iter(seen-done));kind,value=docs[key];result[keys[key]]=norm(value);done.add(key)
 return result

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('before',type=Path);p.add_argument('after',type=Path);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 before,after=graph(a.before),graph(a.after)
 value={'status':'passed'if before==after else'failed','before':{'file':str(a.before),'sha256':hashlib.sha256(a.before.read_bytes()).hexdigest()},'after':{'file':str(a.after),'sha256':hashlib.sha256(a.after.read_bytes()).hexdigest()},'graphSha256':hashlib.sha256(json.dumps(after,sort_keys=True,separators=(',',':')).encode()).hexdigest(),'objects':len(after),'graph':after,'scope':'Reachable animator objects; local references normalized by state/machine/tree identity and ordered from-to transition priority. Every other serialized value, condition order, external motion GUID/fileID, exit time, duration and interruption property must match. Unreferenced obsolete serialized objects do not execute.'}
 a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 if before!=after:
  print('Different graph keys:',[k for k in set(before)|set(after)if before.get(k)!=after.get(k)]);raise SystemExit(1)
 print(json.dumps({k:v for k,v in value.items()if k!='graph'}))
if __name__=='__main__':main()
