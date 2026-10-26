#!/usr/bin/env python3
"""Archive allowlisted evidence, excluding raw logs, models and audio sources."""
import hashlib
import json
from pathlib import Path
import zipfile

root = Path(__file__).resolve().parents[2]
artifacts = root / "Artifacts"
selected = [
    root / "Docs/Validation.md",
    root / "Docs/ValidationHistory/2026-10-20.md",
    root / "Docs/PlaytestGuide.md",
    root / "Docs/UnitySmokeTests.md",
    root / "Docs/CoreValidation.md",
    root / "Docs/ArtDirection.md",
    root / "Docs/Audio.md",
    root / "Docs/Voice.md",
    root / "Docs/FantasyCardsRules.md",
    root / "Docs/FantasyMastersClassic.md",
    root / "Docs/ClassicCardArt.md",
    root / "Docs/ClassicAudio.md",
    root / "Docs/Research/FantasyMastersRulesSources.md",
    root / "Docs/Research/FantasyMastersDatabaseSources.md",
    root / "Docs/classic-cards-browser-validation.md",
    root / "Docs/ValidationHistory/2026-10-26-before-classic-Validation.md",
    root / "Docs/ValidationHistory/2026-10-26-before-classic-VisualValidation.md",
    root / "Tools/Environment/validation-2026-10-26-before-classic.json",
    root / "Tools/Data/ClassicCards/selection.json",
    root / "Tools/Art/ClassicCards/SOURCE-NOTICE.txt",
    root / "Tools/Audio/classic-verification.json",
    root / "Docs/PathOfDawnValidation.md",
    root / "Docs/PaintedDawn.md",
    root / "Docs/DawnMotion.md",
    root / "Docs/ValidationHistory/2026-10-24-before-fluid-dawn-Validation.md",
    root / "Docs/ValidationHistory/2026-10-24-before-fluid-dawn-VisualValidation.md",
    root / "Tools/Environment/validation-2026-10-24-before-fluid-dawn.json",
    root / "Docs/VisualValidation.md",
    root / "Docs/WebPlaytest.md",
    root / "Docs/Environment.md",
    root / "Docs/Illustration.md",
    root / "Docs/AssetRegister.md",
    root / "Docs/painted-dawn-browser-validation.md",
    root / "Docs/ValidationHistory/2026-10-24-before-painted-dawn.md",
    root / "Docs/ValidationHistory/2026-10-24-before-painted-dawn-visual.md",
    root / "Tools/Art/ToonDawn.md",
    root / "Tools/Environment/validation.json",
    root / "Tools/Environment/validation-2026-10-20.json",
    root / "Tools/Environment/validation-2026-10-23-art-edition.json",
    root / "Tools/Environment/validation-2026-10-23-subculture-bgm.json",
    root / "Tools/Environment/validation-2026-10-24-before-painted-dawn.json",
    root / "Docs/ValidationHistory/2026-10-23-art-edition.md",
    root / "Docs/ValidationHistory/2026-10-23-subculture-bgm.md",
    root / "Tools/Art/AssetManifest.json",
    root / "Tools/Art/ToonDawnManifest.json",
    root / "Tools/Art/verification.json",
    root / "Tools/Art/toon-verification.json",
    root / "Tools/Art/Illustration/generation-manifest.json",
    root / "Tools/Art/Illustration/galilee-painted-sky.json",
    root / "Tools/Art/Illustration/verification.json",
    root / "Tools/Art/Painted/characters-generation.json",
    root / "Tools/Art/Painted/backgrounds-generation.json",
    root / "Tools/Validation/painted-dawn-layouts.json",
    root / "Games/PathOfDawn/Assets/Resources/Art/Painted/layout.json",
    root / "Tools/Audio/production.json",
    root / "Tools/Audio/verification.json",
    root / "Tools/Audio/MUSIC-NOTICE.txt",
    root / "Tools/Voice/production.json",
    root / "Tools/Voice/verification.json",
    root / "Tools/Voice/models.json",
    root / "Tools/Voice/asr-model.json",
    root / "Tools/Voice/license-sources.json",
    root / "Tools/Voice/VOICE-NOTICE.txt",
    root / "Tools/Voice/Licenses/MeloTTS-MIT.txt",
    root / "Tools/Voice/Licenses/MeloTTS-Korean-model-card.md",
    root / "Tools/Voice/Licenses/LMkor-Apache-2.0.txt",
    root / "Games/PathOfDawn/Assets/Resources/Voice/opening-voice.json",
    artifacts / "Environment/pipeline-fantasycards.json",
    artifacts / "Environment/browser-prerequisites-best.json",
    artifacts / "Environment/managed-runtime.json",
    artifacts / "TestResults/Core/core-tests.trx",
    artifacts / "Validation/WebSmoke/summary.json",
    artifacts / "Validation/WebRendererProbeFinal.json",
    artifacts / "Validation/VisualAssets/final-audit.json",
    artifacts / "Validation/VisualAssets/preparation-audit.json",
    artifacts / "Validation/Portal/portal-preview.json",
    artifacts / "Validation/GitHub/source-clone.json",
    artifacts / "Validation/VisualWeb/control-mode-regression-diagnostic.json",
    artifacts / "Validation/VisualWeb/control-mode-regression-before-fix.png",
    artifacts / "WebPlaytest/build-manifest.json",
]
selected.extend(sorted((root / "Tools/Art/Previews").glob("ToonDawn_*.png")))
selected.extend(sorted((artifacts / "Validation/Portal").glob("*.png")))
selected.extend(sorted((artifacts / "Validation/VisualWeb").glob("*.json")))
selected.extend(sorted((artifacts / "Validation/GitHub").glob("expanded-demo-*.json")))
selected.extend(sorted((artifacts / "Validation/GitHub").glob("painted-dawn-*.json")))
selected.extend(sorted((artifacts / "Validation/GitHub").glob("fluid-dawn-*.json")))
selected.extend(sorted((artifacts / "Validation/GitHub").glob("fm-classic-*.json")))
selected.extend(sorted((root / "Tools/Art/ClassicCards").glob("*.json")))
for name in ("FmRevival.md", "GospelJourney.md", "GospelChronology.md", "Gospel3DArt.md", "RevivalValidation.md",
             "Research/FmRevivalSources.md", "ValidationHistory/2026-10-26-before-revival-Validation.md",
             "ValidationHistory/2026-10-26-before-revival-VisualValidation.md"):
    selected.append(root / "Docs" / name)
selected.append(root / "Tools/Environment/validation-2026-10-26-before-revival.json")
for directory in ("Tools/Data/Gospel", "Tools/Research/FmRevival", "Tools/Art/Gospel3D", "Tools/Art/GospelTemple"):
    for pattern in ("*.json", "*NOTICE.txt"):
        selected.extend(sorted((root / directory).glob(pattern)))
for pattern in ("*.json", "*.png", "*.png.txt", "*.xml", "*.trx", "executed-harness.py"):
    selected.extend(sorted((artifacts / "Validation/Revival").rglob(pattern)))
selected.extend(sorted((artifacts / "Validation/GospelJourney").glob("*.json")))
selected.extend(sorted((artifacts / "Validation/GitHub").glob("revival-*.json")))
for pattern in ("*.json", "*.md"):
    selected.extend(sorted((root / "Tools/Art/Painted").rglob(pattern)))
selected.extend(sorted((artifacts / "Validation/Subculture").rglob("*.json")))
# Collect compact reports and captures, never raw Blender/Unity logs or voice files.
for directory in ("ExpandedDemo", "Demo", "PaintedDawn", "MotionDawn", "FmExpansion", "ClassicCards"):
    for pattern in ("*.json", "*.png", "*.png.txt", "*.xml", "*.trx", "*-BuildEvidence.txt"):
        selected.extend(sorted((artifacts / "Validation" / directory).rglob(pattern)))
# Reassessments cite the exact executed harness/classifier, not the later fix.
for pattern in ("executed-harness.py", "original-shared-classifier.py"):
    selected.extend(sorted((artifacts / "Validation/ClassicCards").rglob(pattern)))
selected.extend(sorted((artifacts / "Validation/ExpandedDemo").glob("*-reference.txt")))
for profile in ("Desktop720", "Mobile844"):
    selected.append(artifacts / "Validation/PaintedDawn" / profile / "PathOfDawn/report.json")
selected.extend(sorted((artifacts / "Validation/PaintedDawn").rglob("*.txt")))
# Preserve earlier recorded outcomes alongside the current demo, including failures.
for pattern in ("*.png", "*.png.txt", "*.xml", "*.trx", "*.json", "*.md"):
    selected.extend(sorted((artifacts / "ValidationHistory").rglob(pattern)))
for pattern in ("*.json", "*.md"):
    selected.extend(sorted((root / "Tools/Art/Illustration/history").rglob(pattern)))
selected.extend(sorted((artifacts / "Validation/VisualAssets").glob("*.previous-*.json")))
selected.extend(sorted((root / "Tools/Validation/VisualReferences").glob("*.json")))
selected.extend(sorted((root / "Tools/Validation/VisualReferences").glob("*.png")))
for game in ("FantasyCards", "PathOfDawn"):
    selected.append(artifacts / f"TestResults/{game}/EditMode.xml")
    selected.extend(sorted((artifacts / "TestResults" / game).glob("*.xml")))
    for target in ("Windows", "Web"):
        selected.append(artifacts / f"Builds/{game}/{target}/BuildEvidence.txt")
    selected.append(artifacts / f"Builds/{game}/Web/OutputFiles.json")
    selected.extend(sorted((artifacts / "Screenshots" / game).glob("*.png*")))
    report_dir = artifacts / "Validation/WebSmoke" / game
    selected.append(report_dir / "report.json")
    for original_name in ("report-before-network-classification.json",
                          "report-before-capture-fallback.json"):
        original_report = report_dir / original_name
        if original_report.is_file():
            selected.append(original_report)
    for pattern in ("*.png", "*.txt"):
        selected.extend(sorted(report_dir.glob(pattern)))
    # Expanded-demo evidence remains available for unchanged FantasyCards and as
    # the PathOfDawn baseline. Current PathOfDawn reports live in PaintedDawn.
    for profile in ("Desktop720", "Desktop1080", "Mobile844"):
        current = artifacts / "Validation/VisualWeb" / profile / game
        selected.append(current / "report.json")
        for pattern in ("*.json", "*.png", "*.txt"):
            selected.extend(sorted(current.glob(pattern)))
        for previous in current.parent.glob(game + ".previous-*"):
            for pattern in ("*.json", "*.png", "*.txt"):
                selected.extend(sorted(previous.rglob(pattern)))

# Public deployment checks are produced only after Pages has actually served the build.
public = artifacts / "Validation/PublicWeb"
if public.is_dir():
    for pattern in ("*.json", "*.png", "*.txt"):
        selected.extend(sorted(public.rglob(pattern)))

missing = [str(path.relative_to(root)) for path in selected if not path.is_file()]
if missing:
    raise SystemExit("Evidence files missing: " + ", ".join(missing))
selected = sorted(set(selected))
manifest = {str(path.relative_to(root)): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in selected}
output = artifacts / "FirstGame-ValidationEvidence.zip"
with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for path in selected:
        archive.write(path, str(path.relative_to(root)))
    archive.writestr("EvidenceManifest.sha256.json", json.dumps(manifest, indent=2) + "\n")
print(f"{output.name}: {len(selected)} evidence files, {output.stat().st_size:,} bytes")
print("Includes the actual recorded outcomes, including any failed or unrun checks.")
