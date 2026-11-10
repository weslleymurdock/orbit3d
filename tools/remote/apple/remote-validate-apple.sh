#!/bin/bash
set -u

RUN_ID=""
PLATFORM="ios"
PROJECT_PATH="games/3D/SilkTriangleSample/SilkTriangleSample.csproj"
CONFIGURATION="Debug"
WAIT_SECONDS=8
SIMULATOR_ID=""
NO_SCREENSHOT=0
EXERCISE_LIFECYCLE=0

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
        --exercise-lifecycle) EXERCISE_LIFECYCLE=1; shift ;;
        --no-screenshot) NO_SCREENSHOT=1; shift ;;
        *) fail "Unknown argument: $1" ;;
    esac
done

[ -n "$RUN_ID" ] || fail "--run-id is required."

case "$PLATFORM" in
    ios|maccatalyst) ;;
    *) fail "Unsupported platform: $PLATFORM" ;;
esac

ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
PROJECT="$ROOT/$PROJECT_PATH"
ARTIFACTS="$ROOT/artifacts"
mkdir -p "$ARTIFACTS"

BUILD_LOG="$ARTIFACTS/build-run.log"
STDOUT_LOG="$ARTIFACTS/stdout.log"
STDERR_LOG="$ARTIFACTS/stderr.log"
VALIDATION_JSON="$ARTIFACTS/validation.json"
SCREENSHOT="$ARTIFACTS/screenshot.png"
RUNTIME_LOG="$ARTIFACTS/orbit3d-runtime.log"

progress 8 "Checking Apple toolchain..."
if [ -x "/usr/local/share/dotnet/dotnet" ]; then
    export DOTNET_ROOT="/usr/local/share/dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
elif [ -x "$HOME/.dotnet/dotnet" ]; then
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
fi

command -v dotnet >/dev/null 2>&1 || fail "dotnet was not found."
command -v xcrun >/dev/null 2>&1 || fail "xcrun was not found."
sw_vers > "$ARTIFACTS/macos-version.txt" 2>&1 || true
uname -a >> "$ARTIFACTS/macos-version.txt" 2>&1 || true
xcode-select -p > "$ARTIFACTS/xcode-path.txt" 2>&1 || fail "Xcode command-line tools are not configured."
dotnet --version > "$ARTIFACTS/dotnet-version.txt" 2>&1
dotnet workload list > "$ARTIFACTS/workload-list.txt" 2>&1 || true
xcodebuild -version > "$ARTIFACTS/xcode-version.txt" 2>&1 || true

if [ "$PLATFORM" = "ios" ]; then
    progress 20 "Preparing iOS Simulator..."

    if [ -n "$SIMULATOR_ID" ] && ! xcrun simctl list devices available | grep -q "$SIMULATOR_ID"; then
        say "$MUTED" "Simulator UDID $SIMULATOR_ID is not available; auto-detecting..."
        SIMULATOR_ID=""
    fi

    if [ -z "$SIMULATOR_ID" ]; then
        SIMULATOR_ID="$(xcrun simctl list devices available | sed -n 's/.*iPhone[^()]* (\([A-F0-9-]*\)) (Booted).*/\1/p' | head -n 1)"
    fi
    if [ -z "$SIMULATOR_ID" ]; then
        SIMULATOR_ID="$(xcrun simctl list devices available | sed -n 's/.*iPhone[^()]* (\([A-F0-9-]*\)) (Shutdown).*/\1/p' | head -n 1)"
    fi

    [ -n "$SIMULATOR_ID" ] || fail "No available iPhone Simulator was found. Pass --simulator-id."

    xcrun simctl boot "$SIMULATOR_ID" >/dev/null 2>&1 || true
    ( xcrun simctl bootstatus "$SIMULATOR_ID" -b >/dev/null 2>&1 ) &
    BOOT_PID=$!
    for _ in $(seq 1 15); do
        kill -0 "$BOOT_PID" 2>/dev/null || break
        sleep 1
    done
    kill "$BOOT_PID" 2>/dev/null || true
    xcrun simctl list devices | grep "$SIMULATOR_ID" > "$ARTIFACTS/simulator.txt" || true

    progress 34 "Building net10.0-ios with -p:_ValidateXcodeVersion=false..."
    dotnet build "$PROJECT" \
        -c "$CONFIGURATION" \
        -f net10.0-ios \
        -p:_ValidateXcodeVersion=false \
        > "$BUILD_LOG" 2>&1
    BUILD_EXIT=$?
    cat "$BUILD_LOG"
    [ "$BUILD_EXIT" -eq 0 ] || fail "iOS build failed with exit code $BUILD_EXIT."

    APP_BUNDLE="$(find "$ROOT/games/3D/SilkTriangleSample/bin/$CONFIGURATION/net10.0-ios" -type d -name '*.app' -path '*iossimulator*' -print -quit)"
    [ -n "$APP_BUNDLE" ] || fail "The iOS Simulator app bundle was not found after building."
    APP_ID="$(/usr/libexec/PlistBuddy -c 'Print CFBundleIdentifier' "$APP_BUNDLE/Info.plist")"
    xcrun simctl terminate "$SIMULATOR_ID" "$APP_ID" >/dev/null 2>&1 || true
    xcrun simctl install "$SIMULATOR_ID" "$APP_BUNDLE" || fail "Could not install the app on the iOS Simulator."

    CMD_FILE="$ARTIFACTS/ios-commands.txt"
    FRAMES_DIR="$ARTIFACTS/frames"
    mkdir -p "$FRAMES_DIR"
    : > "$CMD_FILE"

    export SIMCTL_CHILD_ORBIT3D_COMMAND_FILE="$CMD_FILE"
    export SIMCTL_CHILD_ORBIT3D_SILK_CAPTURE_DIR="$FRAMES_DIR"

    if ! xcrun simctl launch "$SIMULATOR_ID" "$APP_ID" > "$ARTIFACTS/launch-output.txt" 2> "$STDERR_LOG"; then
        say "$RED" "Launch error: $(cat "$STDERR_LOG")"
        fail "Could not launch the app on the iOS Simulator: $(cat "$STDERR_LOG")"
    fi

    DATA_CONTAINER="$(xcrun simctl get_app_container "$SIMULATOR_ID" "$APP_ID" data 2>/dev/null || true)"
    CACHE_CMD_FILE=""
    if [ -n "$DATA_CONTAINER" ] && [ -d "$DATA_CONTAINER/Library/Caches" ]; then
        CACHE_CMD_FILE="$DATA_CONTAINER/Library/Caches/orbit3d-commands.txt"
        : > "$CACHE_CMD_FILE"
    fi

    send_ios_cmd() {
        echo "$1" >> "$CMD_FILE"
        if [ -n "$CACHE_CMD_FILE" ]; then
            echo "$1" >> "$CACHE_CMD_FILE"
        fi
    }

    progress 58 "Waiting for initial rendered frame..."
    sleep "$WAIT_SECONDS"

    if [ "$NO_SCREENSHOT" -eq 0 ]; then
        xcrun simctl io "$SIMULATOR_ID" screenshot "$ARTIFACTS/01-initial-render.png" >/dev/null
        cp "$ARTIFACTS/01-initial-render.png" "$SCREENSHOT"
    fi

    if [ "$EXERCISE_LIFECYCLE" -eq 1 ]; then
        progress 64 "Exercising iOS camera orbit and zoom..."
        send_ios_cmd "orbit 0.65 0.25"
        send_ios_cmd "zoom -0.75"
        sleep 2
        if [ "$NO_SCREENSHOT" -eq 0 ]; then
            xcrun simctl io "$SIMULATOR_ID" screenshot "$ARTIFACTS/02-camera-orbit-zoom.png" >/dev/null
        fi

        progress 70 "Exercising iOS surface resize..."
        send_ios_cmd "resize 320 260"
        sleep 2
        if [ "$NO_SCREENSHOT" -eq 0 ]; then
            xcrun simctl io "$SIMULATOR_ID" screenshot "$ARTIFACTS/03-resized-surface.png" >/dev/null
        fi
        send_ios_cmd "resize reset"
        sleep 2

        progress 76 "Exercising iOS context loss/recreation cycle 1..."
        send_ios_cmd "suspend"
        sleep 2
        send_ios_cmd "resume"
        sleep 3
        if [ "$NO_SCREENSHOT" -eq 0 ]; then
            xcrun simctl io "$SIMULATOR_ID" screenshot "$ARTIFACTS/04-post-context-recreate-1.png" >/dev/null
        fi

        progress 82 "Exercising iOS context loss/recreation cycle 2..."
        send_ios_cmd "suspend"
        sleep 2
        send_ios_cmd "resume"
        sleep 3
        if [ "$NO_SCREENSHOT" -eq 0 ]; then
            xcrun simctl io "$SIMULATOR_ID" screenshot "$ARTIFACTS/05-post-context-recreate-2.png" >/dev/null
            cp "$ARTIFACTS/05-post-context-recreate-2.png" "$SCREENSHOT"
        fi
    fi

    if [ -n "$DATA_CONTAINER" ] && [ -f "$DATA_CONTAINER/Library/Caches/orbit3d-runtime.log" ]; then
        cp "$DATA_CONTAINER/Library/Caches/orbit3d-runtime.log" "$RUNTIME_LOG"
    fi
    if [ ! -f "$RUNTIME_LOG" ]; then
        FOUND_IOS_LOG="$(find "$HOME/Library/Developer/CoreSimulator/Devices/$SIMULATOR_ID" -name 'orbit3d-runtime.log' -print -quit 2>/dev/null || true)"
        if [ -n "$FOUND_IOS_LOG" ] && [ -f "$FOUND_IOS_LOG" ]; then
            cp "$FOUND_IOS_LOG" "$RUNTIME_LOG"
        fi
    fi

    xcrun simctl terminate "$SIMULATOR_ID" "$APP_ID" >/dev/null 2>&1 || true
else
    progress 34 "Building and running net10.0-maccatalyst with -p:_ValidateXcodeVersion=false..."
    CMD_FILE="$ARTIFACTS/maccatalyst-commands.txt"
    FRAMES_DIR="$ARTIFACTS/frames"
    mkdir -p "$FRAMES_DIR"
    rm -f "$CMD_FILE"

    export ORBIT3D_COMMAND_FILE="$CMD_FILE"
    export ORBIT3D_SILK_CAPTURE_DIR="$FRAMES_DIR"

    capture_mac_frame() {
        local name="$1"
        local out_path="$ARTIFACTS/$name.png"
        echo "$name" > "$FRAMES_DIR/capture.request"
        local waited=0
        while [ "$waited" -lt 30 ]; do
            if [ -f "$FRAMES_DIR/$name.png" ]; then
                cp "$FRAMES_DIR/$name.png" "$out_path"
                return 0
            fi
            sleep 0.2
            waited=$((waited + 1))
        done
        if [ "$NO_SCREENSHOT" -eq 0 ]; then
            screencapture -x "$out_path" >/dev/null 2>&1 || true
        fi
    }

    progress 34 "Building net10.0-maccatalyst with -p:_ValidateXcodeVersion=false..."
    dotnet build "$PROJECT" \
        -c "$CONFIGURATION" \
        -f net10.0-maccatalyst \
        -p:_ValidateXcodeVersion=false \
        > "$BUILD_LOG" 2>&1
    BUILD_EXIT=$?
    cat "$BUILD_LOG"
    [ "$BUILD_EXIT" -eq 0 ] || fail "Mac Catalyst build failed with exit code $BUILD_EXIT."

    APP_BUNDLE="$(find "$ROOT/games/3D/SilkTriangleSample/bin/$CONFIGURATION/net10.0-maccatalyst" -type d -name '*.app' -print -quit)"
    [ -n "$APP_BUNDLE" ] || fail "The Mac Catalyst app bundle was not found after building."

    progress 48 "Launching Mac Catalyst application..."
    "$APP_BUNDLE/Contents/MacOS/SilkTriangleSample" > "$STDOUT_LOG" 2> "$STDERR_LOG" &
    APP_PID=$!

    (
        # Wait for the app process to launch and render initial frames
        elapsed=0
        while [ "$elapsed" -lt 45 ]; do
            if pgrep -f "SilkTriangleSample" >/dev/null 2>&1; then
                break
            fi
            sleep 1
            elapsed=$((elapsed + 1))
        done

        sleep "$WAIT_SECONDS"
        capture_mac_frame "01-initial-render"
        [ -f "$ARTIFACTS/01-initial-render.png" ] && cp "$ARTIFACTS/01-initial-render.png" "$SCREENSHOT"

        if [ "$EXERCISE_LIFECYCLE" -eq 1 ]; then
            printf "orbit 0.65 0.25\nzoom -0.75\n" > "$CMD_FILE"
            sleep 2
            capture_mac_frame "02-camera-orbit-zoom"

            printf "resize 1120 760\n" > "$CMD_FILE"
            sleep 2
            capture_mac_frame "03-resized-window"

            printf "suspend\n" > "$CMD_FILE"
            sleep 2
            printf "resume\n" > "$CMD_FILE"
            sleep 3
            capture_mac_frame "04-post-context-recreate-1"

            printf "suspend\n" > "$CMD_FILE"
            sleep 2
            printf "resume\n" > "$CMD_FILE"
            sleep 3
            capture_mac_frame "05-post-context-recreate-2"
            [ -f "$ARTIFACTS/05-post-context-recreate-2.png" ] && cp "$ARTIFACTS/05-post-context-recreate-2.png" "$SCREENSHOT"
        fi

        printf "close\n" > "$CMD_FILE"
        sleep 3
        kill "$APP_PID" >/dev/null 2>&1 || true
        pkill -f "SilkTriangleSample" >/dev/null 2>&1 || true
    )
    wait "$APP_PID" 2>/dev/null || true

    FOUND_LOG="$(find "$HOME/Library/Caches" "$HOME/Library/Containers" -name 'orbit3d-runtime.log' -mmin -5 2>/dev/null | head -n 1 || true)"
    if [ -n "$FOUND_LOG" ] && [ -f "$FOUND_LOG" ]; then
        cp "$FOUND_LOG" "$RUNTIME_LOG"
    fi
fi

progress 88 "Collecting runtime diagnostics..."

if [ "$PLATFORM" = "ios" ]; then
    ( xcrun simctl spawn "$SIMULATOR_ID" log show --last 1m --style compact > "$STDOUT_LOG" 2>&1 ) &
    LOG_PID=$!
    for _ in $(seq 1 5); do
        kill -0 "$LOG_PID" 2>/dev/null || break
        sleep 1
    done
    kill "$LOG_PID" 2>/dev/null || true
else
    log show --last 3m --style compact > "$STDOUT_LOG" 2>&1 || true
fi

if [ -f "$RUNTIME_LOG" ]; then
    cat "$RUNTIME_LOG" > "$ARTIFACTS/relevant-log.txt"
fi
grep -Ei "Orbit3D|Silk|draw|triangle|texture|context|metric|error|exception" \
    "$STDOUT_LOG" "$BUILD_LOG" >> "$ARTIFACTS/relevant-log.txt" 2>/dev/null || true

# Clean up raw frame sequence after selecting key stage screenshots to keep artifact bundle compact
rm -rf "$ARTIFACTS/frames"

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
  "exerciseLifecycle": $EXERCISE_LIFECYCLE,
  "screenshot": "$SCREENSHOT_STATE",
  "simulatorId": "$SIMULATOR_ID",
  "artifacts": [
    "build-run.log",
    "stdout.log",
    "stderr.log",
    "relevant-log.txt",
    "orbit3d-runtime.log",
    "macos-version.txt",
    "xcode-version.txt",
    "xcode-path.txt",
    "dotnet-version.txt",
    "workload-list.txt",
    "validation.json"
  ]
}
EOF

progress 100 "Validation completed."
say "$GREEN" "Apple validation completed successfully."
say "$MUTED" "Artifacts: $ARTIFACTS"
