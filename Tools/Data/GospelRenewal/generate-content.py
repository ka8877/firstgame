from pathlib import Path
import re,html,json
root=Path(__file__).resolve().parents[3]; raw=(root/'Tools/Data/GospelRenewal/luke-2-HAN.html').read_text();verses={}
for n,body in re.findall(r'<span class="number">(\d+)&nbsp;.*?</span>(.*?)<br\s*/?>',raw,re.S):
 body=re.sub(r'<div\b[^>]*>.*?</div>','',body,flags=re.S)
 body=re.sub(r'<a\b[^>]*class=comment\b[^>]*>.*?</a>','',body,flags=re.S)
 text=html.unescape(re.sub('<[^>]+>','',body)); text=' '.join(text.split());verses[int(n)]=text
# Unmodified text from the official HAN page; selected passages contain no footnotes.
content=json.loads((root/'Tools/Data/GospelRenewal/story-content.json').read_text())
specs=content['encounters']
def q(s):return json.dumps(s,ensure_ascii=False)
rows=[]
for id,title,a,b,x,portrait,turns in specs:
 text='\n'.join(f'{n} {verses[n]}' for n in range(a,b+1))
 reference=f'누가복음 2:{a}'+(f'–{b}' if b>a else '')
 rows.append('            new GospelStudyEncounter('+','.join([q(id),q(title),q(reference),q(text),str(x)+'f',q(portrait)])+', new[] {\n'+',\n'.join('                '+q(t) for t in turns)+'\n            })')
intro=content['opening']
text='''using System;
using System.Collections.Generic;
namespace PathOfDawn.Core
{
    public sealed class GospelStudyEncounter
    {
        public readonly GospelReading Reading;
        public readonly float X;
        public readonly string Portrait;
        public readonly IReadOnlyList<string> Turns;
        public GospelStudyEncounter(string id,string title,string reference,string text,float x,string portrait,string[] turns)
        { Reading=new GospelReading(id,title,reference,text,"본문을 바탕으로 한 학습용 극화"); X=x; Portrait=portrait; Turns=Array.AsReadOnly(turns); }
    }
    public sealed class GospelOpeningPage
    {
        public readonly string Title, Subtitle, Body, Reference;
        public GospelOpeningPage(string title,string subtitle,string body,string reference)
        {Title=title;Subtitle=subtitle;Body=body;Reference=reference;}
    }
    /// <summary>Additive childhood study trail; original seven Gospel reading IDs and order remain untouched.</summary>
    public static class GospelStudyCatalog
    {
        public static IReadOnlyList<GospelOpeningPage> Opening { get; }=Array.AsReadOnly(new[] {
'''+',\n'.join('            new GospelOpeningPage('+','.join(q(s) for s in t)+')' for t in intro)+'''
        });
        public static IReadOnlyList<GospelStudyEncounter> Encounters { get; }=Array.AsReadOnly(new[] {
'''+',\n'.join(rows)+'''
        });
        public static GospelReading GetReading(string id)
        { foreach(var encounter in Encounters)if(encounter.Reading.Id==id)return encounter.Reading;return GospelCatalog.GetReading(id); }
        public static IReadOnlyList<GospelReading> AllReadings
        { get {var result=new List<GospelReading>(GospelCatalog.PrologueReadings);foreach(var encounter in Encounters)result.Add(encounter.Reading);for(int i=GospelCatalog.PrologueReadings.Count;i<GospelCatalog.Readings.Count;i++)result.Add(GospelCatalog.Readings[i]);return result.AsReadOnly();} }
    }
}
'''
p=root/'Games/PathOfDawn/Assets/Game/Core/GospelStudyCatalog.cs';p.write_text(text)
(root/'Tools/Data/GospelRenewal/selected-verses.json').write_text(json.dumps({str(n):verses[n] for _,_,a,b,*_ in specs for n in range(a,b+1)},ensure_ascii=False,indent=2)+'\n')
print('encounters',len(specs),'dialogue turns',sum(len(s[-1]) for s in specs),'dialogue chars',sum(len(t) for s in specs for t in s[-1]),'introchars',sum(len(t[2]) for t in intro))
