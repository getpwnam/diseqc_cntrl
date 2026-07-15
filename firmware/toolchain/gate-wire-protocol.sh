#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./_common.sh
source "$SCRIPT_DIR/_common.sh"

SERIAL_PORT="${SERIAL_PORT:-/dev/ttyUSB0}"
BAUD="${BAUD:-115200}"
CYCLES="${CYCLES:-5}"
PHASE="${PHASE:-wire}"

BOOTER_IMAGE="${BOOTER_IMAGE:-$OUT_DIR/nanoBooter.bin}"
CLR_IMAGE="${CLR_IMAGE:-$OUT_DIR/nanoCLR.bin}"

UART_PREFLIGHT="$FW_ROOT/toolchain/uart-preflight.sh"
DET_CYCLES="$FW_ROOT/toolchain/run-deterministic-cycles.sh"

require_cmd bash
require_cmd nanoff
require_cmd st-flash

if [[ ! -x "$UART_PREFLIGHT" || ! -x "$DET_CYCLES" ]]; then
  echo "Required local toolchain scripts are missing/executable bit not set." >&2
  echo "Expected: $UART_PREFLIGHT" >&2
  echo "Expected: $DET_CYCLES" >&2
  exit 2
fi

if [[ ! -f "$BOOTER_IMAGE" || ! -f "$CLR_IMAGE" ]]; then
  echo "Missing required firmware artifacts." >&2
  echo "BOOTER_IMAGE=$BOOTER_IMAGE" >&2
  echo "CLR_IMAGE=$CLR_IMAGE" >&2
  exit 2
fi

LOG_DIR="$(phase_log_dir "$PHASE")"

echo "Running wire protocol gate..."
print_paths

echo "[1/2] uart-preflight"
"$UART_PREFLIGHT" \
  --serial "$SERIAL_PORT" \
  --baud "$BAUD" \
  --log-dir "$LOG_DIR/uart_preflight"

echo "[2/2] deterministic cycles"
"$DET_CYCLES" \
  --cycles "$CYCLES" \
  --serial "$SERIAL_PORT" \
  --baud "$BAUD" \
  --booter "$BOOTER_IMAGE" \
  --clr "$CLR_IMAGE" \
  --log-root "$LOG_DIR/deterministic_cycles"

echo "PASS: wire protocol gate"
