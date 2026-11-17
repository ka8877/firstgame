#!/usr/bin/env python3
"""Select both current Sharp/Illustration products and the strict shared stager."""
from pathlib import Path
import os
import runpy

selected = os.environ.setdefault('FIRSTGAME_RELEASE_PROFILE', 'sharp-illustration')
if selected != 'sharp-illustration':
    raise ValueError('This entry point stages only the explicit sharp-illustration release')
runpy.run_path(str(Path(__file__).with_name('stage-canon.py')), run_name='__main__')
