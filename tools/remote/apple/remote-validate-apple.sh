#!/bin/bash
set -u

RUN_ID=""
PLATFORM="ios"
PROJECT_PATH="games/3D/SilkTriangleSample/SilkTriangleSample.csproj"
CONFIGURATION="Debug"
WAIT_SECONDS=8
SIMULATOR_ID=""
NO_SCREENSHOT=0

PURPLE="\033[38;2;216;180;254m"
BLUE="\033[38;2;147;197;253m"
GREEN="\033[38;2;134;239;172m"
RED="\033[38;2;252;165;165m"
MUTED="\033[38;2;148;163;184m"
RESET="\033[0m"

say() {
    printf "%b%s%b\n" "$1" "$2" "$RESET"
}

progress() {
    local percent="$1"
    local label="$2"
    local filled=$((34 * percent / 100))
    local bar=""
    local i

    for ((i=0; i<34; i++)); do
        if ((i < filled)); then
            if ((i < 17)); then
                bar="$bar$PURPLE━"
            else
                bar="$bar$BLUE━"
            fi
        else
            bar="$bar\033[38;2;71;85;105m━"
        fi
    done

    printf "\r\033[2K%b%b %3d%% %s%b\n" "$bar" "$RESET" "$percent" "$label" "$RESET"
}

fail() {
    say "$RED" "ERROR: $1"
    exit 1
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --platform) PLATFORM="$2"; shift 2 ;;
        --run-id) RUN_ID="$2"; shift 2 ;;
        --project-path) PROJECT_PATH="$2"; shift 2 ;;
        --configuration) CONFIGURATION="$2"; shift 2 ;;
        --wait-seconds) WAIT_SECONDS="$2"; shift 2 ;;
        --simulator-id) SIMULATOR_ID="$2"; shift 2 ;;
        --no-screenshot) NO_SCREENSHOT=1; shift ;;
        *) fail "Unknown argument: $1" ;;
    esac
done

[ -n "$RUN_ID" ] || fail "--run-id is required."

case "$PLATFORM" in
    ios|maccatalyst) ;;
    *) fail "Unsupported platform: $PLATFORM" ;;
esac

ROOT="$(cd "$(dirname "$0")/../../../.." && pwd)"
ARTIFACTS="$ROOT/artifacts"
mkdir -p "$ARTIFACTS"

BUILD_LOG="$ARTIFACTS/build-run.log"
STDOUT_LOG="$ARTIFACTS/stdout.log"
STDERR_LOG="$ARTIFACTS/stderr.log"
VALIDATION_JSON="$ARTIFACTS/validation.json"
SCREENSHOT="$ARTIFACTS/screenshot.png"

progress 8 "Checking Apple toolchain..."
command -v dotnet >/dev/null 2>&1 || fail "dotnet was not found."
command -v xcrun >/dev/null 2>&1 || fail "xcrun was not found."
xcode-select -p > "$ARTIFACTS/xcode-path.txt" 2>&1 || fail "Xcode command-line tools are not configured."
dotnet --version > "$ARTIFACTS/dotnet-version.txt" 2>&1
xcodebuild -version > "$ARTIFACTS/xcode-version.txt" 2>&1 || true

if [ "$PLATFORM" = "ios" ]; then
    progress 20 "Preparing iOS Simulator..."

    if [ -z "$SIMULATOR_ID" ]; then
        SIMULATOR_ID="$(xcrun simctl list devices available | sed -n 's/.*iPhone[^()]* (\([A-F0-9-]*\)) (Shutdown).*/\1/p' | head -n 1)"
    fi

    [ -n "$SIMULATOR_ID" ] || fail "No available iPhone Simulator was found. Pass --simulator-id."

    xcrun simctl boot "$SIMULATOR_ID" >/dev/null 2>&1 || true
    xcrun simctl bootstatus "$SIMULATOR_ID" -b
    xcrun simctl list devices | grep "$SIMULATOR_ID" > "$ARTIFACTS/simulator.txt" || true
fi

progress 34 "Building and launching $PLATFORM..."

if [ "$PLATFORM" = "ios" ]; then
    dotnet build "$PROJECT_PATH" \
        -c "$CONFIGURATION" \
        -f net10.0-ios \
        -p:_DeviceName=":v2:udid=$SIMULATOR_ID" \
        -t:Run > "$BUILD_LOG" 2>&1
else
    dotnet build "$PROJECT_PATH" \
        -c "$CONFIGURATION" \
        -f net10.0-maccatalyst \
        -t:Run > "$BUILD_LOG" 2>&1
fi

BUILD_EXIT=$?
cat "$BUILD_LOG"

[ "$BUILD_EXIT" -eq 0 ] || fail "Apple build/run failed with exit code $BUILD_EXIT."

progress 58 "Waiting for the rendered frame..."
sleep "$WAIT_SECONDS"

if [ "$NO_SCREENSHOT" -eq 0 ]; then
    if [ "$PLATFORM" = "ios" ]; then
        progress 70 "Capturing iOS Simulator screenshot..."
        xcrun simctl io "$SIMULATOR_ID" screenshot "$SCREENSHOT" >/dev/null
    else
        progress 70 "Capturing Mac Catalyst desktop screenshot..."
        screencapture -x "$SCREENSHOT" >/dev/null 2>&1 || true
    fi
fi

progress 82 "Collecting runtime diagnostics..."

if [ "$PLATFORM" = "ios" ]; then
    xcrun simctl spawn "$SIMULATOR_ID" log show --last 2m --style compact > "$STDOUT_LOG" 2>&1 || true
else
    log show --last 2m --style compact > "$STDOUT_LOG" 2>&1 || true
fi

grep -Ei "Orbit3D|Silk|draw|triangle|texture|context|metric|error|exception" \
    "$STDOUT_LOG" > "$ARTIFACTS/relevant-log.txt" 2>/dev/null || true

SCREENSHOT_STATE="skipped"
if [ "$NO_SCREENSHOT" -eq 0 ] && [ -f "$SCREENSHOT" ]; then
    SCREENSHOT_STATE="captured"
fi

: > "$STDERR_LOG"

cat > "$VALIDATION_JSON" <<EOF
{
  "runId": "$RUN_ID",
  "platform": "$PLATFORM",
  "configuration": "$CONFIGURATION",
  "project": "$PROJECT_PATH",
  "buildAndLaunch": "passed",
  "screenshot": "$SCREENSHOT_STATE",
  "simulatorId": "$SIMULATOR_ID",
  "artifacts": [
    "build-run.log",
    "stdout.log",
    "stderr.log",
    "relevant-log.txt",
    "validation.json"
  ]
}
EOF

progress 100 "Validation completed."
say "$GREEN" "Apple validation completed successfully."
say "$MUTED" "Artifacts: $ARTIFACTS"
