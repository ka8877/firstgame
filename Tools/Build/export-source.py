#!/usr/bin/env python3
"""Package both Unity project sources with actual LFS working files, not pointers."""
from pathlib import Path
from modern_release_dispatch import dispatch
dispatch(Path(__file__).resolve().parents[2], 'source')
import json
import os
import zipfile

root = Path(__file__).resolve().parents[2]
validation = json.loads((root / 'Tools/Environment/validation.json').read_text())
if (validation.get('gameRevisions') != {'FantasyCards': 'fm-remaster-2026-10-26', 'PathOfDawn': 'gospel-remaster-2026-10-26'}
        or validation.get('contentVersions') != {'FantasyCards': 'fm-classic-0.1.0', 'PathOfDawn': 'gospel-0.1.0'}):
    raise SystemExit('Summarize current remaster validation before exporting release sources.')
for name in ('CardRemaster.md', 'FmRemasterUI.md', 'FmMusicLatency.md', 'GospelRemaster.md', 'RemasterValidation.md'):
    if not (root / 'Docs' / name).is_file():
        raise SystemExit('Missing current remaster source document: ' + name)
output = root / 'Artifacts/FirstGame-UnitySources.zip'
output.parent.mkdir(parents=True, exist_ok=True)
excluded = {'.git', 'Artifacts', 'Library', 'Temp', 'Logs', 'UserSettings', 'obj', 'bin', '__pycache__',
            '.aws', '.ssh', '.gnupg', '.config', '.credentials', '.codex'}
credential_files = {'auth.json', 'credentials', 'credentials.json', 'id_rsa', 'id_ed25519'}
with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for directory, folders, files in os.walk(root):
        folders[:] = sorted(folder for folder in folders if folder not in excluded)
        for folder in folders:
            if (Path(directory) / folder).is_symlink():
                raise RuntimeError(f'Symlink must be reviewed before source export: {Path(directory) / folder}')
        for filename in sorted(files):
            path = Path(directory) / filename
            relative = path.relative_to(root)
            if (path.suffix.lower() in ('.log', '.pyc', '.ulf', '.alf', '.pem', '.key')
                    or filename.lower() in credential_files or filename.startswith('.env')):
                continue
            if path.is_symlink():
                raise RuntimeError(f'Symlink must be reviewed before source export: {relative}')
            if path.stat().st_size < 512 and path.read_bytes().startswith(b'version https://git-lfs.github.com/spec/v1'):
                raise RuntimeError(f'LFS content is missing: {relative}; run git lfs pull before exporting.')
            archive.write(path, 'FirstGame/' + str(relative))
print(f'{output}: {output.stat().st_size} bytes; Unity sources, NOT game build binaries.')
