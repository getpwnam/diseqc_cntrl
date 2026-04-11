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
  echo "gdb not found (override with GDB_BIN)." >&2
  exit 1
fi

tmp_dir="$(mktemp -d /tmp/w5500-diag-XXXXXX)"
trap 'rm -rf "$tmp_dir"' EXIT

openocd_log="$tmp_dir/openocd.log"
gdb_cmd="$tmp_dir/read_diag.gdb"
gdb_out="$tmp_dir/gdb.out"

cat > "$gdb_cmd" <<'EOF_GDB'
set pagination off
set confirm off
target extended-remote :3333
monitor halt
set $mailbox_addr = &g_w5500_bringup_status
set $error_addr = &g_w5500_last_native_error
x/wx $mailbox_addr
x/wx $error_addr
monitor resume
quit
EOF_GDB

$OPENOCD_BIN -f $OPENOCD_CFG >"$openocd_log" 2>&1 &
openocd_pid=$!
trap 'kill "$openocd_pid" >/dev/null 2>&1 || true; rm -rf "$tmp_dir"' EXIT

mailbox_hex=""
error_hex=""
for _ in $(seq 1 15); do
  if "$GDB_BIN" "$ELF_PATH" -q -batch -x "$gdb_cmd" >"$gdb_out" 2>&1; then
    mapfile -t vals < <(sed -n 's/.*:\s*\(0x[0-9a-fA-F]\+\).*/\1/p' "$gdb_out")
    if [[ ${#vals[@]} -ge 2 ]]; then
      mailbox_hex="${vals[0]}"
      error_hex="${vals[1]}"
      break
    fi
  fi
  true
done

kill "$openocd_pid" >/dev/null 2>&1 || true

if [[ -z "$mailbox_hex" || -z "$error_hex" ]]; then
  echo "Unable to read diagnostics symbols." >&2
  echo "--- gdb output ---" >&2
  cat "$gdb_out" >&2 || true
  echo "--- openocd output ---" >&2
  tail -n 50 "$openocd_log" >&2 || true
  exit 1
fi

mailbox_dec=$((mailbox_hex))
mb_magic=$(((mailbox_dec >> 24) & 0xFF))
mb_stage=$(((mailbox_dec >> 16) & 0xFF))
mb_result=$(((mailbox_dec >> 8) & 0xFF))
mb_detail=$((mailbox_dec & 0xFF))

result_label="UNKNOWN"
case "$mb_result" in
  0) result_label="RUNNING" ;;
  1) result_label="PASS" ;;
  2) result_label="WARN" ;;
  14) result_label="FAIL" ;;
  15) result_label="EXCEPTION" ;;
esac

error_dec=$((error_hex))
err_op=$(((error_dec >> 16) & 0xFF))
err_code=$(((error_dec >> 8) & 0xFF))
err_detail=$((error_dec & 0xFF))

printf 'Bringup mailbox raw: %s\n' "$mailbox_hex"
printf '  Magic: 0x%02X\n' "$mb_magic"
printf '  Stage: %d\n' "$mb_stage"
printf '  Result: %d (%s)\n' "$mb_result" "$result_label"
printf '  Detail: %d (0x%02X)\n' "$mb_detail" "$mb_detail"

printf 'Native error raw:    %s\n' "$error_hex"
printf '  OpCode: 0x%02X\n' "$err_op"
printf '  Code:   0x%02X\n' "$err_code"
printf '  Detail: 0x%02X\n' "$err_detail"

decode_phycfgr() {
  local phy="$1"
  local lnk=$((phy & 0x01))
  local spd=$(((phy >> 1) & 0x01))
  local dpx=$(((phy >> 2) & 0x01))
  local opmdc=$(((phy >> 3) & 0x07))
  local opmd=$(((phy >> 6) & 0x01))

  local link_str="DOWN"
  local speed_str="10M"
  local duplex_str="HALF"
  local mode_src="HW straps"
  local mode_hint="mode-code"

  [[ "$lnk" -eq 1 ]] && link_str="UP"
  [[ "$spd" -eq 1 ]] && speed_str="100M"
  [[ "$dpx" -eq 1 ]] && duplex_str="FULL"
  [[ "$opmd" -eq 1 ]] && mode_src="SW config"

  if [[ "$opmd" -eq 1 && "$opmdc" -eq 7 ]]; then
    mode_hint="all-capable auto-neg (prefers 100FDX when partner supports it)"
  elif [[ "$opmd" -eq 1 ]]; then
    mode_hint="forced/alternate mode code"
  else
    mode_hint="hardware strap mode from PMODE pins"
  fi

  printf '  PHYCFGR decode: link=%s speed=%s duplex=%s opmd=%s opmdc=0x%X (%s)\n' "$link_str" "$speed_str" "$duplex_str" "$mode_src" "$opmdc" "$mode_hint"
}

if [[ "$mb_magic" -ne 213 ]]; then
  echo "Warning: mailbox magic mismatch (expected 0xD5)." >&2
fi

if [[ "$err_op" -eq 0x51 ]]; then
  echo "Hint: opcode 0x51 = PHYCFGR snapshot; detail byte is PHYCFGR." >&2
  decode_phycfgr "$err_detail"
elif [[ "$err_op" -eq 0x53 ]]; then
  echo "Hint: opcode 0x53 = combined snapshot; code byte is VERSIONR, detail byte is PHYCFGR." >&2
  printf '  VERSIONR decode: 0x%02X%s\n' "$err_code" "$([[ "$err_code" -eq 0x04 ]] && echo ' (expected for W5500)' || echo ' (unexpected)')"
  decode_phycfgr "$err_detail"
elif [[ "$err_op" -eq 0x54 ]]; then
  echo "Hint: opcode 0x54 = packed NativeGetVersionPhyStatus; code byte is VERSIONR, detail byte is PHYCFGR." >&2
  printf '  VERSIONR decode: 0x%02X%s\n' "$err_code" "$([[ "$err_code" -eq 0x04 ]] && echo ' (expected for W5500)' || echo ' (unexpected)')"
  decode_phycfgr "$err_detail"
fi
