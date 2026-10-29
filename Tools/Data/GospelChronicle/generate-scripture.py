#!/usr/bin/env python3
"""Generate only the four adult readings from verified official HAN passages."""
import json
from pathlib import Path

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
passages=json.loads((HERE/'passages.json').read_text())
q=lambda s:json.dumps(s,ensure_ascii=False)
source='https://www.bskorea.or.kr/bible/korbibReadpage.php?version=HAN&book={}&chap=3'
rows=[
 ('john-jordan','요단 부근의 요한','누가복음 3:2–3','luke-3-2-3','luk','요한의 회개 세례 사역을 전하는 누가복음의 기록.'),
 ('baptism-request','요한에게 세례를 받으려 하시다','마태복음 3:13–15','matthew-3-13-15','mat','예수님과 요한의 대화를 전하는 마태복음의 기록.'),
 ('baptism-water','세례 후 하늘의 음성','마태복음 3:16–17','matthew-3-16-17','mat','마태복음의 표현을 그대로 인용하며 누가복음의 하늘 음성과 합치지 않는다.'),
 ('ministry-thirty','삼십세쯤 시작하신 가르치심','누가복음 3:23','luke-3-23','luk','가르치심을 시작할 때의 대략적인 나이. 세례 날의 정확한 생일을 뜻하지 않는다.'),
]
lines=['// Generated from Tools/Data/GospelChronicle/passages.json; exact verified HAN words.', 'namespace PathOfDawn.Core','{',' internal static class GospelChronicleScripture',' {','  internal static readonly GospelReading[] Readings = new GospelReading[]','  {']
for id,title,reference,key,book,context in rows:
 lines.append('   new GospelReading('+', '.join(q(s)for s in(id,title,reference,passages[key],context,source.format(book)))+'),')
lines+=['  };',' }','}']
(ROOT/'Games/PathOfDawn/Assets/Game/Core/GospelChronicleScripture.cs').write_text('\n'.join(lines)+'\n')
print('Generated four verified adult readings.')
