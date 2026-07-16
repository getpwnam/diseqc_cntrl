#!/usr/bin/env bash
set -u -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SERIAL_PORT="${SERIAL_PORT:-/dev/ttyUSB0}"
BAUD="${BAUD:-115200}"
NANOFF_TIMEOUT="${NANOFF_TIMEOUT:-20}"
RAW_CAPTURE_BYTES="${RAW_CAPTURE_BYTES:-512}"
REQUIRE_RAW_BYTES="${REQUIRE_RAW_BYTES:-0}"
DO_RESET=1
LOG_DIR=""

usage() {
  cat <<'EOF'
Usage:
  ./toolchain/uart-preflight.sh [options]

Options:
  --serial <port>         Serial device to probe. Default: /dev/ttyUSB0
  --baud <rate>           UART baud rate. Default: 115200
  --timeout <seconds>     nanoff timeout in seconds. Default: 20
  --capture-bytes <n>     Raw UART capture size. Default: 512
  --require-raw-bytes     Fail if raw UART captures are empty.
  --skip-reset            Do not issue st-flash reset before post-reset capture.
  --log-dir <dir>         Write logs into this directory.
  -h, --help              Show this help.

Environment:
  SERIAL_PORT, BAUD, NANOFF_TIMEOUT, RAW_CAPTURE_BYTES, REQUIRE_RAW_BYTES
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --serial)
      SERIAL_PORT="${2:-}"
      shift 2
      ;;
    --baud)
      BAUD="${2:-}"
      shift 2
      ;;
    --timeout)
      NANOFF_TIMEOUT="${2:-}"
      shift 2
      ;;
    --capture-bytes)
      RAW_CAPTURE_BYTES="${2:-}"
      shift 2
      ;;
    --require-raw-bytes)
      REQUIRE_RAW_BYTES=1
      shift
      ;;
    --skip-reset)
      DO_RESET=0
      shift
      ;;
    --log-dir)
      LOG_DIR="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$LOG_DIR" ]]; then
  LOG_DIR="${REPO_ROOT}/.debug/uart_preflight_$(date -u +%Y%m%d_%H%M%S)"
fi

mkdir -p "$LOG_DIR"
SUMMARY_FILE="$LOG_DIR/summary.txt"
STATUS=0

log() {
  printf '%s\n' "$*" | tee -a "$SUMMARY_FILE"
}

run_capture() {
  local label="$1"
  local outfile="$LOG_DIR/${label}.log"
  local rc=0

  timeout 8s sh -c "dd if='${SERIAL_PORT}' bs=1 count='${RAW_CAPTURE_BYTES}' 2>/dev/null | xxd -g1 -u" >"$outfile" || rc=$?

  log "${label}: rc=${rc} file=${outfile}"

  if [[ -s "$outfile" ]]; then
    log "${label}: captured-bytes=yes"
  else
    log "${label}: captured-bytes=no"
    if [[ "$REQUIRE_RAW_BYTES" == "1" ]]; then
      STATUS=1
    fi
  fi

  if [[ $rc -ne 0 && $rc -ne 124 ]]; then
    STATUS=1
  fi
}

run_capture_during_reset() {
  local label="$1"
  local outfile="$LOG_DIR/${label}.log"
  local rc=0

  timeout 10s sh -c "dd if='${SERIAL_PORT}' bs=1 count='${RAW_CAPTURE_BYTES}' 2>/dev/null | xxd -g1 -u" >"$outfile" &
  local cap_pid=$!

  # Give dd a brief moment to open the serial port before reset.
  sleep 0.2

  if command -v st-flash >/dev/null 2>&1; then
    st-flash reset >"$LOG_DIR/st_flash_reset.log" 2>&1 || STATUS=1
    log "st_flash_reset=file=${LOG_DIR}/st_flash_reset.log"
  else
    log "st_flash_available=no"
    STATUS=1
  fi

  wait "$cap_pid" || rc=$?

  log "${label}: rc=${rc} file=${outfile}"

  if [[ -s "$outfile" ]]; then
    log "${label}: captured-bytes=yes"
  else
    log "${label}: captured-bytes=no"
    if [[ "$REQUIRE_RAW_BYTES" == "1" ]]; then
      STATUS=1
    fi
  fi

  if [[ $rc -ne 0 && $rc -ne 124 ]]; then
    STATUS=1
  fi
}

run_nanoff() {
  local label="$1"
  shift
  local outfile="$LOG_DIR/${label}.log"
  local rc=0

  timeout "${NANOFF_TIMEOUT}s" "$@" >"$outfile" 2>&1 || rc=$?
  log "${label}: rc=${rc} file=${outfile}"

  if grep -q "No devices found" "$outfile" 2>/dev/null; then
    log "${label}: result=no-devices"
    STATUS=1
  fi

  if grep -q "Error E2001" "$outfile" 2>/dev/null; then
    log "${label}: result=E2001"
    STATUS=1
  fi

  if [[ $rc -eq 124 ]]; then
    log "${label}: result=timeout"
    STATUS=1
  elif [[ $rc -ne 0 ]]; then
    STATUS=1
  fi
}

log "uart-preflight"
log "repo=${REPO_ROOT}"
log "serial_port=${SERIAL_PORT}"
log "baud=${BAUD}"
log "nanoff_timeout=${NANOFF_TIMEOUT}"
log "raw_capture_bytes=${RAW_CAPTURE_BYTES}"
log "require_raw_bytes=${REQUIRE_RAW_BYTES}"
log "log_dir=${LOG_DIR}"

if [[ ! -e "$SERIAL_PORT" ]]; then
  log "serial_port_present=no"
  exit 1
fi

log "serial_port_present=yes"
ls -l "$SERIAL_PORT" >"$LOG_DIR/serial_device_ls.txt" 2>&1 || STATUS=1

if command -v lsof >/dev/null 2>&1; then
  lsof "$SERIAL_PORT" >"$LOG_DIR/lsof.txt" 2>&1 || true
  if [[ -s "$LOG_DIR/lsof.txt" ]]; then
    log "serial_port_in_use=yes file=${LOG_DIR}/lsof.txt"
  else
    log "serial_port_in_use=no"
  fi
else
  log "lsof_available=no"
fi

stty -F "$SERIAL_PORT" "$BAUD" raw -echo -echoe -echok -echoctl -echoke >"$LOG_DIR/stty.log" 2>&1 || STATUS=1
log "stty_configured=$( [[ -s "$LOG_DIR/stty.log" ]] && echo maybe || echo yes ) file=${LOG_DIR}/stty.log"

if command -v nanoff >/dev/null 2>&1; then
  run_nanoff nanoff_listports nanoff --listports
  run_nanoff nanoff_listdevices nanoff --nanodevice --serialport "$SERIAL_PORT" --baud "$BAUD" --listdevices
  run_nanoff nanoff_devicedetails nanoff --nanodevice --serialport "$SERIAL_PORT" --baud "$BAUD" --devicedetails
else
  log "nanoff_available=no"
  STATUS=1
fi

run_capture raw_capture_prereset

if [[ $DO_RESET -eq 1 ]]; then
  run_capture_during_reset raw_capture_postreset
else
  run_capture raw_capture_postreset
fi

log "overall_status=${STATUS}"
exit "$STATUS"