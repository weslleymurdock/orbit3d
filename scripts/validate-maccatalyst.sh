#!/usr/bin/env bash
# Launches the Mac Catalyst SilkTriangleSample with validation hooks enabled and logs stdout/stderr.
# Usage: scripts/validate-maccatalyst.sh <run-name> [seconds]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RUN="${1:-run}"
SECONDS_TO_RUN="${2:-8}"
OUT="$ROOT/artifacts/apple/stage-09"
APP="$ROOT/games/3D/SilkTriangleSample/bin/Debug/net10.0-maccatalyst/maccatalyst-x64/SilkTriangleSample.app"
[ -d "$APP" ] || APP="$ROOT/games/3D/SilkTriangleSample/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/SilkTriangleSample.app"

mkdir -p "$OUT/logs" "$OUT/screenshots"
pkill -f SilkTriangleSample.app/Contents/MacOS || true
sleep 1
export ORBIT3D_LOG_DIAGNOSTICS=1
# The app runs inside the App Sandbox, so the capture and command paths must live in its container.
CAPTURE_DIR="$HOME/Library/Containers/com.remotecodehub.silktrianglesample/Data/tmp/orbit3d-capture"
mkdir -p "$CAPTURE_DIR"
rm -f "$CAPTURE_DIR/capture.request" "$CAPTURE_DIR/commands.txt"
export ORBIT3D_SILK_CAPTURE_DIR="$CAPTURE_DIR"
export ORBIT3D_COMMAND_FILE="$CAPTURE_DIR/commands.txt"
"$APP/Contents/MacOS/SilkTriangleSample" > "$OUT/logs/application-$RUN.log" 2>&1 &
echo "pid=$! log=$OUT/logs/application-$RUN.log"
sleep "$SECONDS_TO_RUN"
