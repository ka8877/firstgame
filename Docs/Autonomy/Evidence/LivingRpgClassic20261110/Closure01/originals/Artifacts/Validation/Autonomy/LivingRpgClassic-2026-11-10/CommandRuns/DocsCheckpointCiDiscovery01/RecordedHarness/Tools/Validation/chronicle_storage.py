"""Exact task-owned evidence storage authorization; retain lexical artifact paths."""
from pathlib import Path
import json
import os

ROOT = Path(__file__).resolve().parents[2]
LINK = ROOT / 'Artifacts/Validation/Chronicle'
ACTUAL = Path('/tmp/firstgame-chronicle-evidence')


def validate_evidence_root():
    if not (LINK.is_symlink() and LINK.readlink() == ACTUAL
            and ACTUAL.is_dir() and not ACTUAL.is_symlink()):
        raise ValueError('Chronicle evidence must use its exact task-owned storage link')
    ledger = LINK / 'generated-storage.json'
    if not ledger.is_file() or ledger.is_symlink():
        raise ValueError('Chronicle evidence storage ledger missing')
    record = json.loads(ledger.read_text())
    if record.get('artifactPath') != str(LINK) or record.get('actualRoot') != str(ACTUAL):
        raise ValueError('Chronicle evidence storage ledger differs')
    return LINK


def authorize_output(value):
    """Allow ordinary Artifacts paths or the one recorded Chronicle evidence root."""
    lexical = Path(os.path.abspath(value))
    artifact = (ROOT / 'Artifacts').resolve()
    resolved = lexical.resolve()
    if resolved != artifact and resolved.is_relative_to(artifact):
        return lexical
    if lexical.is_relative_to(LINK):
        validate_evidence_root()
        if resolved.is_relative_to(ACTUAL):
            return lexical
    raise ValueError('Evidence output must remain under Artifacts or exact Chronicle storage')
