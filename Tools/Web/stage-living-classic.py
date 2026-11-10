#!/usr/bin/env python3
"""Select the new release, then use the unchanged strict two-game stage flow."""
from pathlib import Path
import os
import runpy

selected = os.environ.setdefault('FIRSTGAME_RELEASE_PROFILE', 'living-classic')
if selected != 'living-classic':
    raise ValueError('This entry point only stages the explicit living-classic release')
runpy.run_path(str(Path(__file__).with_name('stage-canon.py')), run_name='__main__')
