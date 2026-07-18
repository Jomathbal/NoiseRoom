#!/usr/bin/env bash
# Converts every eye_*.jpg in the "right_eyes" and "left_eyes" subfolders
# to black and white (grayscale) in place. Uses macOS's built-in `sips`,
# so no extra tools are required.

set -euo pipefail

GRAY_PROFILE="/System/Library/ColorSync/Profiles/Generic Gray Profile.icc"

DIR="$(cd "$(dirname "$0")" && pwd)"

shopt -s nullglob
EYES=("$DIR/right_eyes"/eye_*.jpg "$DIR/left_eyes"/eye_*.jpg)
if [ ${#EYES[@]} -eq 0 ]; then
    echo "No eye_*.jpg images found in $DIR/right_eyes or $DIR/left_eyes" >&2
    exit 1
fi

for SRC in "${EYES[@]}"; do
    echo "Converting $SRC to black and white"
    sips --matchTo "$GRAY_PROFILE" "$SRC" >/dev/null
done

echo "Done: ${#EYES[@]} images converted to black and white"
