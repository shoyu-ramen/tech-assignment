#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="${1:-$PROJECT_DIR/Builds/Desktop/HijackPoker.app}"
UNITY="/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity"
LOG_FILE="$PROJECT_DIR/Logs/desktop-build.log"

if [ ! -f "$UNITY" ]; then
    echo "Unity not found at $UNITY"
    echo "Update the UNITY path in this script to match your installation."
    exit 1
fi

echo "Building Desktop (macOS) to: $BUILD_DIR"
echo "Log file: $LOG_FILE"

export DESKTOP_BUILD_PATH="$BUILD_DIR"

"$UNITY" \
    -quit \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT_DIR" \
    -executeMethod DesktopBuilder.Build \
    -logFile "$LOG_FILE"

EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo "Build succeeded! Output: $BUILD_DIR"
    echo "To run: open '$BUILD_DIR'"
else
    echo "Build failed (exit code $EXIT_CODE). Check log: $LOG_FILE"
    tail -30 "$LOG_FILE" 2>/dev/null
fi

exit $EXIT_CODE
