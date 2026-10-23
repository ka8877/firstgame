#!/usr/bin/env python3
"""Restore the build-only MIT FluidR3 GM bank from a pinned Debian package."""
from pathlib import Path
import argparse, hashlib, subprocess, urllib.request
URL='https://deb.debian.org/debian/pool/main/f/fluid-soundfont/fluid-soundfont-gm_3.1-6_all.deb'
PACKAGE_SHA='9965fbcc6acee17d6f72b685d28c7f968ec64eba14645445b476d5c6cc2eee4c'
FONT_SHA='74594e8f4250680adf590507a306655a299935343583256f3b722c48a1bc1cb0'
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--destination',type=Path,default=Path('/workspace/tools/audio'));args=p.parse_args()
args.destination.mkdir(parents=True,exist_ok=True)
package=args.destination/'fluid-soundfont-gm_3.1-6_all.deb'
if not package.is_file():urllib.request.urlretrieve(URL,package)
if hashlib.sha256(package.read_bytes()).hexdigest()!=PACKAGE_SHA:raise SystemExit('SoundFont package checksum does not match; not extracting.')
out=args.destination/'fluid-r3'
subprocess.run(['dpkg-deb','-x',str(package),str(out)],check=True)
font=out/'usr/share/sounds/sf2/FluidR3_GM.sf2'
assert hashlib.sha256(font.read_bytes()).hexdigest()==FONT_SHA
print(font)
