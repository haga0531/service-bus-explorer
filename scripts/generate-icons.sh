#!/bin/bash
# macOS用のアイコンを生成するスクリプト
# 注意: ImageMagickが必要です (brew install imagemagick)

if ! command -v convert &> /dev/null; then
    echo "Error: ImageMagick is not installed. Please install it first:"
    echo "  brew install imagemagick"
    exit 1
fi

# SVGファイルのパス
SVG_FILE="assets/icon-template.svg"
OUTPUT_DIR="assets/icons"

# 出力ディレクトリを作成
mkdir -p "$OUTPUT_DIR"

# 各サイズのPNGを生成
sizes=(16 32 64 128 256 512 1024)
for size in "${sizes[@]}"; do
    echo "Generating ${size}x${size} icon..."
    convert -background none -resize ${size}x${size} "$SVG_FILE" "$OUTPUT_DIR/icon_${size}x${size}.png"
done

# macOS用の.icnsファイルを生成
echo "Creating .icns file for macOS..."
iconutil_dir="$OUTPUT_DIR/AppIcon.iconset"
mkdir -p "$iconutil_dir"

# iconutilに必要なファイル名でコピー
cp "$OUTPUT_DIR/icon_16x16.png" "$iconutil_dir/icon_16x16.png"
cp "$OUTPUT_DIR/icon_32x32.png" "$iconutil_dir/icon_16x16@2x.png"
cp "$OUTPUT_DIR/icon_32x32.png" "$iconutil_dir/icon_32x32.png"
cp "$OUTPUT_DIR/icon_64x64.png" "$iconutil_dir/icon_32x32@2x.png"
cp "$OUTPUT_DIR/icon_128x128.png" "$iconutil_dir/icon_128x128.png"
cp "$OUTPUT_DIR/icon_256x256.png" "$iconutil_dir/icon_128x128@2x.png"
cp "$OUTPUT_DIR/icon_256x256.png" "$iconutil_dir/icon_256x256.png"
cp "$OUTPUT_DIR/icon_512x512.png" "$iconutil_dir/icon_256x256@2x.png"
cp "$OUTPUT_DIR/icon_512x512.png" "$iconutil_dir/icon_512x512.png"
cp "$OUTPUT_DIR/icon_1024x1024.png" "$iconutil_dir/icon_512x512@2x.png"

# .icnsファイルを生成
iconutil -c icns "$iconutil_dir" -o "$OUTPUT_DIR/AppIcon.icns"

# Windows用の.icoファイルを生成
echo "Creating .ico file for Windows..."
convert "$OUTPUT_DIR/icon_16x16.png" "$OUTPUT_DIR/icon_32x32.png" "$OUTPUT_DIR/icon_64x64.png" "$OUTPUT_DIR/icon_128x128.png" "$OUTPUT_DIR/icon_256x256.png" "$OUTPUT_DIR/AppIcon.ico"

echo "Icon generation complete!"
echo "  macOS: $OUTPUT_DIR/AppIcon.icns"
echo "  Windows: $OUTPUT_DIR/AppIcon.ico"