#!/usr/bin/env python3
"""Select the fresh Client/Motion profile and retain the strict two-game stage."""
from pathlib import Path
import os
import runpy

selected = os.environ.setdefault('FIRSTGAME_RELEASE_PROFILE', 'client-motion')
if selected != 'client-motion':
    raise ValueError('This entry point only stages the explicit client-motion release')
runpy.run_path(str(Path(__file__).with_name('stage-canon.py')), run_name='__main__')
