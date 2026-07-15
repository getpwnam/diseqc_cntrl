#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./_common.sh
source "$SCRIPT_DIR/_common.sh"

PHASE="${PHASE:-phase4_usb_cdc}"
USB_CDC_SMOKE_CMD="${USB_CDC_SMOKE_CMD:-}"
USB_CDC_PORT="${USB_CDC_PORT:-/dev/ttyACM0}"
SERIAL_PORT="${SERIAL_PORT:-/dev/ttyUSB0}"
BAUD="${BAUD:-115200}"

LOG_DIR="$(phase_log_dir "$PHASE")"

if [[ -z "$USB_CDC_SMOKE_CMD" ]]; then
  echo "USB_CDC_SMOKE_CMD is not set." >&2
  echo "Set it to the USB-CDC console smoke command for this phase." >&2
  echo "Example: USB_CDC_SMOKE_CMD='firmware/cubley-f407/toolchain/run-usb-cdc-smoke.sh'" >&2
  exit 2
fi

require_cmd nanoff

echo "Running Phase 4 USB-CDC isolation gate..."
print_paths

echo "[1/3] pre-regression wire gate on USART3"
PHASE="${PHASE}_pre" SERIAL_PORT="$SERIAL_PORT" BAUD="$BAUD" "$SCRIPT_DIR/gate-wire-protocol.sh"

echo "[2/3] USB-CDC user console smoke"
bash -lc "$USB_CDC_SMOKE_CMD" | tee "$LOG_DIR/usb_cdc_smoke.log"

echo "[3/3] verify nanoff does not enumerate on USB-CDC"
set +e
nanoff --nanodevice --serialport "$USB_CDC_PORT" --baud "$BAUD" --listdevices >"$LOG_DIR/nanoff_usbcdc_listdevices.log" 2>&1
RC=$?
set -e

if [[ $RC -eq 0 ]] && ! grep -qi "No devices found" "$LOG_DIR/nanoff_usbcdc_listdevices.log"; then
  echo "FAIL: USB-CDC appears to carry wire protocol unexpectedly." >&2
  exit 1
fi

echo "PASS: USB-CDC isolation gate"
