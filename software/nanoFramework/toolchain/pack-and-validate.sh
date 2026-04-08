#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
OUT_DIR_DEFAULT="$ROOT_DIR/tests/BlinkBringup/bin/Release"
DEPLOY_REGION_MAX_BYTES=$((0x40000))

OUT_DIR="$OUT_DIR_DEFAULT"
OUT_BASE=""
REQUIRED_MARKER="NFMRK1"

marker_name_from_hex() {
  local hex="$1"
  case "$hex" in
    4E464D524B31) echo "NFMRK1" ;;
    4E464D524B32) echo "NFMRK2" ;;
    *) echo "UNKNOWN" ;;
  esac
}

usage() {
  cat <<'EOF'
Usage:
  ./toolchain/pack-and-validate.sh [--out-dir <dir>] [--out-base <name>] [--required-marker NFMRK1|NFMRK2|ANY] <assembly1.pe> [assembly2.pe ...]

Description:
  Validates each input file has an NFMRK marker, concatenates files in order,
  enforces marker policy,
  verifies output size <= deployment region limit (0x40000), and writes:
    - <out-base>-<timestamp>.deploy.bin
    - latest.deploy.bin (symlink)

Examples:
  ./toolchain/pack-and-validate.sh \
    tests/BlinkBringup/bin/Release/BlinkBringup.pe \
    tests/BlinkBringup/bin/Release/System.Device.Gpio.pe \
    tests/BlinkBringup/bin/Release/nanoFramework.Runtime.Events.pe \
    tests/BlinkBringup/bin/Release/System.Threading.pe

  ./toolchain/pack-and-validate.sh --out-base BlinkBringup_complete \
    tests/BlinkBringup/bin/Release/BlinkBringup.pe \
    tests/BlinkBringup/bin/Release/System.Device.Gpio.pe \
    tests/BlinkBringup/bin/Release/System.Threading.pe

  ./toolchain/pack-and-validate.sh --required-marker NFMRK2 \
    tests/BlinkBringup/bin/Release/BlinkBringup.pe \
    tests/BlinkBringup/bin/Release/System.Device.Gpio.pe
EOF
}

fail() {
  echo "ERROR: $*" >&2
  exit 2
}

ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --out-dir)
      OUT_DIR="${2:-}"
      shift 2
      ;;
    --out-base)
      OUT_BASE="${2:-}"
      shift 2
      ;;
    --required-marker)
      REQUIRED_MARKER="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      while [[ $# -gt 0 ]]; do
        ARGS+=("$1")
        shift
      done
      ;;
    *)
      ARGS+=("$1")
      shift
      ;;
  esac
done

if [[ ${#ARGS[@]} -lt 1 ]]; then
  usage
  fail "at least one .pe/.bin input is required"
fi

case "$REQUIRED_MARKER" in
  NFMRK1|NFMRK2|ANY)
    ;;
  *)
    fail "--required-marker must be NFMRK1, NFMRK2, or ANY"
    ;;
esac

mkdir -p "$OUT_DIR"

for f in "${ARGS[@]}"; do
  if [[ ! -f "$f" ]]; then
    fail "input not found: $f"
  fi
  marker_hex="$(hexdump -n 6 -e '6/1 "%02X"' "$f" 2>/dev/null || true)"
  if [[ "$marker_hex" != "4E464D524B31" && "$marker_hex" != "4E464D524B32" ]]; then
    fail "input is not an NFMRK assembly image: $f (header=$marker_hex)"
  fi

  marker_name="$(marker_name_from_hex "$marker_hex")"
  if [[ "$REQUIRED_MARKER" != "ANY" && "$marker_name" != "$REQUIRED_MARKER" ]]; then
    fail "marker policy violation: $f has $marker_name, required $REQUIRED_MARKER"
  fi
done

if [[ -z "$OUT_BASE" ]]; then
  first_base="$(basename "${ARGS[0]}")"
  OUT_BASE="${first_base%.*}_complete"
fi

TIMESTAMP="$(date -u +"%Y%m%dT%H%M%SZ")"
OUT_FILE="$OUT_DIR/${OUT_BASE}-${TIMESTAMP}.deploy.bin"
LATEST_LINK="$OUT_DIR/latest.deploy.bin"

cat "${ARGS[@]}" > "$OUT_FILE"

OUT_SIZE="$(stat -c '%s' "$OUT_FILE")"
if (( OUT_SIZE > DEPLOY_REGION_MAX_BYTES )); then
  rm -f "$OUT_FILE"
  fail "output too large: ${OUT_SIZE} bytes > ${DEPLOY_REGION_MAX_BYTES} bytes (deployment region limit)"
fi

python3 "$SCRIPT_DIR/inspect_deploy_bundle.py" "$OUT_FILE"

ln -sfn "$(basename "$OUT_FILE")" "$LATEST_LINK"

echo "PACK_OK: $OUT_FILE"
echo "SIZE_OK: $OUT_SIZE bytes"
echo "MARKER_POLICY: $REQUIRED_MARKER"
echo "LATEST: $LATEST_LINK -> $(readlink "$LATEST_LINK")"
