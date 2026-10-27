#!/usr/bin/env python3
"""Author deterministic practice presets from complete, supported original cards.

These are new practice decks, not recovered original named product decks. Their
fixed IDs and card lists are generated once and committed with their audit.
"""
from collections import Counter
import hashlib,json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).parent
cards=json.loads((ROOT/'Tools/Data/ClassicCards/selection.json').read_text())['cards']
old_ids={c['sourceId']for c in cards[:250]}
by_id={c['sourceId']:c for c in cards}
MASK={1:7,11:1,12:3,13:5,21:2,31:4,32:6}
ELEMENTS={7:('light','빛','lyria',4),3:('water','물','serin',1),2:('fire','불','kael',1),4:('forest','숲','lyria',1),5:('metal','금속','kael',2),6:('earth','대지','noa',3),1:('darkness','암흑','serin',4)}

def costs(card):
    text=card['fields']['RequireSoul']
    parts=[(int(text[i:i+2]),int(text[i+2:i+4]))for i in range(0,len(text),4)]
    return sum(n for t,n in parts if t not in(1,81)),sum(n for t,n in parts if t==81)

def pool(element,category):
    return [c for c in cards if c['fields']['SoulType']==element and c['fields']['Category']==category]

def terrain(element,amount):
    # Every chosen land costs LP1 and produces one coloured plus one neutral soul.
    lands=sorted(pool(element,1),key=lambda c:int(c['sourceId']))[:amount]
    assert len(lands)==amount
    for c in lands:
        f=c['fields'];assert f['RequireLP']==1 and f['EarnSoulList']==f'{element}1018101'
    return [(c,4)for c in lands]

def units(element,curve,offset=0):
    used=set();result=[];preferred=ELEMENTS[element][3]
    for level,amount in curve:
        options=[c for c in pool(element,2)if c['fields']['LV']==level and c['fields']['DeckLimit']>=2 and c['sourceId']not in used]
        # Keep upkeep affordable, then prefer the element's useful capability and
        # new source records. Rotate only among cards satisfying the same curve.
        options.sort(key=lambda c:(costs(c)[1],not bool(MASK[c['fields']['UnitType']]&preferred),c['sourceId']in old_ids,int(c['sourceId'])))
        assert len(options)>=amount,(element,level,amount)
        # Offset is deliberately bounded to equally affordable alternatives.
        low_upkeep=costs(options[0])[1]
        affordable=[c for c in options if costs(c)[1]<=max(low_upkeep,1 if level<=3 else 2)]
        options=affordable if len(affordable)>=amount+offset else options
        selected=options[offset:offset+amount] if len(options)>=amount+offset else options[:amount]
        result.extend((c,2)for c in selected);used.update(c['sourceId']for c in selected)
    return result

def spells(element,amount,own_units):
    capabilities=[MASK[c['fields']['UnitType']]for c,_ in own_units]
    options=[]
    for c in pool(element,3):
        # A capability-filtered friendly spell must have a real recipient.
        if any(op.get('side')=='ally'and op.get('requiredTrait')and not any(mask&{'beast':1,'gear':2,'magic':4}[op['requiredTrait']]for mask in capabilities)for op in c['effect']['operations']):continue
        options.append(c)
    options.sort(key=lambda c:(c['fields']['LV'],costs(c)[1],int(c['sourceId'])))
    assert len(options)>=2
    # Six mono spells use three different originals when available. Dual decks
    # receive three spells per colour so neither source pool is merely cosmetic.
    if amount==6:
        count=min(3,len(options));copies=[2,2,2]if count==3 else[3,3]
        return list(zip(options[:count],copies))
    return [(options[0],2),(options[1],1)]

def equipment(element,amount,own_units):
    capabilities=[MASK[c['fields']['UnitType']]for c,_ in own_units]
    options=[]
    for c in pool(element,4):
        f=c['fields'];effect=c['effect'];required=0 if f['UnitType']==1 else MASK[f['UnitType']]
        if required and not any(mask&required for mask in capabilities):continue
        # Do not include flavour-only craft tokens or penalising equipment in a
        # beginner preset. Catalog availability remains independent of presets.
        if min(effect.get(k,0)for k in('attackDelta','defenseDelta','healthDelta'))<0:continue
        if sum(effect.get(k,0)for k in('attackDelta','defenseDelta','healthDelta'))<=0:continue
        options.append(c)
    options.sort(key=lambda c:(c['fields'].get('CardUISoul',0)>=9,c['fields']['LV'],costs(c)[1],-sum(c['effect'].get(k,0)for k in('attackDelta','defenseDelta','healthDelta')),int(c['sourceId'])))
    assert options,element
    result=[];remaining=amount
    for c in options:
        copies=min(2,remaining,c['fields']['DeckLimit']);result.append((c,copies));remaining-=copies
        if not remaining:break
    assert remaining==0
    return result

def build(identifier,name,description,elements,leader,offset=0):
    mono=len(elements)==1;chosen=[];own=[]
    for element in elements:
        chosen+=terrain(element,4 if mono else 2)
        curve=[(1,4),(2,3),(3,2),(4,1),(5,1),(6,1)]if mono else[(1,2),(2,2),(3,1),(5,1)]
        own+=units(element,curve,offset)
    chosen+=own
    for element in elements:chosen+=spells(element,6 if mono else 3,own)
    for element in elements:chosen+=equipment(element,4 if mono else 2,own)
    ids=[c['sourceId']for c,n in chosen for _ in range(n)]
    definitions=[by_id[i]for i in ids];counts=Counter(ids)
    assert len(ids)==50
    assert all(n<=by_id[i]['fields']['DeckLimit']for i,n in counts.items())
    assert Counter(c['fields']['Category']for c in definitions)=={1:16,2:24,3:6,4:4}
    assert {c['fields']['SoulType']for c in definitions}==set(elements)
    assert sum(c['fields']['LV']<=2 for c in definitions if c['fields']['Category']==2)>=14
    assert sum(costs(c)[1] for c in definitions if c['fields']['Category']==2 and c['fields']['LV']<=3)<=16
    return dict(id=identifier,name=name,description=description,recommendedLeaderId=leader,elements=elements,
                cards=['fm_'+i for i in ids],kindCounts={'terrain':16,'unit':24,'spell':6,'equipment':4},
                unitLevelCounts=dict(Counter(c['fields']['LV']for c in definitions if c['fields']['Category']==2)),
                unitGenericUpkeepCounts=dict(Counter(costs(c)[1]for c in definitions if c['fields']['Category']==2)),
                sourceIds=[dict(sourceId=c['sourceId'],name=c['name'],copies=n)for c,n in chosen])

decks=[]
MONO_NAMES={7:'성휘의 성가',3:'푸른 심해',2:'홍련의 압박',4:'야수의 연대',5:'은빛 군단',6:'바위의 요새',1:'황혼의 군세'}
for element,(slug,label,leader,_)in ELEMENTS.items():
    decks.append(build('mono-'+slug,label+' 단일 · '+MONO_NAMES[element],
        f'{label} 지형16장과 레벨1–6 유닛. 원작 카드의 소울·유지비·장착 조건을 익히는 단일 속성 연습 덱입니다.',[element],leader))
for slug,name,elements,leader in[
    ('light-water','빛과 물 · 서리의 성가',[7,3],'serin'),
    ('light-darkness','빛과 암흑 · 황혼의 서약',[7,1],'lyria'),
    ('fire-forest','불과 숲 · 붉은 야성',[2,4],'lyria'),
    ('metal-earth','금속과 대지 · 철벽의 맹세',[5,6],'kael'),
    ('water-forest','물과 숲 · 생명의 물결',[3,4],'serin'),
    ('fire-earth','불과 대지 · 화산의 심장',[2,6],'noa')]:
    decks.append(build('dual-'+slug,name,'두 속성 지형을8장씩 배분한50장 연습 덱. 낮은 레벨 전열과 두 계열의 마법·장비를 함께 사용합니다.',elements,leader,1))
assert len(decks)==13 and len({tuple(sorted(d['cards']))for d in decks})==13
payload={'revision':'fm-arcana-2026-10-27','scope':'13 newly authored practice presets, appended to the five preserved presets. Not recovered original product deck lists.','preservedDeckIds':['forest','knights','tides','stars','fidelity'],'totalDecks':18,'decks':decks}
(HERE/'starter-decks.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n')
q=lambda s:json.dumps(s,ensure_ascii=False)
lines=['// Generated by Tools/Data/Arcana/generate-starter-decks.py.','namespace FantasyCards.Core','{','    internal static class ArcanaStarterDeckData','    {','        internal static StarterDeckDefinition[] Create()','        {','            return new StarterDeckDefinition[]','            {']
for d in decks:
    lines.append('                new StarterDeckDefinition('+', '.join(q(d[k])for k in('id','name','description','recommendedLeaderId'))+',')
    lines.append('                    '+', '.join(q(i)for i in d['cards'])+'),')
lines+=['            };','        }','    }','}']
(ROOT/'Games/FantasyCards/Assets/Game/Core/ArcanaStarterDeckData.cs').write_text('\n'.join(lines)+'\n')
print('Generated13 new50-card presets:7mono +6dual;18 total including five preserved decks.')
