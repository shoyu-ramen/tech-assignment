#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="${1:-$PROJECT_DIR/Builds/iOS}"
UNITY="/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity"
LOG_FILE="$PROJECT_DIR/Logs/ios-build.log"

if [ ! -f "$UNITY" ]; then
    echo "Unity not found at $UNITY"
    echo "Update the UNITY path in this script to match your installation."
    exit 1
fi

echo "Building iOS Xcode project to: $BUILD_DIR"
echo "Log file: $LOG_FILE"

export IOS_BUILD_PATH="$BUILD_DIR"

"$UNITY" \
    -quit \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT_DIR" \
    -executeMethod iOSBuilder.Build \
    -logFile "$LOG_FILE"

EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo "Unity iOS export succeeded! Xcode project: $BUILD_DIR"
    echo ""
    echo "Next steps:"
    echo "  1. Open in Xcode:  open '$BUILD_DIR/Unity-iPhone.xcodeproj'"
    echo "  2. Set your signing team in Xcode"
    echo "  3. Build & run on device or simulator from Xcode"
    echo ""
    echo "Or build from command line:"
    echo "  xcodebuild -project '$BUILD_DIR/Unity-iPhone.xcodeproj' \\"
    echo "    -scheme Unity-iPhone -destination 'platform=iOS Simulator,name=iPhone 16' \\"
    echo "    build"
else
    echo "Build failed (exit code $EXIT_CODE). Check log: $LOG_FILE"
    tail -30 "$LOG_FILE" 2>/dev/null
fi

exit $EXIT_CODE
