#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DESIGNER_PACKAGE_ROOT="$(cd "$SCRIPT_DIR/../../com.possiphil.genix" && pwd)"
SOURCE_FILES=(
  "$DESIGNER_PACKAGE_ROOT/Runtime/Core/GenerationRandom.cs"
  "$DESIGNER_PACKAGE_ROOT/Runtime/Extensions/EnumDisplayNameExtensions.cs"
)

before="$(shasum -a 256 "${SOURCE_FILES[@]}")"

verify_source_unchanged() {
  local after
  after="$(shasum -a 256 "${SOURCE_FILES[@]}")"

  if [[ "$before" != "$after" ]]; then
    printf '%s\n' "Mutation testing changed a source file unexpectedly. Review the working tree immediately." >&2
    exit 2
  fi
}

trap verify_source_unchanged EXIT

cd "$SCRIPT_DIR"
dotnet test Genix.Mutation.Tests.csproj
dotnet tool run dotnet-stryker -- --config-file stryker-config.json
