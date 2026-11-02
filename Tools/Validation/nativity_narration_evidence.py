"""Bind ordinary WebGL narration observations to exact current dialogue assets."""
from pathlib import Path
import hashlib
import json
import math
import re

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / 'Games/PathOfDawn/Assets/Resources/Voice/GospelNativity/nativity-voice.json'
PREFIX = 'GOSPEL_NATIVITY_NARRATION '


def narration_evidence(console):
    manifest = json.loads(MANIFEST.read_text())
    lines = {row['key']: row for row in manifest['lines']}
    if len(lines) != len(manifest['lines']) or len(lines) != 28:
        raise ValueError('Exact current28 narration manifest required')
    events = []
    for index, row in enumerate(console):
        text = row.get('text', '')
        if PREFIX not in text:
            continue
        match = re.search(re.escape(PREFIX) + r'(actual-start|stop)\s+([^\n]+)', text)
        if not match:
            raise ValueError('Malformed actual narration observation')
        values = dict(re.findall(r'([A-Za-z][A-Za-z0-9]*)=([^\s]+)', match[2]))
        event = {'kind':match[1], 'consoleIndex':index, 'navigation':row.get('navigation'), **values}
        if match[1] == 'actual-start':
            line = lines.get(values.get('key'))
            if not line or values.get('textSha256') != line['textSha256'] or values.get('clip') != Path(line['resourcePath']).name:
                raise ValueError('Actual narration key/clip/text hash differs from current manifest')
            time_value,gain,duration = (float(values[k]) for k in ('sourceTime','sourceGain','seconds'))
            if not (all(math.isfinite(v) and v > 0 for v in (time_value,gain,duration)) and time_value <= duration + .1):
                raise ValueError('Narration start must observe a positive real source clock and gain')
        events.append(event)
    initial = next((e for e in events if e['navigation'] == 0 and e['kind'] == 'actual-start'
                    and e.get('key') == 'birth-road-bethlehem:0'), None)
    stopped = next((e for e in events if initial and e['navigation'] == 0 and e['kind'] == 'stop'
                    and e.get('key') == initial['key'] and e.get('reason') == 'bible'
                    and e.get('hadPlaying') == 'True' and e['consoleIndex'] > initial['consoleIndex']), None)
    advanced = next((e for e in events if stopped and e['navigation'] == 0 and e['kind'] == 'actual-start'
                     and e.get('key') == 'birth-road-bethlehem:1' and e['consoleIndex'] > stopped['consoleIndex']), None)
    if not initial or not stopped or not advanced:
        raise ValueError('Actual introkey0 start → playing book stop → post-book nextkey1 start required')
    return {'status':'passed', 'manifest':{'file':str(MANIFEST.relative_to(ROOT)),
            'sha256':hashlib.sha256(MANIFEST.read_bytes()).hexdigest()}, 'events':events,
            'initialStartConsoleIndex':initial['consoleIndex'], 'bookStopConsoleIndex':stopped['consoleIndex'],
            'nextStartConsoleIndex':advanced['consoleIndex'],
            'scope':'Read-only observations emitted only by actual audible-time Unity AudioSources after ordinary UI input; no debug API or game/save injection. PCM is measured separately. This is not human pronunciation or acting approval.'}
