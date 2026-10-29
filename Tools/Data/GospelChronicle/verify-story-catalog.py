#!/usr/bin/env python3
"""Audit exact old reading literals and new source-backed adult quotations."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
CORE=ROOT/'Games/PathOfDawn/Assets/Game/Core'
literal=re.compile(r'"(?:[^"\\]|\\.)*"')


def arguments(text,constructor):
    start=0
    while True:
        pos=text.find('new '+constructor+'(',start)
        if pos<0:return
        pos+=len('new '+constructor+'(')
        quoted=escaped=False;depth=1;end=pos
        while depth:
            c=text[end]
            if quoted:
                if escaped:escaped=False
                elif c=='\\':escaped=True
                elif c=='"':quoted=False
            elif c=='"':quoted=True
            elif c=='(':depth+=1
            elif c==')':depth-=1
            end+=1
        yield text[pos:end-1]
        start=end


old_readings={}
for filename,constructor in [('GospelCatalog.cs','GospelReading'),('GospelStudyCatalog.cs','GospelStudyEncounter')]:
    path=CORE/filename
    previous=subprocess.check_output(['git','show','HEAD:'+str(path.relative_to(ROOT))],cwd=ROOT).decode()
    def reading_literals(text):
        result={}
        for args in arguments(text,constructor):
            if args.lstrip().startswith('"'):
                values=[json.loads(m.group()) for m in literal.finditer(args)]
                result[values[0]]=values[:4]
        return result
    before=reading_literals(previous);current=reading_literals(path.read_text())
    assert current==before,filename+' changed existing ID/title/reference/quotation'
    old_readings.update(current)
assert len(old_readings)==15

passages=json.loads((HERE/'passages.json').read_text())
mapping={'john-jordan':('luke-3-2-3','luk'),'baptism-request':('matthew-3-13-15','mat'),'baptism-water':('matthew-3-16-17','mat'),'ministry-thirty':('luke-3-23','luk')}
adult=[]
for args in arguments((CORE/'GospelChronicleScripture.cs').read_text(),'GospelReading'):
    values=json.loads('['+args+']')
    assert len(values)==6
    id,title,reference,text,context,url=values
    key,book=mapping[id]
    assert text==passages[key]
    assert url=='https://www.bskorea.or.kr/bible/korbibReadpage.php?version=HAN&book='+book+'&chap=3'
    adult.append({'id':id,'reference':reference,'sourceUrl':url,'textSha256':hashlib.sha256(text.encode()).hexdigest()})
assert len(adult)==4 and len({r['id'] for r in adult})==4
catalog=(CORE/'GospelChronicleCatalog.cs').read_text()
birth=re.findall(r'^            Birth\("([^"]+)"',catalog,re.M)
world=re.findall(r'^            World\("([^"]+)","([^"]+)"',catalog,re.M)
assert birth==['road-bethlehem','birth-manger','night-shepherds','birth-good-news','manger-witnesses','birth-remembered','name-jesus','temple-dedication','simeon-peace','anna-thanksgiving','nazareth-growth']
assert [chapter for _,chapter in world]==['temple']*7+['baptism']*5
subprocess.run(['python3',str(HERE/'verify-sources.py')],cwd=ROOT,check=True,stdout=subprocess.DEVNULL)
report={'status':'passed','revision':'gospel-chronicle-2026-10-29','preservedExistingReadings':len(old_readings),'newVerifiedReadings':len(adult),'totalReadings':19,'illustratedBirthSteps':len(birth),'templeRpgSteps':7,'adultRpgSteps':5,'totalQuestSteps':23,'adultReadings':adult,'scope':'Exact content/source audit only. Runtime progression/save/Unity tests are separate execution evidence.'}
(HERE/'story-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False,indent=2))
