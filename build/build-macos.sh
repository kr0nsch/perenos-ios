#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
VERSION="${1:-1.7.0}"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Version must look like 1.7.0" >&2
  exit 2
fi
if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "macOS packages must be built on macOS (a GitHub macOS runner also works)." >&2
  exit 2
fi
for command in dotnet pwsh go tar plutil codesign hdiutil ditto sips iconutil shasum; do
  command -v "$command" >/dev/null || { echo "Missing required command: $command" >&2; exit 2; }
done
RIDS=("${@:2}")
if [[ ${#RIDS[@]} -eq 0 ]]; then
  case "$(uname -m)" in
    arm64) RIDS=(osx-arm64) ;;
    x86_64) RIDS=(osx-x64) ;;
    *) echo "Unsupported Mac architecture: $(uname -m)" >&2; exit 2 ;;
  esac
fi
IDENTITY="${MACOS_SIGN_IDENTITY:--}"
NOTARY_PROFILE="${MACOS_NOTARY_PROFILE:-}"
RELEASE="$ROOT/artifacts/release"
mkdir -p "$RELEASE"

for RID in "${RIDS[@]}"; do
  if [[ "$RID" != "osx-arm64" && "$RID" != "osx-x64" ]]; then
    echo "Unsupported runtime: $RID" >&2
    exit 2
  fi
  ARCH="${RID#osx-}"
  WORK="$ROOT/artifacts/macos/$RID"
  PUBLISH="$ROOT/artifacts/publish/$RID"
  case "$WORK" in "$ROOT/artifacts/"*) ;; *) echo "Unsafe work directory" >&2; exit 2;; esac
  rm -rf "$WORK" "$PUBLISH"
  mkdir -p "$WORK" "$PUBLISH"

  pwsh -NoProfile -File "$ROOT/build/Get-Ipatool.ps1" -RuntimeIdentifier "$RID"
  dotnet publish "$ROOT/src/Perenos/Perenos.csproj" -c Release -r "$RID" \
    -p:Version="$VERSION" -p:ContinuousIntegrationBuild=true -p:UseAppHost=true -p:PublishSingleFile=true \
    --self-contained true -o "$PUBLISH"

  APP="$WORK/Perenos.app"
  mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
  cp -R "$PUBLISH"/ "$APP/Contents/MacOS/"
  cp "$ROOT/build/macos/Info.plist" "$APP/Contents/Info.plist"
  plutil -replace CFBundleShortVersionString -string "$VERSION" "$APP/Contents/Info.plist"
  plutil -replace CFBundleVersion -string "$VERSION" "$APP/Contents/Info.plist"
  test -x "$APP/Contents/MacOS/Perenos"
  test -x "$APP/Contents/MacOS/tools/ipatool"

  ICONSET="$WORK/Perenos.iconset"
  mkdir -p "$ICONSET"
  for spec in "16:icon_16x16.png" "32:icon_16x16@2x.png" "32:icon_32x32.png" "64:icon_32x32@2x.png" \
    "128:icon_128x128.png" "256:icon_128x128@2x.png" "256:icon_256x256.png" "512:icon_256x256@2x.png" \
    "512:icon_512x512.png" "1024:icon_512x512@2x.png"; do
    sips -z "${spec%%:*}" "${spec%%:*}" "$ROOT/assets/logo-1024.png" --out "$ICONSET/${spec#*:}" >/dev/null
  done
  iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/Perenos.icns"

  SIGN_ARGS=(--force --sign "$IDENTITY")
  if [[ "$IDENTITY" != "-" ]]; then
    SIGN_ARGS+=(--timestamp --options runtime)
  fi
  while IFS= read -r -d '' binary; do
    if [[ "$(basename "$binary")" == "Perenos" && "$IDENTITY" != "-" ]]; then
      codesign "${SIGN_ARGS[@]}" --entitlements "$ROOT/build/macos/Entitlements.plist" "$binary"
    else
      codesign "${SIGN_ARGS[@]}" "$binary"
    fi
  done < <(find "$APP/Contents" -type f \( -name '*.dylib' -o -name 'ipatool' -o -name 'Perenos' \) -print0)
  if [[ "$IDENTITY" != "-" ]]; then
    codesign "${SIGN_ARGS[@]}" --entitlements "$ROOT/build/macos/Entitlements.plist" "$APP"
  else
    codesign "${SIGN_ARGS[@]}" "$APP"
  fi
  codesign --verify --deep --strict --verbose=2 "$APP"
  plutil -lint "$APP/Contents/Info.plist"

  if [[ -n "$NOTARY_PROFILE" ]]; then
    if [[ "$IDENTITY" == "-" ]]; then
      echo "Notarization requires MACOS_SIGN_IDENTITY with a Developer ID Application certificate." >&2
      exit 2
    fi
    NOTARY_ZIP="$WORK/Perenos-notary.zip"
    ditto -c -k --sequesterRsrc --keepParent "$APP" "$NOTARY_ZIP"
    xcrun notarytool submit "$NOTARY_ZIP" --keychain-profile "$NOTARY_PROFILE" --wait
    xcrun stapler staple "$APP"
    xcrun stapler validate "$APP"
  fi

  STAGING="$WORK/dmg"
  mkdir -p "$STAGING"
  cp -R "$APP" "$STAGING/Perenos.app"
  ln -s /Applications "$STAGING/Applications"
  DMG="$RELEASE/Perenos-$VERSION-macos-$ARCH.dmg"
  rm -f "$DMG"
  hdiutil create -volname "Перенос" -srcfolder "$STAGING" -ov -format UDZO "$DMG"
  if [[ -n "$NOTARY_PROFILE" ]]; then
    xcrun notarytool submit "$DMG" --keychain-profile "$NOTARY_PROFILE" --wait
    xcrun stapler staple "$DMG"
    spctl -a -vv --type open --context context:primary-signature "$DMG"
  else
    echo "Built an ad-hoc development package. Use a Developer ID and notarization for public distribution."
  fi
  (cd "$RELEASE" && shasum -a 256 "$(basename "$DMG")") >> "$WORK/checksum.txt"
  cat "$WORK/checksum.txt" >> "$RELEASE/SHA256SUMS.txt"
  echo "Created $DMG"
done
