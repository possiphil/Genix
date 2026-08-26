#!/bin/zsh

set -eu

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY_PROJECT="${GENIX_UNITY_PROJECT:-$SCRIPT_DIR/../../space-foundation-system}"
SUITE_PATH="${1:-Assets/Genix/Benchmarks/ThesisPerformanceSuite.asset}"
SCENARIO_INDEX="${2:--1}"
PREPARE_ENVIRONMENT="${3:-true}"
TIMEOUT_SECONDS="${GENIX_BENCHMARK_TIMEOUT:-7200}"
COMMAND_DIR="$UNITY_PROJECT/Library/Genix"
REQUEST_PATH="$COMMAND_DIR/BenchmarkCommandRequest.json"
RESPONSE_PATH="$COMMAND_DIR/BenchmarkCommandResponse.json"
trap 'rm -f "$REQUEST_PATH"' EXIT

if [[ ! -d "$UNITY_PROJECT/ProjectSettings" ]]; then
    echo "Unity project not found at: $UNITY_PROJECT" >&2
    exit 2
fi

mkdir -p "$COMMAND_DIR"
rm -f "$RESPONSE_PATH"
printf '{"suiteAssetPath":"%s","scenarioIndex":%s,"prepareEnvironment":%s}\n' \
    "$SUITE_PATH" "$SCENARIO_INDEX" "$PREPARE_ENVIRONMENT" > "$REQUEST_PATH"
echo "Requested Genix benchmark from the open Unity editor..."
START_TIME="$(date +%s)"

while [[ ! -f "$RESPONSE_PATH" ]]; do
    CURRENT_TIME="$(date +%s)"
    if (( CURRENT_TIME - START_TIME >= TIMEOUT_SECONDS )); then
        echo "Timed out after ${TIMEOUT_SECONDS}s waiting for Unity." >&2
        exit 3
    fi
    sleep 0.25
done

ERROR="$(jq -r '.error // empty' "$RESPONSE_PATH")"
if [[ -n "$ERROR" ]]; then
    echo "$ERROR" >&2
    exit 4
fi

jq -r '
    "Status: \(.status)",
    "Runs: \(.completedRuns), failed: \(.failedRuns), incomplete: \(.incompleteRuns), semantic mismatches: \(.semanticMismatches)",
    "Export: \(.outputDirectory)"
' "$RESPONSE_PATH"

FAILED_COUNT="$(jq -r '.failedRuns' "$RESPONSE_PATH")"
INCOMPLETE_COUNT="$(jq -r '.incompleteRuns' "$RESPONSE_PATH")"
MISMATCH_COUNT="$(jq -r '.semanticMismatches' "$RESPONSE_PATH")"
if (( FAILED_COUNT > 0 || INCOMPLETE_COUNT > 0 || MISMATCH_COUNT > 0 )); then
    exit 1
fi
