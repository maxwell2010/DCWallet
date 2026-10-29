#!/usr/bin/env bash
set -euo pipefail
binary=$1; version=$2; rid=$3; out=$4
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.]+)?$ ]] || exit 1
[[ "$rid" == osx-arm64 || "$rid" == osx-x64 ]] || exit 1
mkdir -p "$out"; out="$(cd "$out" && pwd)"; stage=$(mktemp -d)
trap 'rm -rf -- "$stage"' EXIT
app="$stage/DC WALLET.app"; mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
install -m 755 "$binary" "$app/Contents/MacOS/DCWallet"
root="$(cd "$(dirname "$0")/.." && pwd)"
python3 - "$root/src/DCWallet/Assets/icon.png" "$app/Contents/Resources/icon.icns" <<'PY'
import pathlib,struct,sys
png=pathlib.Path(sys.argv[1]).read_bytes()
chunk=b'ic07'+struct.pack('>I',len(png)+8)+png
pathlib.Path(sys.argv[2]).write_bytes(b'icns'+struct.pack('>I',len(chunk)+8)+chunk)
PY
cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict><key>CFBundleExecutable</key><string>DCWallet</string><key>CFBundleIconFile</key><string>icon.icns</string><key>CFBundleIdentifier</key><string>ru.mintcandy.dcwallet</string><key>CFBundleName</key><string>DC WALLET</string><key>CFBundlePackageType</key><string>APPL</string><key>CFBundleShortVersionString</key><string>${version%%-*}</string><key>NSHighResolutionCapable</key><true/></dict></plist>
PLIST
# No signing identity is configured. This produces an unsigned preview bundle.
ditto -c -k --sequesterRsrc --keepParent "$app" "$out/DCWallet-${version}-${rid}.app.zip"
