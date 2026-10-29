#!/usr/bin/env bash
set -euo pipefail
binary=$1; version=$2; arch=${3:-amd64}; out=$4
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.]+)?$ ]] || exit 1
[[ "$arch" == amd64 || "$arch" == arm64 ]] || exit 1
root="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$out"; out="$(cd "$out" && pwd)"
stage=$(mktemp -d)
trap 'rm -rf -- "$stage"' EXIT
mkdir -p "$stage/opt/dc-wallet" "$stage/usr/bin" "$stage/usr/share/applications" "$stage/usr/share/icons/hicolor/128x128/apps" "$stage/DEBIAN"
install -m 755 "$binary" "$stage/opt/dc-wallet/DCWallet"
ln -s /opt/dc-wallet/DCWallet "$stage/usr/bin/dc-wallet"
cp "$root/src/DCWallet/Assets/icon.png" "$stage/usr/share/icons/hicolor/128x128/apps/dc-wallet.png"
cat > "$stage/usr/share/applications/dc-wallet.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=DC WALLET
Comment=Decimal network wallet
Exec=dc-wallet
Icon=dc-wallet
Terminal=false
Categories=Office;Finance;
DESKTOP
cat > "$stage/DEBIAN/control" <<CONTROL
Package: dc-wallet
Version: ${version/-/\~}
Section: utils
Priority: optional
Architecture: $arch
Maintainer: DC WALLET contributors
Depends: libc6, libgcc-s1, libstdc++6, libicu-dev, libssl3, libx11-6, libice6, libsm6, libfontconfig1
Description: DC WALLET alpha for Decimal EVM
 Local encrypted wallet using the Decimal C# SDK.
CONTROL
dpkg-deb --root-owner-group --build "$stage" "$out/dc-wallet_${version}_${arch}.deb"
tar -C "$stage/opt/dc-wallet" -czf "$out/DCWallet-${version}-linux-${arch}.tar.gz" DCWallet
if [[ "$arch" == amd64 ]]; then
  appdir="$stage/AppDir"
  mkdir -p "$appdir/usr/bin"
  install -m 755 "$binary" "$appdir/usr/bin/DCWallet"
  cp "$root/src/DCWallet/Assets/icon.png" "$appdir/dc-wallet.png"
  sed 's/Exec=dc-wallet/Exec=DCWallet/' "$stage/usr/share/applications/dc-wallet.desktop" > "$appdir/dc-wallet.desktop"
  cat > "$appdir/AppRun" <<'APPRUN'
#!/bin/sh
exec "$(dirname "$(readlink -f "$0")")/usr/bin/DCWallet" "$@"
APPRUN
  chmod 755 "$appdir/AppRun"
  # Same fixed upstream tool version as the reviewed MinterWallet release recipe.
  curl --fail --location --proto '=https' -o "$stage/appimagetool" https://github.com/AppImage/appimagetool/releases/download/1.9.0/appimagetool-x86_64.AppImage
  chmod 755 "$stage/appimagetool"
  ARCH=x86_64 "$stage/appimagetool" --appimage-extract-and-run --no-appstream "$appdir" "$out/DCWallet-${version}-x86_64.AppImage"
  test -s "$out/DCWallet-${version}-x86_64.AppImage"
fi
