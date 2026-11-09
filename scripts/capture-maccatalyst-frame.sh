#!/usr/bin/env bash
# Requests a frame capture from the running Mac Catalyst sample (started by validate-maccatalyst.sh) and copies the PNG to artifacts.
# Usage: scripts/capture-maccatalyst-frame.sh <name>
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
NAME="$1"
CAPTURE_DIR="$HOME/Library/Containers/com.remotecodehub.silktrianglesample/Data/tmp/orbit3d-capture"
DEST="$ROOT/artifacts/apple/stage-09/screenshots"
mkdir -p "$DEST"
rm -f "$CAPTURE_DIR/$NAME.png"
echo "$NAME" > "$CAPTURE_DIR/capture.request"
for _ in $(seq 1 40); do
  if [ -f "$CAPTURE_DIR/$NAME.png" ]; then
    cp "$CAPTURE_DIR/$NAME.png" "$DEST/$NAME.png"
    echo "$DEST/$NAME.png"
    exit 0
  fi
  sleep 0.25
done
echo "capture timed out" >&2
exit 1
