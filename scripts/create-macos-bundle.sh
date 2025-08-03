#!/bin/bash
# macOS .app バンドルを作成するスクリプト

APP_NAME="Service Bus Explorer"
BUNDLE_ID="com.servicebus.explorer"
VERSION="1.0.0"
EXECUTABLE="ServiceBusExplorer.UI"

# 引数からディレクトリとアーキテクチャを取得
SOURCE_DIR=$1
ARCH=$2  # x64 or arm64

if [ -z "$SOURCE_DIR" ] || [ -z "$ARCH" ]; then
    echo "Usage: $0 <source_directory> <architecture>"
    exit 1
fi

# .app バンドル構造を作成
APP_DIR="${SOURCE_DIR}/${APP_NAME}.app"
mkdir -p "${APP_DIR}/Contents/MacOS"
mkdir -p "${APP_DIR}/Contents/Resources"

# 実行ファイルとライブラリをコピー
cp -R "${SOURCE_DIR}"/* "${APP_DIR}/Contents/MacOS/"

# Info.plist を作成
cat > "${APP_DIR}/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>${EXECUTABLE}</string>
    <key>CFBundleIdentifier</key>
    <string>${BUNDLE_ID}</string>
    <key>CFBundleName</key>
    <string>${APP_NAME}</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2024 Service Bus Explorer Contributors</string>
    <key>LSArchitecturePriority</key>
    <array>
        <string>${ARCH}</string>
    </array>
</dict>
</plist>
EOF

# 実行権限を設定
chmod +x "${APP_DIR}/Contents/MacOS/${EXECUTABLE}"

echo "Created ${APP_NAME}.app bundle for ${ARCH}"