#!/bin/zsh

set -eu

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY_PROJECT="${GENIX_UNITY_PROJECT:-$SCRIPT_DIR/../../../../space-foundation-system}"
SUITE_PATH="${1:-Assets/Genix/Evaluations/Suites/ThesisQualityEvaluation.asset}"
SCENARIO_INDEX="${2:--1}"
REFRESH_SUITE="${3:-false}"
TIMEOUT_SECONDS="${GENIX_EVALUATION_TIMEOUT:-7200}"
COMMAND_DIR="$UNITY_PROJECT/Library/Genix"
REQUEST_PATH="$COMMAND_DIR/EvaluationCommandRequest.json"
RESPONSE_PATH="$COMMAND_DIR/EvaluationCommandResponse.json"
trap 'rm -f "$REQUEST_PATH"' EXIT

if [[ ! -d "$UNITY_PROJECT/ProjectSettings" ]]; then
    echo "Unity project not found at: $UNITY_PROJECT" >&2
    exit 2
fi

mkdir -p "$COMMAND_DIR"
rm -f "$RESPONSE_PATH"
printf '{"suiteAssetPath":"%s","scenarioIndex":%s,"refreshThesisSuite":%s}\n' \
    "$SUITE_PATH" "$SCENARIO_INDEX" "$REFRESH_SUITE" > "$REQUEST_PATH"
echo "Requested Genix evaluation from the open Unity editor..."
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
    "Scope: \(.runScope), campaign complete: \(.campaignCompleted), cancelled: \(.campaignCancelled)",
    "Runs: \(.completedRuns)/\(.expectedRuns), automatic failures: \(.failedRuns), incomplete evidence: \(.incompleteRuns)",
    "Invalid visual evidence: \(.invalidReviewRuns), missing layout assets: \(.missingLayoutAssets)",
    "Report: \(.reportAssetPath)",
    "Export: \(.outputDirectory)"
' "$RESPONSE_PATH"

FAILED_COUNT="$(jq -r '.failedRuns' "$RESPONSE_PATH")"
INCOMPLETE_COUNT="$(jq -r '.incompleteRuns' "$RESPONSE_PATH")"
INVALID_REVIEW_COUNT="$(jq -r '.invalidReviewRuns' "$RESPONSE_PATH")"
MISSING_LAYOUT_COUNT="$(jq -r '.missingLayoutAssets' "$RESPONSE_PATH")"
COMPLETED_COUNT="$(jq -r '.completedRuns' "$RESPONSE_PATH")"
EXPECTED_COUNT="$(jq -r '.expectedRuns' "$RESPONSE_PATH")"
CAMPAIGN_COMPLETE="$(jq -r '.campaignCompleted' "$RESPONSE_PATH")"
if [[ "$CAMPAIGN_COMPLETE" != "true" ]] ||
   (( COMPLETED_COUNT != EXPECTED_COUNT || FAILED_COUNT > 0 || INCOMPLETE_COUNT > 0 ||
      INVALID_REVIEW_COUNT > 0 || MISSING_LAYOUT_COUNT > 0 )); then
    exit 1
fi
