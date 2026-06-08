#!/usr/bin/env bash
set -u -o pipefail

usage() {
  cat <<'EOF'
Usage:
  ./toolchain/run-deterministic-cycles.sh [options]

Runs repeated flash/reset cycles and probes UART wire-protocol health.

Options:
  --cycles <n>            Number of cycles (default: 20)
  --serial <port>         Serial device (default: /dev/ttyUSB0)
  --baud <rate>           Serial baud rate (default: 115200)
  --settle-ms <ms>        Delay after reset before nanoff probes (default: 2000)
  --listdevices-retries <n>
                          Extra retries for nanoff --listdevices (default: 0)
  --devicedetails-retries <n>
                          Extra retries for nanoff --devicedetails (default: 0)
  --retry-delay-ms <ms>   Delay between probe retries (default: 500)
  --booter <path>         nanoBooter image (default: build/nanoBooter.bin)
  --clr <path>            nanoCLR image (default: build/nanoCLR.bin)
  --bootaddr <hex>        nanoBooter flash address (default: 0x08000000)
  --clraddr <hex>         nanoCLR flash address (default: 0x08004000)
  --log-root <dir>        Output log root (default: .debug/issue26_campaign_<utc>)
  --stop-on-fail          Stop immediately on first failed cycle
  -h, --help              Show help

Exit codes:
  0 if all cycles pass; 1 otherwise.
EOF
}

CYCLES=20
SERIAL_PORT="/dev/ttyUSB0"
BAUD=115200
SETTLE_MS=2000
LISTDEVICES_RETRIES=0
DEVICEDETAILS_RETRIES=0
RETRY_DELAY_MS=500
BOOTER_IMG="build/nanoBooter.bin"
CLR_IMG="build/nanoCLR.bin"
BOOT_ADDR="0x08000000"
CLR_ADDR="0x08004000"
LOG_ROOT=""
STOP_ON_FAIL=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --cycles)
      CYCLES="${2:-}"
      shift 2
      ;;
    --serial)
      SERIAL_PORT="${2:-}"
      shift 2
      ;;
    --baud)
      BAUD="${2:-}"
      shift 2
      ;;
    --settle-ms)
      SETTLE_MS="${2:-}"
      shift 2
      ;;
    --listdevices-retries)
      LISTDEVICES_RETRIES="${2:-}"
      shift 2
      ;;
    --devicedetails-retries)
      DEVICEDETAILS_RETRIES="${2:-}"
      shift 2
      ;;
    --retry-delay-ms)
      RETRY_DELAY_MS="${2:-}"
      shift 2
      ;;
    --booter)
      BOOTER_IMG="${2:-}"
      shift 2
      ;;
    --clr)
      CLR_IMG="${2:-}"
      shift 2
      ;;
    --bootaddr)
      BOOT_ADDR="${2:-}"
      shift 2
      ;;
    --clraddr)
      CLR_ADDR="${2:-}"
      shift 2
      ;;
    --log-root)
      LOG_ROOT="${2:-}"
      shift 2
      ;;
    --stop-on-fail)
      STOP_ON_FAIL=1
      shift
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

if ! [[ "$CYCLES" =~ ^[0-9]+$ ]] || [[ "$CYCLES" -le 0 ]]; then
  echo "Error: --cycles must be a positive integer." >&2
  exit 2
fi

if ! [[ "$BAUD" =~ ^[0-9]+$ ]] || [[ "$BAUD" -le 0 ]]; then
  echo "Error: --baud must be a positive integer." >&2
  exit 2
fi

if ! [[ "$SETTLE_MS" =~ ^[0-9]+$ ]]; then
  echo "Error: --settle-ms must be a non-negative integer." >&2
  exit 2
fi

if ! [[ "$LISTDEVICES_RETRIES" =~ ^[0-9]+$ ]]; then
  echo "Error: --listdevices-retries must be a non-negative integer." >&2
  exit 2
fi

if ! [[ "$DEVICEDETAILS_RETRIES" =~ ^[0-9]+$ ]]; then
  echo "Error: --devicedetails-retries must be a non-negative integer." >&2
  exit 2
fi

if ! [[ "$RETRY_DELAY_MS" =~ ^[0-9]+$ ]]; then
  echo "Error: --retry-delay-ms must be a non-negative integer." >&2
  exit 2
fi

if [[ -z "$LOG_ROOT" ]]; then
  LOG_ROOT=".debug/issue26_campaign_$(date -u +%Y%m%dT%H%M%SZ)"
fi

mkdir -p "$LOG_ROOT"
SUMMARY_FILE="$LOG_ROOT/summary.log"
FAIL_COUNT=0
PROBE_RC=1

log() {
  printf '%s\n' "$*" | tee -a "$SUMMARY_FILE"
}

if [[ ! -f "$BOOTER_IMG" ]]; then
  echo "Error: booter image not found: $BOOTER_IMG" >&2
  exit 1
fi

if [[ ! -f "$CLR_IMG" ]]; then
  echo "Error: CLR image not found: $CLR_IMG" >&2
  exit 1
fi

GIT_REV="$(git rev-parse --short HEAD 2>/dev/null || echo unknown)"
log "campaign_root=$LOG_ROOT git_rev=$GIT_REV"
log "serial_port=$SERIAL_PORT baud=$BAUD settle_ms=$SETTLE_MS cycles=$CYCLES"
log "listdevices_retries=$LISTDEVICES_RETRIES devicedetails_retries=$DEVICEDETAILS_RETRIES retry_delay_ms=$RETRY_DELAY_MS"
log "booter=$BOOTER_IMG bootaddr=$BOOT_ADDR"
log "clr=$CLR_IMG clraddr=$CLR_ADDR"

run_probe_with_retries() {
  local cycle_id="$1"
  local cycle_dir="$2"
  local stem="$3"
  local retries="$4"
  shift 4

  local attempt=0
  local rc=1
  local total_attempts=$((retries + 1))
  local start_ms=0
  local end_ms=0
  local elapsed=0

  while [[ "$attempt" -lt "$total_attempts" ]]; do
    local out_file="$cycle_dir/${stem}_attempt_$(printf '%02d' "$attempt").log"
    start_ms=$(date +%s%3N)
    "$@" >"$out_file" 2>&1
    rc=$?

    # nanoff --listdevices exits 0 even when no target is detected; treat that as a retryable failure.
    if [[ "$rc" -eq 0 && "$stem" == "nanoff_listdevices" ]] && grep -qi "No devices found" "$out_file"; then
      rc=201
    fi

    # Also require devicedetails payload markers when rc=0.
    # nanoff output varies by version; accept either legacy or current headers.
    if [[ "$rc" -eq 0 && "$stem" == "nanoff_devicedetails" ]] && ! grep -Eqi "Target name:|HAL build info:|nanoCLR running" "$out_file"; then
      rc=202
    fi

    end_ms=$(date +%s%3N)
    elapsed=$((end_ms - start_ms))

    log "cycle ${cycle_id}: ${stem}_attempt=$attempt rc=$rc elapsed_ms=$elapsed log=$(basename "$out_file")"

    if [[ "$rc" -eq 0 ]]; then
      PROBE_RC="$rc"
      return 0
    fi

    attempt=$((attempt + 1))
    if [[ "$attempt" -lt "$total_attempts" && "$RETRY_DELAY_MS" -gt 0 ]]; then
      sleep "$(awk "BEGIN { printf \"%.3f\", $RETRY_DELAY_MS/1000 }")"
    fi
  done

  PROBE_RC="$rc"
  return 0
}

for i in $(seq 1 "$CYCLES"); do
  CYCLE_ID=$(printf '%02d' "$i")
  CYCLE_DIR="$LOG_ROOT/cycle_$CYCLE_ID"
  mkdir -p "$CYCLE_DIR"

  log "--- cycle $CYCLE_ID ---"

  if st-flash write "$BOOTER_IMG" "$BOOT_ADDR" >"$CYCLE_DIR/st_flash_booter.log" 2>&1; then
    log "cycle $CYCLE_ID: booter_flash=OK"
  else
    log "cycle $CYCLE_ID: booter_flash=FAIL"
    FAIL_COUNT=$((FAIL_COUNT + 1))
    [[ "$STOP_ON_FAIL" -eq 1 ]] && break
    continue
  fi

  if st-flash write "$CLR_IMG" "$CLR_ADDR" >"$CYCLE_DIR/st_flash_clr.log" 2>&1; then
    log "cycle $CYCLE_ID: clr_flash=OK"
  else
    log "cycle $CYCLE_ID: clr_flash=FAIL"
    FAIL_COUNT=$((FAIL_COUNT + 1))
    [[ "$STOP_ON_FAIL" -eq 1 ]] && break
    continue
  fi

  if st-flash reset >"$CYCLE_DIR/st_flash_reset.log" 2>&1; then
    log "cycle $CYCLE_ID: reset=OK"
  else
    log "cycle $CYCLE_ID: reset=FAIL"
    FAIL_COUNT=$((FAIL_COUNT + 1))
    [[ "$STOP_ON_FAIL" -eq 1 ]] && break
    continue
  fi

  if [[ "$SETTLE_MS" -gt 0 ]]; then
    sleep "$(awk "BEGIN { printf \"%.3f\", $SETTLE_MS/1000 }")"
  fi

  nanoff --listports >"$CYCLE_DIR/nanoff_listports.log" 2>&1 || true

  run_probe_with_retries "$CYCLE_ID" "$CYCLE_DIR" "nanoff_listdevices" "$LISTDEVICES_RETRIES" \
    nanoff --nanodevice --serialport "$SERIAL_PORT" --baud "$BAUD" --listdevices
  LD_RC="$PROBE_RC"

  run_probe_with_retries "$CYCLE_ID" "$CYCLE_DIR" "nanoff_devicedetails" "$DEVICEDETAILS_RETRIES" \
    nanoff --nanodevice --serialport "$SERIAL_PORT" --baud "$BAUD" --devicedetails
  DD_RC="$PROBE_RC"

  if [[ "$LD_RC" -eq 0 && "$DD_RC" -eq 0 ]]; then
    log "cycle $CYCLE_ID: listdevices_rc=$LD_RC devicedetails_rc=$DD_RC result=PASS"
  else
    log "cycle $CYCLE_ID: listdevices_rc=$LD_RC devicedetails_rc=$DD_RC result=FAIL"
    FAIL_COUNT=$((FAIL_COUNT + 1))
    [[ "$STOP_ON_FAIL" -eq 1 ]] && break
  fi
done

log "CAMPAIGN COMPLETE: fails=$FAIL_COUNT/$CYCLES"
printf 'CAMPAIGN_ROOT=%s\n' "$LOG_ROOT"

if [[ "$FAIL_COUNT" -eq 0 ]]; then
  exit 0
fi

exit 1
