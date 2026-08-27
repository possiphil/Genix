#!/bin/sh

set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PACKAGE_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
UNITY_PROJECT=$(CDPATH= cd -- "$PACKAGE_ROOT/../../../space-foundation-system" && pwd)

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
}

build_dependency() {
    project=$1

    dotnet build "$UNITY_PROJECT/$project.csproj" \
        --no-restore \
        --disable-build-servers \
        -m:1 \
        -v:q \
        -clp:ErrorsOnly \
        -t:Rebuild \
        -p:BuildProjectReferences=false
}

build_dependency DyrdaDev.SpaceFoundationSystemForUnity.Runtime
build_dependency DyrdaDev.SpaceFoundationSystemForUnity.Editor
build_assembly Genix.Runtime
build_assembly Genix.Editor.Common
build_assembly Genix.SpaceFoundation.Editor
build_assembly Genix.Editor

"$DOCFX" "$PACKAGE_ROOT/Documentation~/docfx.json"
