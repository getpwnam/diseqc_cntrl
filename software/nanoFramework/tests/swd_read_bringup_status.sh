#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NF_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ELF_PATH="${1:-$NF_ROOT/build/nanoCLR.elf}"
OPENOCD_CFG="${OPENOCD_CFG:-interface/stlink.cfg -f target/stm32f4x.cfg}"
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

if [[ ! -f "$ELF_PATH" ]]; then
  echo "ELF not found: $ELF_PATH" >&2
  exit 1
fi

if ! command -v "$OPENOCD_BIN" >/dev/null 2>&1; then
  echo "openocd not found (override with OPENOCD_BIN)." >&2
  exit 1
fi

if ! command -v "$GDB_BIN" >/dev/null 2>&1; then
  echo "arm-none-eabi-gdb not found (override with GDB_BIN)." >&2
  exit 1
fi

tmp_dir="$(mktemp -d /tmp/w5500-mailbox-XXXXXX)"
trap 'rm -rf "$tmp_dir"' EXIT

openocd_log="$tmp_dir/openocd.log"
gdb_cmd="$tmp_dir/read_mailbox.gdb"
gdb_out="$tmp_dir/gdb.out"

cat > "$gdb_cmd" <<'EOF_GDB'
set pagination off
set confirm off
target extended-remote :3333
monitor halt
set $mailbox_addr = &g_w5500_bringup_status
x/wx $mailbox_addr
monitor resume
quit
EOF_GDB

$OPENOCD_BIN -f $OPENOCD_CFG >"$openocd_log" 2>&1 &
openocd_pid=$!
trap 'kill "$openocd_pid" >/dev/null 2>&1 || true; rm -rf "$tmp_dir"' EXIT

# Probe repeatedly for up to ~3 seconds while OpenOCD starts.
value_hex=""
for _ in $(seq 1 15); do
  if "$GDB_BIN" "$ELF_PATH" -q -batch -x "$gdb_cmd" >"$gdb_out" 2>&1; then
    value_hex="$(sed -n 's/.*:\s*\(0x[0-9a-fA-F]\+\).*/\1/p' "$gdb_out" | tail -n1)"
    if [[ -n "$value_hex" ]]; then
      break
    fi
  fi
  true

done

kill "$openocd_pid" >/dev/null 2>&1 || true

if [[ -z "$value_hex" ]]; then
  echo "Unable to read g_w5500_bringup_status." >&2
  echo "--- gdb output ---" >&2
  cat "$gdb_out" >&2 || true
  echo "--- openocd output ---" >&2
  tail -n 50 "$openocd_log" >&2 || true
  exit 1
fi

value_dec=$((value_hex))
magic=$(((value_dec >> 24) & 0xFF))
stage=$(((value_dec >> 16) & 0xFF))
result=$(((value_dec >> 8) & 0xFF))
detail=$((value_dec & 0xFF))

result_label="UNKNOWN"
case "$result" in
  0) result_label="RUNNING" ;;
  1) result_label="PASS" ;;
  2) result_label="WARN" ;;
  14) result_label="FAIL" ;;
  15) result_label="EXCEPTION" ;;
esac

printf 'Mailbox raw: %s\n' "$value_hex"
printf 'Magic: 0x%02X\n' "$magic"
printf 'Stage: %d\n' "$stage"
printf 'Result: %d (%s)\n' "$result" "$result_label"
printf 'Detail: %d\n' "$detail"

if [[ "$magic" -ne 213 ]]; then
  echo "Warning: magic byte mismatch (expected 0xD5)." >&2
fi
