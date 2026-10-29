#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
if [ "$(uname -s)" != "Darwin" ]; then
  echo "请在 macOS 13 或更新版本上运行此脚本。" >&2
  exit 1
fi
if ! xcrun --find swift >/dev/null 2>&1; then
  echo "需要 Xcode 15 或更新版本，或含 Swift 5.9+ 的 Command Line Tools。" >&2
  echo "可先运行 xcode-select --install 安装 Apple 命令行工具。" >&2
  exit 1
fi
export MACOSX_DEPLOYMENT_TARGET=13.0
if [ "${1:-}" != "" ] && [ "${1:-}" != "--universal" ]; then
  echo "用法：bash build-macos.sh [--universal]" >&2
  exit 1
fi
echo "[1/4] 运行转换核心测试"
swift test
echo "[2/4] 编译原生应用"
if [ "${1:-}" = "--universal" ]; then
  swift build -c release --arch arm64 --arch x86_64
  BIN_DIR="$(swift build -c release --arch arm64 --arch x86_64 --show-bin-path)"
else
  swift build -c release
  BIN_DIR="$(swift build -c release --show-bin-path)"
fi
echo "[3/4] 生成图标和应用包"
APP="$PWD/dist/Vocal Grid.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$PWD/.build/VocalGrid.iconset"
cp "$BIN_DIR/VocalGrid" "$APP/Contents/MacOS/VocalGrid"
cp Info.plist "$APP/Contents/Info.plist"
ICONSET="$PWD/.build/VocalGrid.iconset"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" Assets/VocalGrid.png --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z "$double" "$double" Assets/VocalGrid.png --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/VocalGrid.icns"
chmod +x "$APP/Contents/MacOS/VocalGrid"
echo "[4/4] 本地临时签名并核验应用结构"
codesign --force --sign - "$APP"
codesign --verify --strict "$APP"
plutil -lint "$APP/Contents/Info.plist"
file "$APP/Contents/MacOS/VocalGrid"
echo ""
echo "完成：$APP"
echo "可以将 Vocal Grid.app 移动到需要的文件夹，再双击运行。"
echo "此签名用于本地运行，不是 Apple Developer ID 签名或公证。"
