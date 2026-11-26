#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
if ! xcrun --find swiftc >/dev/null 2>&1; then
  echo 'Install Xcode Command Line Tools with xcode-select --install, then rerun bash build.sh.' >&2
  exit 1
fi
app="$PWD/build/WinKnotch.app"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT
mkdir -p "$app/Contents/MacOS"
sdk="$(xcrun --sdk macosx --show-sdk-path)"
for arch in x86_64 arm64; do
  xcrun swiftc -swift-version 5 -O -sdk "$sdk" -target "$arch-apple-macos12.0" \
    Sources/*.swift -o "$scratch/$arch"
done
cp Info.plist "$app/Contents/Info.plist"
plutil -lint "$app/Contents/Info.plist"
xcrun lipo -create "$scratch/x86_64" "$scratch/arm64" -output "$app/Contents/MacOS/WinKnotch"
codesign --force --sign - "$app"
codesign --verify --strict "$app"
xcrun lipo -archs "$app/Contents/MacOS/WinKnotch"
ditto -c -k --sequesterRsrc --keepParent "$app" "$PWD/build/WinKnotch-macOS.zip"
echo "Built $app"
