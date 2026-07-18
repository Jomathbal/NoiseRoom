#!/usr/bin/env bash
# Cuts both eye regions out of every face_*.jpg in the "faces" subfolder.
# Right eye: top-left at (x=556, y=432); left eye mirrored at
# (x=294, y=432); both 174px wide, 104px high (image origin (0,0) =
# top-left, faces are 1024x1024). Results are saved as eye_XX.jpg into
# the "right_eyes" and "left_eyes" subfolders.
# Uses macOS's built-in `sips`, so no extra tools are required.

set -euo pipefail

Y=432
WIDTH=174
HEIGHT=104
RIGHT_X=556
LEFT_X=294   # 1024 - RIGHT_X - WIDTH (mirrored horizontally)

DIR="$(cd "$(dirname "$0")" && pwd)"
FACES_DIR="$DIR/faces"
RIGHT_EYES_DIR="$DIR/right_eyes"
LEFT_EYES_DIR="$DIR/left_eyes"

shopt -s nullglob
FACES=("$FACES_DIR"/face_*.jpg)
if [ ${#FACES[@]} -eq 0 ]; then
    echo "No face_*.jpg images found in $FACES_DIR" >&2
    exit 1
fi

mkdir -p "$RIGHT_EYES_DIR" "$LEFT_EYES_DIR"

for SRC in "${FACES[@]}"; do
    NAME="$(basename "$SRC" .jpg)"
    RIGHT_OUT="$RIGHT_EYES_DIR/eye_${NAME#face_}.jpg"
    LEFT_OUT="$LEFT_EYES_DIR/eye_${NAME#face_}.jpg"
    echo "Cropping $SRC -> $RIGHT_OUT + $LEFT_OUT"
    # sips crops relative to the top-left corner; --cropOffset takes Y then X.
    sips -c "$HEIGHT" "$WIDTH" --cropOffset "$Y" "$RIGHT_X" "$SRC" --out "$RIGHT_OUT" >/dev/null
    sips -c "$HEIGHT" "$WIDTH" --cropOffset "$Y" "$LEFT_X" "$SRC" --out "$LEFT_OUT" >/dev/null
done

echo "Done: ${#FACES[@]} faces cropped into $RIGHT_EYES_DIR and $LEFT_EYES_DIR"
