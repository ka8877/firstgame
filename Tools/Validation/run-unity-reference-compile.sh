#!/usr/bin/env bash
set -euo pipefail
repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
dotnet_command="${DOTNET_COMMAND:-dotnet}"
unity_editor_data="${UNITY_EDITOR_DATA:?Set UNITY_EDITOR_DATA to the official Unity 6000.3.26f1 Editor/Data directory}"
cinemachine_package="${CINEMACHINE_PACKAGE:?Set CINEMACHINE_PACKAGE to the extracted official Cinemachine 3.1.7 package directory}"
results="$repository_root/Artifacts/ReferenceCompile"
mkdir -p "$results"
"$dotnet_command" build "$repository_root/Tools/Validation/UnityReferenceCompile.csproj" --configuration Release \
  "-p:UnityEditorData=$unity_editor_data" "-p:CinemachinePackage=$cinemachine_package" \
  2>&1 | tee "$results/runtime.log"
for game_project in FantasyCards PathOfDawn; do
  "$dotnet_command" build "$repository_root/Tools/Validation/UnityEditorReferenceCompile.csproj" --configuration Release \
    "-p:UnityEditorData=$unity_editor_data" "-p:CinemachinePackage=$cinemachine_package" "-p:GameProject=$game_project" \
    2>&1 | tee "$results/editor-$game_project.log"
done
