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
    root / "Docs/PlaytestGuide.md",
    root / "Docs/UnitySmokeTests.md",
    root / "Docs/CoreValidation.md",
    root / "Docs/ArtDirection.md",
    root / "Tools/Environment/validation.json",
    root / "Tools/Art/verification.json",
    artifacts / "Environment/pipeline-fantasycards.json",
    artifacts / "TestResults/Core/core-tests.trx",
    artifacts / "Validation/WebSmoke/summary.json",
    artifacts / "Validation/WebRendererProbeFinal.json",
]
for game in ("FantasyCards", "PathOfDawn"):
    selected.append(artifacts / f"TestResults/{game}/EditMode.xml")
    for target in ("Windows", "Web"):
        selected.append(artifacts / f"Builds/{game}/{target}/BuildEvidence.txt")
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
