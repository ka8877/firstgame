#!/usr/bin/env python3
"""Archive an explicit allowlist of validation evidence, excluding raw tool logs."""
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
    root / "Docs/VisualValidation.md",
    root / "Docs/WebPlaytest.md",
    root / "Docs/Environment.md",
    root / "Docs/Illustration.md",
    root / "Tools/Art/ToonDawn.md",
    root / "Tools/Environment/validation.json",
    root / "Tools/Environment/validation-2026-10-20.json",
    root / "Tools/Art/verification.json",
    root / "Tools/Art/toon-verification.json",
    root / "Tools/Art/Illustration/verification.json",
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
]
selected.extend(sorted((root / "Tools/Art/Previews").glob("ToonDawn_*.png")))
selected.extend(sorted((artifacts / "Validation/Portal").glob("*.png")))
selected.extend(sorted((artifacts / "Validation/VisualWeb").glob("*.json")))
selected.extend(sorted((artifacts / "Validation/VisualAssets").glob("*.previous-*.json")))
selected.extend(sorted((root / "Tools/Validation/VisualReferences").glob("*.json")))
selected.extend(sorted((root / "Tools/Validation/VisualReferences").glob("*.png")))
for game in ("FantasyCards", "PathOfDawn"):
    selected.append(artifacts / f"TestResults/{game}/EditMode.xml")
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
    # The dated baseline remains available; these are the current visual-refresh checks.
    for profile in ("Desktop720", "Desktop1080", "Mobile844"):
        current = artifacts / "Validation/VisualWeb" / profile / game
        selected.append(current / "report.json")
        for pattern in ("*.png", "*.txt"):
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
