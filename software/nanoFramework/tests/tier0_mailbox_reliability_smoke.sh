#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./tests/tier0_mailbox_reliability_smoke.sh [options]

Run repeated reset/read cycles and validate Tier-0 diagnostics mailbox invariants.

Options:
  --cycles <n>                 Number of reset cycles (default: 10)
  --settle-ms <ms>             Delay after reset before first sample (default: 1200)
  --read-count <n>             Number of repeated reads per cycle (default: 4)
  --read-delay-ms <ms>         Delay between repeated reads (default: 150)
  --boot-probe-timeout-ms <ms> Max wait for boot-probe latch to become non-zero (default: 6000)
  --boot-probe-poll-ms <ms>    Poll interval while waiting for boot-probe latch (default: 250)
  --elf <path>                 Path to nanoCLR ELF (default: build/nanoCLR.elf)
  --openocd-cfg <args>         OpenOCD cfg args string (default: "interface/stlink.cfg -f target/stm32f4x.cfg")
  --stop-on-fail               Stop at first failed cycle
  -h, --help                   Show this help

Exit codes:
  0 if all cycles pass, 1 otherwise.
EOF
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NF_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
source "$SCRIPT_DIR/phase_a_result_codec.sh"

CYCLES=10
SETTLE_MS=1200
READ_COUNT=4
READ_DELAY_MS=150
BOOT_PROBE_TIMEOUT_MS=6000
BOOT_PROBE_POLL_MS=250
ELF_PATH="$NF_ROOT/build/nanoCLR.elf"
ELF_EXPLICIT=0
OPENOCD_CFG="interface/stlink.cfg -f target/stm32f4x.cfg"
STOP_ON_FAIL=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --cycles)
      CYCLES="${2:-}"
      shift 2
      ;;
    --settle-ms)
      SETTLE_MS="${2:-}"
      shift 2
      ;;
    --read-count)
      READ_COUNT="${2:-}"
      shift 2
      ;;
    --read-delay-ms)
      READ_DELAY_MS="${2:-}"
      shift 2
      ;;
    --boot-probe-timeout-ms)
      BOOT_PROBE_TIMEOUT_MS="${2:-}"
      shift 2
      ;;
    --boot-probe-poll-ms)
      BOOT_PROBE_POLL_MS="${2:-}"
      shift 2
      ;;
    --elf)
      ELF_PATH="${2:-}"
      ELF_EXPLICIT=1
      shift 2
      ;;
    --openocd-cfg)
      OPENOCD_CFG="${2:-}"
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

if [[ -n "${GDB_BIN:-}" ]]; then
  GDB_BIN="$GDB_BIN"
elif command -v arm-none-eabi-gdb >/dev/null 2>&1; then
  GDB_BIN="arm-none-eabi-gdb"
elif command -v gdb-multiarch >/dev/null 2>&1; then
  GDB_BIN="gdb-multiarch"
else
  GDB_BIN="gdb"
fi

OPENOCD_BIN="${OPENOCD_BIN:-openocd}"

require_nonneg_int() {
  local name="$1"
  local value="$2"
  if ! [[ "$value" =~ ^[0-9]+$ ]]; then
    echo "Error: $name must be a non-negative integer." >&2
    exit 2
  fi
}

require_pos_int() {
  local name="$1"
  local value="$2"
  require_nonneg_int "$name" "$value"
  if [[ "$value" -le 0 ]]; then
    echo "Error: $name must be > 0." >&2
    exit 2
  fi
}

require_pos_int "--cycles" "$CYCLES"
require_nonneg_int "--settle-ms" "$SETTLE_MS"
require_pos_int "--read-count" "$READ_COUNT"
require_nonneg_int "--read-delay-ms" "$READ_DELAY_MS"
require_pos_int "--boot-probe-timeout-ms" "$BOOT_PROBE_TIMEOUT_MS"
require_pos_int "--boot-probe-poll-ms" "$BOOT_PROBE_POLL_MS"

if [[ ! -f "$ELF_PATH" ]]; then
  echo "ELF not found: $ELF_PATH" >&2
  exit 1
fi

if ! command -v "$OPENOCD_BIN" >/dev/null 2>&1; then
  echo "openocd not found (override with OPENOCD_BIN)." >&2
  exit 1
fi

if ! command -v "$GDB_BIN" >/dev/null 2>&1; then
  echo "gdb not found (override with GDB_BIN)." >&2
  exit 1
fi

has_required_diag_symbols() {
  local elf="$1"
  "$GDB_BIN" -q -batch \
    -ex "file $elf" \
    -ex "info address g_cubley_diag_current_status" \
    -ex "info address g_cubley_diag_boot_probe_status" \
    -ex "info address g_cubley_diag_clr_status" >/dev/null 2>&1
}

if ! has_required_diag_symbols "$ELF_PATH"; then
  if [[ "$ELF_EXPLICIT" -eq 0 ]]; then
    for candidate in \
      "$NF_ROOT/build/nf-interpreter/M0DMF_CUBLEY_F407/nanoCLR.elf" \
      "$NF_ROOT/build/nf-interpreter/M0DMF_CUBLEY_V0_4/nanoCLR.elf"; do
      if [[ -f "$candidate" ]] && has_required_diag_symbols "$candidate"; then
        ELF_PATH="$candidate"
        break
      fi
    done
  fi
fi

if ! has_required_diag_symbols "$ELF_PATH"; then
  cat >&2 <<EOF
ERROR: required diagnostics symbols were not found in ELF:
  $ELF_PATH

Use an unstripped nanoCLR ELF that contains:
  - g_cubley_diag_current_status
  - g_cubley_diag_boot_probe_status
  - g_cubley_diag_clr_status

Tip: pass an explicit image via --elf <path>.
EOF
  exit 1
fi

echo "Using ELF: $ELF_PATH"

if ! command -v st-flash >/dev/null 2>&1; then
  echo "st-flash not found on PATH." >&2
  exit 1
fi

tmp_dir="$(mktemp -d /tmp/tier0-mailbox-smoke-XXXXXX)"
trap 'rm -rf "$tmp_dir"' EXIT

sleep_ms() {
  local ms="$1"
  if [[ "$ms" -gt 0 ]]; then
    sleep "$(awk "BEGIN { printf \"%.3f\", $ms/1000 }")"
  fi
}

read_mailboxes_once() {
  local sample_id="$1"
  local openocd_log="$tmp_dir/openocd_${sample_id}.log"
  local gdb_cmd="$tmp_dir/read_${sample_id}.gdb"
  local gdb_out="$tmp_dir/gdb_${sample_id}.out"
  local gdb_attempt=0
  local gdb_max_attempts=15

  cat > "$gdb_cmd" <<'EOF_GDB'
set pagination off
set confirm off
target extended-remote :3333
monitor halt
set $current_addr = &g_cubley_diag_current_status
set $boot_probe_addr = &g_cubley_diag_boot_probe_status
set $clr_addr = &g_cubley_diag_clr_status
x/wx $current_addr
x/wx $boot_probe_addr
x/wx $clr_addr
monitor resume
quit
EOF_GDB

  "$OPENOCD_BIN" -f $OPENOCD_CFG >"$openocd_log" 2>&1 &
  local openocd_pid=$!

  local current_hex=""
  local boot_probe_hex=""
  local clr_hex=""

  while [[ "$gdb_attempt" -lt "$gdb_max_attempts" ]]; do
    if "$GDB_BIN" -q -batch -ex "file $ELF_PATH" -x "$gdb_cmd" >"$gdb_out" 2>&1; then
      mapfile -t vals < <(sed -n 's/.*:\s*\(0x[0-9a-fA-F]\+\).*/\1/p' "$gdb_out")
      if [[ ${#vals[@]} -ge 3 ]]; then
        current_hex="${vals[0]}"
        boot_probe_hex="${vals[1]}"
        clr_hex="${vals[2]}"
        break
      fi
    fi

    gdb_attempt=$((gdb_attempt + 1))
    sleep_ms 200
  done

  kill "$openocd_pid" >/dev/null 2>&1 || true

  if [[ -z "$current_hex" || -z "$boot_probe_hex" || -z "$clr_hex" ]]; then
    echo "ERROR: failed to read diagnostics symbols for sample '$sample_id' after ${gdb_max_attempts} attempts." >&2
    echo "--- gdb output ---" >&2
    cat "$gdb_out" >&2 || true
    echo "--- openocd output ---" >&2
    tail -n 50 "$openocd_log" >&2 || true
    return 1
  fi

  printf '%s %s %s\n' "$current_hex" "$boot_probe_hex" "$clr_hex"
  return 0
}

is_valid_result_code() {
  local result="$1"
  phase_a_result_label "$result" >/dev/null 2>&1
}

validate_status_word() {
  local label="$1"
  local value_hex="$2"
  local value_dec=$((value_hex))
  local magic=$(((value_dec >> 24) & 0xFF))
  local stage=$(((value_dec >> 16) & 0xFF))
  local result=$(((value_dec >> 8) & 0xFF))

  if [[ "$magic" -ne $((0xD5)) ]]; then
    echo "ERROR: $label has invalid magic (0x$(printf '%02X' "$magic"), expected 0xD5)." >&2
    return 1
  fi

  if ! is_valid_result_code "$result"; then
    echo "ERROR: $label has invalid result code $result (accepted: $(phase_a_result_contract_summary))." >&2
    return 1
  fi

  # Tier-0 boot-probe aggregate should stay on stage 0xF0 (or legacy decimal 226).
  if [[ "$label" == "boot_probe" && "$stage" -ne $((0xF0)) && "$stage" -ne 226 ]]; then
    echo "ERROR: boot_probe stage changed to $stage (expected 240/0xF0 or legacy 226)." >&2
    return 1
  fi

  return 0
}

FAIL_COUNT=0

printf 'tier0_mailbox_reliability_smoke: cycles=%s read_count=%s settle_ms=%s\n' "$CYCLES" "$READ_COUNT" "$SETTLE_MS"

for cycle in $(seq 1 "$CYCLES"); do
  cycle_id="$(printf '%02d' "$cycle")"
  cycle_fail=0
  printf '\n=== cycle %s ===\n' "$cycle_id"

  if ! st-flash reset >/dev/null 2>&1; then
    echo "cycle $cycle_id: FAIL (st-flash reset failed)" >&2
    FAIL_COUNT=$((FAIL_COUNT + 1))
    [[ "$STOP_ON_FAIL" -eq 1 ]] && break
    continue
  fi

  sleep_ms "$SETTLE_MS"

  # Wait until managed startup has had a chance to latch boot-probe, then validate.
  probe_sample=""
  deadline_ms=$(( $(date +%s%3N) + BOOT_PROBE_TIMEOUT_MS ))
  while [[ $(date +%s%3N) -lt "$deadline_ms" ]]; do
    if sample="$(read_mailboxes_once "${cycle_id}_wait")"; then
      read -r current_hex boot_probe_hex clr_hex <<< "$sample"
      if [[ "$boot_probe_hex" != "0x0" && "$boot_probe_hex" != "0x00000000" ]]; then
        probe_sample="$sample"
        break
      fi
    fi
    sleep_ms "$BOOT_PROBE_POLL_MS"
  done

  if [[ -z "$probe_sample" ]]; then
    echo "cycle $cycle_id: FAIL (boot_probe did not latch before timeout ${BOOT_PROBE_TIMEOUT_MS}ms)" >&2
    FAIL_COUNT=$((FAIL_COUNT + 1))
    [[ "$STOP_ON_FAIL" -eq 1 ]] && break
    continue
  fi

  read -r first_current first_boot first_clr <<< "$probe_sample"

  if ! validate_status_word "boot_probe" "$first_boot"; then
    cycle_fail=1
  fi

  if ! validate_status_word "clr_status" "$first_clr"; then
    cycle_fail=1
  fi

  if ! validate_status_word "current_status" "$first_current"; then
    cycle_fail=1
  fi

  for read_idx in $(seq 2 "$READ_COUNT"); do
    sleep_ms "$READ_DELAY_MS"
    if ! sample="$(read_mailboxes_once "${cycle_id}_r${read_idx}")"; then
      cycle_fail=1
      continue
    fi

    read -r current_hex boot_probe_hex clr_hex <<< "$sample"
    if ! validate_status_word "boot_probe" "$boot_probe_hex"; then
      cycle_fail=1
    fi
    if ! validate_status_word "clr_status" "$clr_hex"; then
      cycle_fail=1
    fi
    if ! validate_status_word "current_status" "$current_hex"; then
      cycle_fail=1
    fi

    if [[ "$boot_probe_hex" != "$first_boot" ]]; then
      echo "ERROR: cycle $cycle_id sticky boot_probe changed: first=$first_boot now=$boot_probe_hex" >&2
      cycle_fail=1
    fi
  done

  if [[ "$cycle_fail" -eq 0 ]]; then
    printf 'cycle %s: PASS boot_probe=%s clr=%s current=%s\n' "$cycle_id" "$first_boot" "$first_clr" "$first_current"
  else
    printf 'cycle %s: FAIL\n' "$cycle_id" >&2
    FAIL_COUNT=$((FAIL_COUNT + 1))
    [[ "$STOP_ON_FAIL" -eq 1 ]] && break
  fi
done

printf '\nSummary: fails=%s/%s\n' "$FAIL_COUNT" "$CYCLES"

if [[ "$FAIL_COUNT" -eq 0 ]]; then
  exit 0
fi

exit 1