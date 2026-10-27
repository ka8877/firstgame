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

# This optional PackageCache link is task-owned and ignored. Unity Package Manager
# repopulates an empty target from the committed package manifest/lock file.
package_link = ROOT / 'Games/PathOfDawn/Library/PackageCache'
package_target = Path('/tmp/firstgame-restoration-generated-cache/PathOfDawn/PackageCache')
if package_link.is_symlink() and package_link.readlink() == package_target:
    package_target.mkdir(parents=True, exist_ok=True)
