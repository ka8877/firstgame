#!/usr/bin/env python3
"""Require current non-empty NUnit execution; a zero-test success is not readiness."""
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

path = Path(sys.argv[1])
root = ET.parse(path).getroot()
cases = root.findall('.//test-case')
passed = sum(case.get('result') == 'Passed' for case in cases)
failed = sum(case.get('result') == 'Failed' for case in cases)
skipped = sum(case.get('result') in ('Skipped', 'Inconclusive') for case in cases)
print(f'{path.name}: total={len(cases)}, passed={passed}, failed={failed}, skipped/inconclusive={skipped}')
if not cases or failed or passed == 0 or root.get('result') != 'Passed':
    raise SystemExit(1)
