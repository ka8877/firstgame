"""Current source/path registry; legacy profiles retain their original selector.

Selection never runs a validator or grants approval. The new profile has its
own source contracts and cannot execute Native/browser work until Root freezes
their final source selection and ordinary-input geometry.
"""
from pathlib import Path
import os

if os.environ.get('FIRSTGAME_RELEASE_PROFILE', 'canon-art') != 'sharp-illustration':
    from LivingRpgClassic.profile import *  # Preserve every historical mode's values and SOURCE.
    IS_SHARP_ILLUSTRATION = False
else:
    ROOT = Path(__file__).resolve().parents[2]
    NAME = 'sharp-illustration'
    IS_LIVING = IS_CLIENT_MOTION = IS_GOSPEL_NPR = IS_CLASSIC_NPR = False
    IS_SHARP_ILLUSTRATION = True
    USES_NPR_GOSPEL = REQUIRES_EXPLICIT_ENV = True
    CORE_MINIMUM = 311
    REVISIONS = {'FantasyCards': 'fm-sharp-client-2026-11-17',
                 'PathOfDawn': 'gospel-illustrated-anime-2026-11-17'}
    OUT = ROOT / 'Artifacts/Validation/SharpIllustration-2026-11-17'
    CONTROL = ROOT / 'Artifacts/Validation/Autonomy/SharpIllustration-2026-11-17'
    BUILD = Path('/tmp/firstgame-sharp-illustration-20261117-builds')
    STAGE = ROOT / 'Artifacts/SharpIllustrationWebPlaytest'
    PLAN_DIRECTORY = ROOT / 'Tools/Validation/SharpIllustration'
    PLAN_PREFIX = NAME
    SOURCE = Path(__file__).resolve()
    CURRENT_GAMES = ('FantasyCards', 'PathOfDawn')
    PRESERVED_GAMES = ()
    NATIVE_REPLACEMENTS = {}
    ADDITIONAL_NATIVE = {'FantasyCards': (), 'PathOfDawn': ()}
    EXTRA_SOURCE_FILES = (PLAN_DIRECTORY / 'contract.py', PLAN_DIRECTORY / 'native-contract.json',
                          PLAN_DIRECTORY / 'baseline-obligations.json')

# Fresh executions pin this dispatch source as well as a delegated legacy SOURCE.
REGISTRY_SOURCE = Path(__file__).resolve()
