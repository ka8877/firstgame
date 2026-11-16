#!/usr/bin/env python3
"""Select both current Classic/NPR products and the strict shared stager."""
from pathlib import Path
import os
import runpy

selected = os.environ.setdefault('FIRSTGAME_RELEASE_PROFILE', 'classic-npr')
if selected != 'classic-npr':
    raise ValueError('This entry point stages only the explicit classic-npr release')
runpy.run_path(str(Path(__file__).with_name('stage-canon.py')), run_name='__main__')
