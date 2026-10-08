#!/bin/zsh
set -euo pipefail
script_dir=${0:A:h}
project_dir=${script_dir:h}
cd "$project_dir"
swift build -c release
bundle="$project_dir/.build/CodexMenuMeter.app"
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"
cp Resources/Info.plist "$bundle/Contents/Info.plist"
cp Resources/AppIcon-1024.png "$bundle/Contents/Resources/AppIcon-1024.png"
cp Resources/AppIcon.icns "$bundle/Contents/Resources/AppIcon.icns"
cp ".build/release/CodexMenuMeter" "$bundle/Contents/MacOS/CodexMenuMeter"
codesign --force --sign - "$bundle"
echo "$bundle"
