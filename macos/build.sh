#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
if ! xcrun --find swiftc >/dev/null 2>&1; then
  echo 'Install Xcode Command Line Tools with xcode-select --install, then rerun bash build.sh.' >&2
  exit 1
fi
name="WinKnotch"
archive="WinKnotch-macOS.zip"
compiler_flags=(-D STANDARD_BUILD)
case "${1:-}" in
  --lite) name="WinKnotch Lite"; archive="WinKnotch-Lite-macOS.zip"; compiler_flags=(-D LITE_BUILD) ;;
  "") ;;
  *) echo 'Usage: bash build.sh [--lite]' >&2; exit 1 ;;
esac
app="$PWD/build/$name.app"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT
mkdir -p "$app/Contents/MacOS"
sdk="$(xcrun --sdk macosx --show-sdk-path)"
for arch in x86_64 arm64; do
  xcrun swiftc -swift-version 5 -O -sdk "$sdk" -target "$arch-apple-macos12.0" \
    "${compiler_flags[@]}" Sources/*.swift -o "$scratch/$arch"
done
cp Info.plist "$app/Contents/Info.plist"
if [[ "$name" == "WinKnotch Lite" ]]; then
  /usr/libexec/PlistBuddy -c 'Set :CFBundleName WinKnotch Lite' "$app/Contents/Info.plist"
  /usr/libexec/PlistBuddy -c 'Set :CFBundleDisplayName WinKnotch Lite' "$app/Contents/Info.plist"
  /usr/libexec/PlistBuddy -c 'Set :CFBundleIdentifier io.github.ego-tistic.winknotch.lite' "$app/Contents/Info.plist"
fi
plutil -lint "$app/Contents/Info.plist"
xcrun lipo -create "$scratch/x86_64" "$scratch/arm64" -output "$app/Contents/MacOS/WinKnotch"
codesign --force --sign - "$app"
codesign --verify --strict "$app"
xcrun lipo -archs "$app/Contents/MacOS/WinKnotch"
ditto -c -k --sequesterRsrc --keepParent "$app" "$PWD/build/$archive"
echo "Built $app"
