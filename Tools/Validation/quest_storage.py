"""Explicit workspace-backed current Quest evidence and generated outputs."""
from datetime import datetime, timezone
from pathlib import Path
import json
import os

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'Artifacts/Validation/Quest'
WORK = ROOT/'Artifacts/QuestWork'


def validate_evidence_root():
    if not OUT.is_dir() or OUT.is_symlink() or not WORK.is_dir() or WORK.is_symlink():
        raise ValueError('Quest uses its dedicated regular Artifacts workspace/evidence directories')
    record = json.loads((OUT/'generated-storage.json').read_text())
    if record.get('artifactPath')!=str(OUT) or record.get('actualRoot')!=str(OUT) or record.get('workRoot')!=str(WORK):
        raise ValueError('Quest generated storage ledger differs')
    return OUT


def authorize_output(value):
    lexical=Path(os.path.abspath(value))
    artifact=(ROOT/'Artifacts').resolve()
    if lexical.resolve()!=artifact and lexical.resolve().is_relative_to(artifact):
        return lexical
    raise ValueError('Quest output must stay under the task Artifacts workspace')


def prepare():
    OUT.mkdir(parents=True,exist_ok=True)
    WORK.mkdir(parents=True,exist_ok=True)
    ledger=OUT/'generated-storage.json'
    record={'artifactPath':str(OUT),'actualRoot':str(OUT),'workRoot':str(WORK),'buildRoot':'/tmp/firstgame-quest-builds'}
    if ledger.exists():
        if any(json.loads(ledger.read_text()).get(k)!=v for k,v in record.items()):
            raise ValueError('Existing Quest storage belongs to another task')
    else:
        ledger.write_text(json.dumps({**record,'preparedUtc':datetime.now(timezone.utc).isoformat(),'scope':'Workspace-backed cache/evidence/stage; temporary player volume. Prior ZIPs/source/originals remain preserved.'},indent=2)+'\n')
    return validate_evidence_root()


if __name__=='__main__':
    print(prepare())
