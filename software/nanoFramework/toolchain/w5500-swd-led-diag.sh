#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
ELF_PATH="$ROOT_DIR/build/nanoCLR.elf"
IMAGE_PATH="$ROOT_DIR/tests/W5500Bringup/bin/Release/W5500Bringup.bin"
SERIAL_PORT="${SERIAL_PORT:-/dev/ttyUSB0}"
BAUDS="${BAUDS:-115200 921600}"
DEPLOY_ADDRESS="${DEPLOY_ADDRESS:-0x080C0000}"
DO_BUILD=0
SKIP_DEPLOY=0
WAIT_AFTER_DEPLOY="${WAIT_AFTER_DEPLOY:-2}"
PROBE_TIMEOUT="${PROBE_TIMEOUT:-8}"
PROBE_ATTEMPTS="${PROBE_ATTEMPTS:-2}"
OUT_FILE="$ROOT_DIR/.debug/w5500_swd_led_diag.out"

usage() {
  cat <<'EOF'
Usage:
  ./toolchain/w5500-swd-led-diag.sh [options]

Options:
  --build                 Rebuild W5500 managed image before diagnostics.
  --skip-deploy           Skip UART deploy and run probes against current image on device.
  --serial <port>         Serial port (default: /dev/ttyUSB0).
  --address <hex>         Deploy address (default: 0x080C0000).
  --bauds "b1 b2"         Space-separated baud retry list (default: "115200 921600").
  --wait <seconds>        Wait after deploy before probing (default: 2).
  --probe-timeout <sec>   Timeout per breakpoint probe attempt (default: 8).
  --probe-attempts <n>    Probe retry attempts per symbol (default: 2).
  --help                  Show this message.

Outputs:
  - Summary: .debug/w5500_swd_led_diag.out
  - Startup gate: .debug/gdb_startup_gate.out
  - Mailbox snapshot is printed to terminal and appended to summary.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --build)
      DO_BUILD=1
      shift
      ;;
    --skip-deploy)
      SKIP_DEPLOY=1
      shift
      ;;
    --serial)
      SERIAL_PORT="$2"
      shift 2
      ;;
    --address)
      DEPLOY_ADDRESS="$2"
      shift 2
      ;;
    --bauds)
      BAUDS="$2"
      shift 2
      ;;
    --wait)
      WAIT_AFTER_DEPLOY="$2"
      shift 2
      ;;
    --probe-timeout)
      PROBE_TIMEOUT="$2"
      shift 2
      ;;
    --probe-attempts)
      PROBE_ATTEMPTS="$2"
      shift 2
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 2
      ;;
  esac
done

if [[ ! -f "$ELF_PATH" ]]; then
  echo "ELF not found: $ELF_PATH" >&2
  exit 1
fi

if [[ $DO_BUILD -eq 1 ]]; then
  "$SCRIPT_DIR/compile-w5500-test.sh"
fi

if [[ ! -f "$IMAGE_PATH" && $SKIP_DEPLOY -eq 0 ]]; then
  echo "Managed image not found: $IMAGE_PATH" >&2
  exit 1
fi

for cmd in arm-none-eabi-nm c++filt rg; do
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "Required command not found: $cmd" >&2
    exit 1
  fi
done

resolve_addr() {
  local symbol="$1"
  local addr
  set +o pipefail
  addr="$(arm-none-eabi-nm -n "$ELF_PATH" | c++filt | rg -F "$symbol" | awk '{print $1}' | head -n1)"
  set -o pipefail
  echo "$addr"
}

deploy_ok=1
deploy_note="SKIPPED"
if [[ $SKIP_DEPLOY -eq 0 ]]; then
  deploy_ok=0
  for b in $BAUDS; do
    if nanoff --nanodevice --serialport "$SERIAL_PORT" --baud "$b" --deploy --image "$IMAGE_PATH" --address "$DEPLOY_ADDRESS" --reset; then
      deploy_ok=1
      deploy_note="OK@$b(addr)"
      break
    fi

    if nanoff --nanodevice --serialport "$SERIAL_PORT" --baud "$b" --deploy --image "$IMAGE_PATH" --reset; then
      deploy_ok=1
      deploy_note="OK@$b(noaddr)"
      break
    fi
  done
  if [[ $deploy_ok -eq 0 ]]; then
    deploy_note="FAIL"
  fi
fi

if [[ "$WAIT_AFTER_DEPLOY" != "0" ]]; then
  sleep "$WAIT_AFTER_DEPLOY"
fi

# Run startup gate with a dedicated OpenOCD session.
pkill -f "openocd -f interface/stlink.cfg -f target/stm32f4x.cfg" >/dev/null 2>&1 || true
openocd -f interface/stlink.cfg -f target/stm32f4x.cfg >/tmp/w5500_gate_openocd.log 2>&1 &
OCD_PID=$!
sleep 1
set +e
"$SCRIPT_DIR/run-startup-gate.sh"
gate_rc=$?
set -e
kill "$OCD_PID" >/dev/null 2>&1 || true

probe_one() {
  local label="$1"
  local symbol="$2"
  local addr
  addr="$(resolve_addr "$symbol")"
  if [[ -z "$addr" ]]; then
    echo "$label=ADDR_NOT_FOUND"
    return 2
  fi

  if "$SCRIPT_DIR/probe-one-breakpoint.sh" "$ELF_PATH" "0x$addr" "$label" "$PROBE_TIMEOUT" "$PROBE_ATTEMPTS" >/tmp/${label}.probe.log 2>&1; then
    echo "$label=HIT"
    return 0
  fi

  echo "$label=MISS"
  return 1
}

mkdir -p "$ROOT_DIR/.debug"

set +e
probe_one W5500_OPEN "Library_cubley_interop_W5500Socket_NativeOpen___STATIC__I4"
rc_open=$?
probe_one W5500_CONFIG "Library_cubley_interop_W5500Socket_NativeConfigureNetwork___STATIC__I4__STRING__STRING__STRING__STRING"
rc_cfg=$?
probe_one W5500_CONNECT "Library_cubley_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4"
rc_connect=$?
probe_one W5500_SEND "Library_cubley_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4"
rc_send=$?
probe_one W5500_RECV "Library_cubley_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4"
rc_recv=$?

mailbox_out="$("$ROOT_DIR/tests/swd_read_bringup_status.sh" "$ELF_PATH" 2>&1)"
mailbox_rc=$?
set -e

{
  echo "DEPLOY=$deploy_note"
  echo "STARTUP_GATE_RC=$gate_rc"
  if [[ -f "$ROOT_DIR/.debug/gdb_startup_gate.out" ]]; then
    cat "$ROOT_DIR/.debug/gdb_startup_gate.out"
  fi
  echo "W5500_OPEN_RC=$rc_open"
  echo "W5500_CONFIG_RC=$rc_cfg"
  echo "W5500_CONNECT_RC=$rc_connect"
  echo "W5500_SEND_RC=$rc_send"
  echo "W5500_RECV_RC=$rc_recv"
  echo "MAILBOX_RC=$mailbox_rc"
  echo "MAILBOX:"
  echo "$mailbox_out"
} > "$OUT_FILE"

cat "$OUT_FILE"

echo "Summary written to $OUT_FILE"
