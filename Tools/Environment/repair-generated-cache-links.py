#!/usr/bin/env python3
"""Recreate empty targets for this task's optional ignored Unity cache links."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CACHE = Path('/tmp/firstgame-arcana-generated-cache')
for game in ('FantasyCards', 'PathOfDawn'):
    for kind in ('Bee', 'Artifacts', 'BurstCache', 'ShaderCache', 'PlayerDataCache'):
        link = ROOT / 'Games' / game / 'Library' / kind
        expected = CACHE / game / kind
        if link.is_symlink() and link.readlink() == expected:
            expected.mkdir(parents=True, exist_ok=True)
