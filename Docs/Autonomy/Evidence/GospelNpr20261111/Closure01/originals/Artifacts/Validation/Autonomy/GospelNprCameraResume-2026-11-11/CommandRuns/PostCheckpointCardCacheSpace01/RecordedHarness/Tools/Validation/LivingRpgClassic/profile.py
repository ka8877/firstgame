"""Explicit current-release selection; defaults keep the historical Canon profile.

This module selects sources and paths only. It never executes a validation,
copies evidence, grants approval, changes a lease or mutates the public site.
"""
from pathlib import Path
import os

ROOT = Path(__file__).resolve().parents[3]
NAME = os.environ.get('FIRSTGAME_RELEASE_PROFILE', 'canon-art')
if NAME not in ('canon-art', 'living-classic', 'client-motion', 'gospel-npr'):
    raise ValueError('Choose an explicit supported current release profile')
IS_LIVING = NAME == 'living-classic'
IS_CLIENT_MOTION = NAME == 'client-motion'
IS_GOSPEL_NPR = NAME == 'gospel-npr'
REQUIRES_EXPLICIT_ENV = IS_LIVING or IS_CLIENT_MOTION or IS_GOSPEL_NPR
CORE_MINIMUM = 311 if REQUIRES_EXPLICIT_ENV else 309
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
NATIVE_REPLACEMENTS = {}
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
} if REQUIRES_EXPLICIT_ENV else {'FantasyCards': (), 'PathOfDawn': ()}

if IS_CLIENT_MOTION:
    REVISIONS = {'FantasyCards': 'fm-client-match-2026-11-11',
                 'PathOfDawn': 'gospel-character-motion-2026-11-11'}
    OUT = ROOT / 'Artifacts/Validation/ClientMotion-2026-11-11'
    BUILD = Path('/tmp/firstgame-client-motion-20261111-builds')
    CONTROL = ROOT / 'Artifacts/Validation/Autonomy/ClientMotion-2026-11-11'
    STAGE = ROOT / 'Artifacts/ClientMotionWebPlaytest'
    PLAN_DIRECTORY = ROOT / 'Tools/Validation/ClientMotion'
    PLAN_PREFIX = 'client'
    NATIVE_REPLACEMENTS = {
        'PathOfDawn.SmokeTests.GospelNativityCharacterSmokeTests.ImportedWholeBodyCyclesBothStepsFacesFourWaysAndSettlesAtActualFeet':
            'PathOfDawn.SmokeTests.GospelNativityCharacterSmokeTests.ImportedLayersCycleEightDirectionsAndSettleAtActualFeet',
        'PathOfDawn.SmokeTests.GospelCanopyCharacterSmokeTests.ImportedWholeBodyCyclesBothStepsFacesFourWaysAndSettlesAtActualFeet':
            'PathOfDawn.SmokeTests.GospelCanopyCharacterSmokeTests.ImportedLayersCycleEightDirectionsAndSettleAtActualFeet',
    }
    ADDITIONAL_NATIVE['PathOfDawn'] += (
        'PathOfDawn.SmokeTests.GospelCharacterMotionSmokeTests.EveryStoryAndAmbientIdentityUsesEightActualSynchronizedLocomotionDrawings',
        'PathOfDawn.SmokeTests.GospelCharacterMotionSmokeTests.NamedNpcFacesAllEightPartnersStopsItsGaitAndKeepsTheSamePhysicalFeet',
    )

# Existing modes retain their exact two-current-game contract.
CURRENT_GAMES = ('FantasyCards', 'PathOfDawn')
PRESERVED_GAMES = ()
EXTRA_SOURCE_FILES = ()
if IS_GOSPEL_NPR:
    REVISIONS = {'FantasyCards': 'fm-client-match-2026-11-11', 'PathOfDawn': 'gospel-npr-2026-11-11'}
    OUT = ROOT / 'Artifacts/Validation/GospelNpr-2026-11-11'
    BUILD = Path('/tmp/firstgame-gospel-npr-20261111-builds')
    CONTROL = ROOT / 'Artifacts/Validation/Autonomy/GospelNpr-2026-11-11'
    STAGE = ROOT / 'Artifacts/GospelNprWebPlaytest'
    PLAN_DIRECTORY = ROOT / 'Tools/Validation/GospelNpr'
    PLAN_PREFIX = 'npr'
    CURRENT_GAMES = ('PathOfDawn',)
    PRESERVED_GAMES = ('FantasyCards',)
    ADDITIONAL_NATIVE = {'FantasyCards': (), 'PathOfDawn': ()}
    NATIVE_REPLACEMENTS = {}
    EXTRA_SOURCE_FILES = (
        ROOT / 'Tools/Validation/GospelNpr/contract.py',
        ROOT / 'Tools/Validation/GospelNpr/card_baseline.py',
        ROOT / 'Tools/Validation/GospelNpr/native-contract.json',
    )

