#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

MODE="${1:-}"
SERIAL_PORT="${SERIAL_PORT:-/dev/ttyUSB0}"
BAUD="${BAUD:-115200}"
DEPLOY_ADDRESS="${DEPLOY_ADDRESS:-0x080C0000}"
IMAGE_PATH="${IMAGE_PATH:-$ROOT_DIR/tests/W5500Bringup/bin/Release/W5500Bringup.bin}"
ELF_PATH="${ELF_PATH:-$ROOT_DIR/build/nanoCLR.elf}"

usage() {
  cat <<'EOF'
Usage:
  ./toolchain/w5500-led-observe.sh start
  ./toolchain/w5500-led-observe.sh sample

Environment overrides:
  SERIAL_PORT   (default: /dev/ttyUSB0)
  BAUD          (default: 115200)
  DEPLOY_ADDRESS(default: 0x080C0000)
  IMAGE_PATH    (default: tests/W5500Bringup/bin/Release/W5500Bringup.bin)
  ELF_PATH      (default: build/nanoCLR.elf)

Modes:
  start   Deploy and reset W5500Bringup image, then exit without any SWD probing.
          Use this before visually reading LED result codes.

  sample  Read and decode mailbox once over SWD (after LED observation window).
EOF
}

require_cmd() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "Missing required command: $cmd" >&2
    exit 1
  fi
}

do_start() {
  require_cmd st-info
  require_cmd st-flash
  require_cmd nanoff

  if [[ ! -f "$IMAGE_PATH" ]]; then
    echo "Managed image not found: $IMAGE_PATH" >&2
    echo "Build it first with ./toolchain/compile-w5500-test.sh" >&2
    exit 1
  fi

  echo "[w5500-led-observe] clearing stale OpenOCD sessions"
  pkill -f "openocd -f interface/stlink.cfg -f target/stm32f4x.cfg" >/dev/null 2>&1 || true

  echo "[w5500-led-observe] st-info probe"
  st-info --probe >/dev/null

  echo "[w5500-led-observe] reset target"
  st-flash reset >/dev/null

  echo "[w5500-led-observe] deploy W5500Bringup over UART"
  nanoff --nanodevice --serialport "$SERIAL_PORT" --baud "$BAUD" --deploy --image "$IMAGE_PATH" --address "$DEPLOY_ADDRESS" --reset

  cat <<EOF

[w5500-led-observe] START COMPLETE
- Board is now running without SWD breakpoints.
- Observe LED result code now.
- When done, capture mailbox with:
  ./toolchain/w5500-led-observe.sh sample
EOF
}

do_sample() {
  if [[ ! -f "$ELF_PATH" ]]; then
    echo "ELF not found: $ELF_PATH" >&2
    exit 1
  fi

  echo "[w5500-led-observe] mailbox sample"
  "$ROOT_DIR/tests/swd_read_bringup_status.sh" "$ELF_PATH"
}

case "$MODE" in
  start)
    do_start
    ;;
  sample)
    do_sample
    ;;
  *)
    usage
    exit 2
    ;;
esac
