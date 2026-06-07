#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NF_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ELF_PATH="${1:-$NF_ROOT/build/nanoCLR.elf}"
OPENOCD_CFG="${OPENOCD_CFG:-interface/stlink.cfg -f target/stm32f4x.cfg}"
source "$SCRIPT_DIR/phase_a_result_codec.sh"
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
STARTUP_PROBE_CS="${STARTUP_PROBE_CS:-$NF_ROOT/DiSEqC_Control/StartupProbe.cs}"

# Allow override from environment; auto-detect from StartupProbe.cs when available.
probe_w5500_on_startup="${PROBE_W5500_ON_STARTUP:-auto}"
probe_fram_on_startup="${PROBE_FRAM_ON_STARTUP:-auto}"

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

if [[ -f "$STARTUP_PROBE_CS" ]]; then
  if [[ "$probe_w5500_on_startup" == "auto" ]]; then
    if grep -Eq 'ProbeW5500OnStartup\s*=\s*true' "$STARTUP_PROBE_CS"; then
      probe_w5500_on_startup="true"
    elif grep -Eq 'ProbeW5500OnStartup\s*=\s*false' "$STARTUP_PROBE_CS"; then
      probe_w5500_on_startup="false"
    fi
  fi

  if [[ "$probe_fram_on_startup" == "auto" ]]; then
    if grep -Eq 'ProbeFramOnStartup\s*=\s*true' "$STARTUP_PROBE_CS"; then
      probe_fram_on_startup="true"
    elif grep -Eq 'ProbeFramOnStartup\s*=\s*false' "$STARTUP_PROBE_CS"; then
      probe_fram_on_startup="false"
    fi
  fi
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
set $current_addr = &g_cubley_diag_current_status
set $boot_probe_addr = &g_cubley_diag_boot_probe_status
set $clr_addr = &g_cubley_diag_clr_status
x/wx $current_addr
x/wx $boot_probe_addr
x/wx $clr_addr
monitor resume
quit
EOF_GDB

$OPENOCD_BIN -f $OPENOCD_CFG >"$openocd_log" 2>&1 &
openocd_pid=$!
trap 'kill "$openocd_pid" >/dev/null 2>&1 || true; rm -rf "$tmp_dir"' EXIT

# Probe repeatedly for up to ~3 seconds while OpenOCD starts.
current_hex=""
boot_probe_hex=""
clr_hex=""
for _ in $(seq 1 15); do
  if "$GDB_BIN" "$ELF_PATH" -q -batch -x "$gdb_cmd" >"$gdb_out" 2>&1; then
    mapfile -t vals < <(sed -n 's/.*:\s*\(0x[0-9a-fA-F]\+\).*/\1/p' "$gdb_out")
    if [[ ${#vals[@]} -ge 3 ]]; then
      current_hex="${vals[0]}"
      boot_probe_hex="${vals[1]}"
      clr_hex="${vals[2]}"
      break
    fi
  fi
  true

done

kill "$openocd_pid" >/dev/null 2>&1 || true

if [[ -z "$current_hex" || -z "$boot_probe_hex" || -z "$clr_hex" ]]; then
  echo "Unable to read Cubley diagnostics mailboxes." >&2
  echo "--- gdb output ---" >&2
  cat "$gdb_out" >&2 || true
  echo "--- openocd output ---" >&2
  tail -n 50 "$openocd_log" >&2 || true
  exit 1
fi

decode_word() {
  local label="$1"
  local value_hex="$2"
  local value_dec=$((value_hex))
  local magic=$(((value_dec >> 24) & 0xFF))
  local stage=$(((value_dec >> 16) & 0xFF))
  local result=$(((value_dec >> 8) & 0xFF))
  local detail=$((value_dec & 0xFF))

  local is_valid_result=1
  local result_label
  if ! result_label="$(phase_a_result_label "$result")"; then
    result_label="INVALID"
    is_valid_result=0
  fi

  printf '%s raw: %s\n' "$label" "$value_hex"
  printf '  Magic: 0x%02X\n' "$magic"
  printf '  Stage: %d\n' "$stage"
  printf '  Result: %d (%s)\n' "$result" "$result_label"
  printf '  Detail: %d (0x%02X)\n' "$detail" "$detail"

  if [[ "$magic" -ne $((0xD5)) ]]; then
    echo "  ERROR: magic byte mismatch (expected 0xD5)." >&2
    return 1
  fi

  if [[ "$is_valid_result" -eq 0 ]]; then
    echo "  ERROR: unknown/invalid result code (accepted: $(phase_a_result_contract_summary))." >&2
    return 1
  fi

  if [[ "$label" == "Boot probe" && ( "$stage" -eq $((0xF0)) || "$stage" -eq 226 ) ]]; then
    local has_w5500="detected"
    local has_lnb="detected"
    local has_fram="detected"

    if (( (detail & 0x01) == 0 )); then
      if [[ "$probe_w5500_on_startup" == "false" ]]; then
        has_w5500="skipped-by-firmware-config"
      else
        has_w5500="not-detected"
      fi
    fi

    if (( (detail & 0x02) == 0 )); then
      has_lnb="not-detected"
    fi

    if (( (detail & 0x04) == 0 )); then
      if [[ "$probe_fram_on_startup" == "false" ]]; then
        has_fram="skipped-by-firmware-config"
      else
        has_fram="not-detected"
      fi
    fi

    printf '  Hardware bitmap: W5500=%s LNBH26=%s FRAM=%s\n' "$has_w5500" "$has_lnb" "$has_fram"
    printf '  Bitmap decode: bit0=W5500 bit1=LNBH26 bit2=FRAM\n'
    printf '  Probe config: W5500=%s FRAM=%s (source: %s)\n' "$probe_w5500_on_startup" "$probe_fram_on_startup" "$STARTUP_PROBE_CS"
    printf '  Note: LNBH26=detected means the startup I2C probe got ACK at bus1 addr 0x08; if the chip is absent this suggests an address/bus mismatch or a false-positive ACK path.\n'
  fi

  if component_label="$(phase_a_component_label "$detail" 2>/dev/null)"; then
    printf '  Phase-A component: %s\n' "$component_label"
  fi
}

decode_word "Current status" "$current_hex" || exit 1
decode_word "Boot probe" "$boot_probe_hex" || exit 1
decode_word "CLR startup" "$clr_hex" || exit 1
