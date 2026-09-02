#!/bin/sh

set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PACKAGE_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
EMBEDDED_PROJECT=$(CDPATH= cd -- "$PACKAGE_ROOT/../.." && pwd)

if [ -z "${UNITY_PROJECT:-}" ]; then
    if [ -d "$EMBEDDED_PROJECT/Assets" ] && [ -d "$EMBEDDED_PROJECT/ProjectSettings" ]; then
        UNITY_PROJECT=$EMBEDDED_PROJECT
    else
        echo "Set UNITY_PROJECT to a Unity project that has the Genix packages installed." >&2
        exit 1
    fi
fi

if [ ! -d "$UNITY_PROJECT/Assets" ] || [ ! -d "$UNITY_PROJECT/ProjectSettings" ]; then
    echo "UNITY_PROJECT must point to the Genix Unity project." >&2
    exit 1
fi

DOCFX=${DOCFX:-}
if [ -z "$DOCFX" ]; then
    if command -v docfx >/dev/null 2>&1; then
        DOCFX=docfx
    elif [ -x "$HOME/.dotnet/tools/docfx" ]; then
        DOCFX="$HOME/.dotnet/tools/docfx"
    else
        echo "DocFX is not installed. Run: dotnet tool install -g docfx" >&2
        exit 1
    fi
fi

build_assembly() {
    project=$1
    output="$UNITY_PROJECT/Temp/Bin/Debug/$project"
    artifacts="$SCRIPT_DIR/.artifacts/$project"

    dotnet build "$UNITY_PROJECT/$project.csproj" \
        --no-restore \
        --disable-build-servers \
        -m:1 \
        -v:q \
        -clp:ErrorsOnly \
        -t:Rebuild \
        -p:BuildProjectReferences=false \
        -p:DocumentationFile="$output/$project.xml" \
        -p:WarningsAsErrors=1591%3B1570%3B1587

    mkdir -p "$artifacts"
    cp "$output"/*.dll "$artifacts/"
    cp "$output/$project.xml" "$artifacts/$project.xml"
}

build_assembly Genix.Runtime
build_assembly Genix.Editor.Common
build_assembly Genix.SpaceFoundation.Editor
build_assembly Genix.Editor

rm -rf "$SCRIPT_DIR/api" "$SCRIPT_DIR/_site"
"$DOCFX" "$PACKAGE_ROOT/Documentation~/docfx.json"
