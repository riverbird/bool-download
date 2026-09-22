#!/usr/bin/env bash
# BoolDownload macOS arm64 DMG 打包脚本
# 将 osx-arm64 的 publish 散文件封装为带图标、已签名的 .app，并打包为 DMG。
#
# 用法: ./build_arm64_dmg_desktop.sh
# 可选环境变量:
#   VERSION  应用版本号 (默认 1.3.0)
#   DMG_OUT  DMG 输出路径 (默认 项目根目录/BoolDownload-osx-arm64.dmg)
set -euo pipefail

ARCH="arm64"
VERSION="${VERSION:-1.3.0}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ_ROOT="$(dirname "$SCRIPT_DIR")"
PUB="$PROJ_ROOT/BoolDownload.Desktop/bin/Release/net10.0/osx-$ARCH/publish"
ICON_PNG="$PROJ_ROOT/BoolDownload/Assets/Icon.png"
ICNS="$PROJ_ROOT/BoolDownload/Assets/AppIcon.icns"
OUT="${DMG_OUT:-$PROJ_ROOT/BoolDownload-osx-$ARCH.dmg}"

if [ ! -d "$PUB" ]; then
  echo "错误: 未找到 publish 目录，请先构建: $PUB" >&2
  exit 1
fi

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

# ---------- 1. 应用图标: 缺少 icns 时由 Icon.png 生成 ----------
if [ ! -f "$ICNS" ]; then
  command -v swiftc >/dev/null 2>&1 || { echo "错误: 缺少 swiftc，无法生成图标" >&2; exit 1; }
  [ -f "$ICON_PNG" ] || { echo "错误: 缺少图标源文件: $ICON_PNG" >&2; exit 1; }
  cat > "$STAGE/gen_icons.swift" <<'SWIFT'
import Cocoa

let srcPath = CommandLine.arguments[1]
let outDir = CommandLine.arguments[2]
guard let img = NSImage(contentsOfFile: srcPath) else { exit(1) }
for s in [16, 32, 64, 128, 256, 512, 1024] {
    let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: s, pixelsHigh: s,
                               bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true,
                               isPlanar: false, colorSpaceName: .deviceRGB,
                               bytesPerRow: 0, bitsPerPixel: 0)!
    rep.size = NSSize(width: s, height: s)
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    NSColor.clear.setFill()
    NSRect(x: 0, y: 0, width: s, height: s).fill(using: .copy)
    let orig = img.size
    let scale = min(CGFloat(s)/orig.width, CGFloat(s)/orig.height)
    let w = orig.width*scale, h = orig.height*scale
    img.draw(in: NSRect(x: (CGFloat(s)-w)/2, y: (CGFloat(s)-h)/2, width: w, height: h),
             from: .zero, operation: .sourceOver, fraction: 1.0)
    NSGraphicsContext.restoreGraphicsState()
    let data = rep.representation(using: .png, properties: [:])!
    try! data.write(to: URL(fileURLWithPath: "\(outDir)/icon_\(s).png"))
}
print("icons generated")
SWIFT
  swiftc -o "$STAGE/gen_icons" "$STAGE/gen_icons.swift"
  mkdir -p "$STAGE/sizes" "$STAGE/AppIcon.iconset"
  "$STAGE/gen_icons" "$ICON_PNG" "$STAGE/sizes"
  cp "$STAGE/sizes/icon_16.png"   "$STAGE/AppIcon.iconset/icon_16x16.png"
  cp "$STAGE/sizes/icon_32.png"   "$STAGE/AppIcon.iconset/icon_16x16@2x.png"
  cp "$STAGE/sizes/icon_32.png"   "$STAGE/AppIcon.iconset/icon_32x32.png"
  cp "$STAGE/sizes/icon_64.png"   "$STAGE/AppIcon.iconset/icon_32x32@2x.png"
  cp "$STAGE/sizes/icon_128.png"  "$STAGE/AppIcon.iconset/icon_128x128.png"
  cp "$STAGE/sizes/icon_256.png"  "$STAGE/AppIcon.iconset/icon_128x128@2x.png"
  cp "$STAGE/sizes/icon_256.png"  "$STAGE/AppIcon.iconset/icon_256x256.png"
  cp "$STAGE/sizes/icon_512.png"  "$STAGE/AppIcon.iconset/icon_256x256@2x.png"
  cp "$STAGE/sizes/icon_512.png"  "$STAGE/AppIcon.iconset/icon_512x512.png"
  cp "$STAGE/sizes/icon_1024.png" "$STAGE/AppIcon.iconset/icon_512x512@2x.png"
  iconutil -c icns "$STAGE/AppIcon.iconset" -o "$ICNS"
fi

# ---------- 2. 组装 .app ----------
echo "准备文件..."
APP="$STAGE/BoolDownload.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUB/." "$APP/Contents/MacOS/"

# 清除可能混入的图标生成中间产物，保证 DMG 内容干净
rm -rf "$APP/Contents/MacOS/Application.iconset" "$APP/Contents/MacOS/sizes"
rm -f  "$APP/Contents/MacOS/gen_icons" "$APP/Contents/MacOS/gen_icons.swift"

chmod +x "$APP/Contents/MacOS/BoolDownload.Desktop"
cp "$ICNS" "$APP/Contents/Resources/AppIcon.icns"

cat > "$APP/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>BoolDownload</string>
    <key>CFBundleDisplayName</key><string>BoolDownload</string>
    <key>CFBundleIdentifier</key><string>com.booldownload.desktop</string>
    <key>CFBundleExecutable</key><string>BoolDownload.Desktop</string>
    <key>CFBundleIconFile</key><string>AppIcon</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$VERSION</string>
    <key>CFBundleVersion</key><string>$VERSION</string>
    <key>LSMinimumSystemVersion</key><string>11.0</string>
    <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
EOF

codesign --force --deep -s - "$APP"
ln -s /Applications "$STAGE/Applications"

# ---------- 3. 生成 DMG ----------
echo "开始打包..."
hdiutil create -srcfolder "$STAGE" -volname "BoolDownload" -format UDZO -ov "$OUT"
echo "打包完成: $OUT"
