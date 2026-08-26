#!/bin/zsh

set -eu

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY_PROJECT="${GENIX_UNITY_PROJECT:-$SCRIPT_DIR/../../space-foundation-system}"
PRESET="${1:-Quick}"
TIMEOUT_SECONDS="${GENIX_TEST_TIMEOUT:-900}"

case "$PRESET" in
    Quick|quick) PRESET="Quick" ;;
    Full|full) PRESET="Full" ;;
    Stress|stress) PRESET="Stress" ;;
    *)
        echo "Preset must be Quick, Full, or Stress." >&2
        exit 2
        ;;
esac

COMMAND_DIR="$UNITY_PROJECT/Library/Genix"
REQUEST_PATH="$COMMAND_DIR/TestCommandRequest.json"
RESPONSE_PATH="$COMMAND_DIR/TestCommandResponse.json"
trap 'rm -f "$REQUEST_PATH"' EXIT

if [[ ! -d "$UNITY_PROJECT/ProjectSettings" ]]; then
    echo "Unity project not found at: $UNITY_PROJECT" >&2
    echo "Set GENIX_UNITY_PROJECT to override the default path." >&2
    exit 2
fi

mkdir -p "$COMMAND_DIR"
rm -f "$RESPONSE_PATH"
printf '{"preset":"%s"}\n' "$PRESET" > "$REQUEST_PATH"

echo "Requested Genix $PRESET tests from the open Unity editor..."
START_TIME="$(date +%s)"

while [[ ! -f "$RESPONSE_PATH" ]]; do
    CURRENT_TIME="$(date +%s)"

    if (( CURRENT_TIME - START_TIME >= TIMEOUT_SECONDS )); then
        rm -f "$REQUEST_PATH"
        echo "Timed out after ${TIMEOUT_SECONDS}s waiting for Unity." >&2
        exit 3
    fi

    sleep 0.25
done

if jq -e '.error' "$RESPONSE_PATH" >/dev/null 2>&1; then
    jq -r '.error' "$RESPONSE_PATH" >&2
    exit 4
fi

jq -r '
    "Preset: \(.runPreset)",
    "Tests: \(.testResults | length), Failed: \([.testResults[] | select(.resultState | startswith("Failed"))] | length), Property cases: \(.executedPropertyCases), Duration: \(.runDurationSeconds)s",
    (.testResults[] | select(.resultState | startswith("Failed")) |
        "\nFAILED \(.fullName)\n\(.message)\n\(.stackTrace)")
' "$RESPONSE_PATH"

FAILED_COUNT="$(jq '[.testResults[] | select(.resultState | startswith("Failed"))] | length' "$RESPONSE_PATH")"

if (( FAILED_COUNT > 0 )); then
    exit 1
fi
