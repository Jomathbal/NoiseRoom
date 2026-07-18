#!/usr/bin/env bash
# Downloads AI-generated face images from https://thispersondoesnotexist.com
# Usage: ./download_faces.sh [count]   (default: 10)
# Images are saved to the "faces" subfolder as face_01.jpg, face_02.jpg, ...

set -euo pipefail

COUNT="${1:-20}"
DIR="$(cd "$(dirname "$0")" && pwd)"
OUT_DIR="$DIR/faces"
URL="https://thispersondoesnotexist.com/random-person.jpeg"

mkdir -p "$OUT_DIR"

for i in $(seq 1 "$COUNT"); do
    FILE="$OUT_DIR/$(printf 'face_%02d.jpg' "$i")"
    echo "Downloading $FILE ..."
    curl -fsSL -A "Mozilla/5.0" -o "$FILE" "$URL"
    # The site serves a new random face per request; wait briefly
    # so consecutive requests don't return the same image.
    sleep 1.0
done

echo "Done: $COUNT images saved to $OUT_DIR"
