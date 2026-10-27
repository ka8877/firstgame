#!/usr/bin/env python3
"""Compile and execute the real FmLocalMusicHost against deterministic audio clocks."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
from datetime import datetime, timezone
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Artifacts/Validation/Arcana/MusicNative'
OUT.mkdir(parents=True, exist_ok=True)
dotnet = shutil.which('dotnet') or '/workspace/tools/dotnet/dotnet'
source = ROOT / 'Games/FantasyCards/Assets/Game/Presentation/FmLocalMusicHost.cs'
stubs = ROOT / 'Tools/Audio/Arcana/native-lifecycle.cs'
project = OUT / 'NativeMusicLifecycle.csproj'
project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>disable</Nullable></PropertyGroup><ItemGroup>' + ''.join('<Compile Include="' + escape(str(path)) + '" />' for path in (source, stubs)) + '</ItemGroup></Project>')
env = dict(os.environ)
env.setdefault('DOTNET_ROOT', '/workspace/tools/dotnet')
env.setdefault('DOTNET_CLI_HOME', '/workspace/tools/dotnet-home')
run = subprocess.run([dotnet, 'run', '--project', str(project), '--configuration', 'Release', '--verbosity', 'quiet'], cwd=ROOT, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
(OUT / 'run.txt').write_text(run.stdout)
if run.returncode:
    raise SystemExit(run.stdout)
result = json.loads(next(line for line in reversed(run.stdout.splitlines()) if line.startswith('{')))
result.update(checkedUtc=datetime.now(timezone.utc).isoformat(), files=[{'file': str(path.relative_to(ROOT)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()} for path in (source, stubs)])
(OUT / 'report.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
print(json.dumps(result, ensure_ascii=False))
