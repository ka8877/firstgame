"""Explicit current-release selection; defaults keep the historical Canon profile.

This module selects sources and paths only. It never executes a validation,
copies evidence, grants approval, changes a lease or mutates the public site.
"""
from pathlib import Path
import os

ROOT = Path(__file__).resolve().parents[3]
NAME = os.environ.get('FIRSTGAME_RELEASE_PROFILE', 'canon-art')
if NAME not in ('canon-art', 'living-classic'):
    raise ValueError('Choose an explicit supported current release profile')
IS_LIVING = NAME == 'living-classic'
REVISIONS = ({'FantasyCards': 'fm-classic-ui-2026-11-10', 'PathOfDawn': 'gospel-living-rpg-2026-11-10'}
             if IS_LIVING else {'FantasyCards': 'fm-canon-2026-11-09', 'PathOfDawn': 'gospel-canon-2026-11-09'})
OUT = ROOT / ('Artifacts/Validation/LivingRpgClassic-2026-11-10' if IS_LIVING
              else 'Artifacts/Validation/CanonArt-2026-11-09')
BUILD = Path('/tmp/firstgame-living-classic-20261110-builds' if IS_LIVING
             else '/tmp/firstgame-canon-20261109-builds')
CONTROL = ROOT / ('Artifacts/Validation/Autonomy/LivingRpgClassic-2026-11-10' if IS_LIVING
                  else 'Artifacts/Validation/Autonomy/CanonArt-2026-11-09')
STAGE = ROOT / ('Artifacts/LivingRpgClassicWebPlaytest' if IS_LIVING else 'Artifacts/CanonArtWebPlaytest')
PLAN_DIRECTORY = ROOT / 'Tools/Validation' / ('LivingRpgClassic' if IS_LIVING else 'CanonArt')
PLAN_PREFIX = 'living' if IS_LIVING else 'canon'
SOURCE = Path(__file__).resolve()
ADDITIONAL_NATIVE = {
    'FantasyCards': (
        'FantasyCards.SmokeTests.NativitySmokeTests.LateClientRoomsAndProfileSurviveReloadWithoutChangingEconomyOrRng',
    ),
    'PathOfDawn': (
        'PathOfDawn.SmokeTests.GospelLivingWorldSmokeTests.DialogueCompletionKeepsJosephAndPeopleAcrossBookAndReload',
        'PathOfDawn.SmokeTests.GospelLivingWorldSmokeTests.BackgroundPilgrimsActuallyWalkOnGroundWithoutReplacingStoryActors',
        'PathOfDawn.SmokeTests.GospelLivingWorldSmokeTests.MovingJohnConversationRestoresExactAcceptedNpcPose',
        'PathOfDawn.SmokeTests.GospelLivingUiSmokeTests.LiveMinimapTracksRealTravelAndOpensTheSameWorldMapWithoutAdvancingStory',
    ),
} if IS_LIVING else {'FantasyCards': (), 'PathOfDawn': ()}
