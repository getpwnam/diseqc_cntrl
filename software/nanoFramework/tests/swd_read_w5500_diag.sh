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
set $link_latch_addr = &g_w5500_first_link_up
set $connect_params_addr = &g_w5500_connect_params
x/wx $mailbox_addr
x/wx $error_addr
x/wx $link_latch_addr
x/wx $connect_params_addr
set $post_connect_addr = &g_w5500_post_connect_sr
x/wx $post_connect_addr
monitor resume
quit
EOF_GDB

$OPENOCD_BIN -f $OPENOCD_CFG >"$openocd_log" 2>&1 &
openocd_pid=$!
trap 'kill "$openocd_pid" >/dev/null 2>&1 || true; rm -rf "$tmp_dir"' EXIT

mailbox_hex=""
error_hex=""
link_latch_hex=""
for _ in $(seq 1 15); do
  if "$GDB_BIN" "$ELF_PATH" -q -batch -x "$gdb_cmd" >"$gdb_out" 2>&1; then
    mapfile -t vals < <(sed -n 's/.*:\s*\(0x[0-9a-fA-F]\+\).*/\1/p' "$gdb_out")
    if [[ ${#vals[@]} -ge 2 ]]; then
      mailbox_hex="${vals[0]}"
      error_hex="${vals[1]}"
      link_latch_hex="${vals[2]:-0x00000000}"
      connect_params_hex="${vals[3]:-0x00000000}"
      post_connect_hex="${vals[4]:-0x00000000}"
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

connect_params_hex="${connect_params_hex:-0x00000000}"
post_connect_hex="${post_connect_hex:-0x00000000}"

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
  local speed_str="N/A"
  local duplex_str="N/A"
  local mode_src="HW straps"
  local mode_hint="mode-code"

  [[ "$lnk" -eq 1 ]] && link_str="UP"
  if [[ "$lnk" -eq 1 ]]; then
    speed_str="10M"
    duplex_str="HALF"
    [[ "$spd" -eq 1 ]] && speed_str="100M"
    [[ "$dpx" -eq 1 ]] && duplex_str="FULL"
  fi
  [[ "$opmd" -eq 1 ]] && mode_src="SW config"

  if [[ "$opmd" -eq 1 && "$opmdc" -eq 7 ]]; then
    mode_hint="all-capable auto-neg (prefers 100FDX when partner supports it)"
  elif [[ "$opmd" -eq 1 ]]; then
    mode_hint="forced/alternate mode code"
  else
    mode_hint="hardware-controlled mode active (OPMDC meaning is limited when OPMD=0)"
  fi

  printf '  PHYCFGR decode: link=%s speed=%s duplex=%s opmd=%s opmdc=0x%X (%s)\n' "$link_str" "$speed_str" "$duplex_str" "$mode_src" "$opmdc" "$mode_hint"
  if [[ "$lnk" -eq 0 ]]; then
    echo '  Note: speed/duplex are meaningful only when link=UP.'
  fi
}

if [[ "$mb_magic" -ne 213 ]]; then
  echo "Warning: mailbox magic mismatch (expected 0xD5)." >&2
fi

if [[ "$err_op" -eq 0x51 ]]; then
  echo "Hint: opcode 0x51 = PHYCFGR snapshot; detail byte is PHYCFGR." >&2
  decode_phycfgr "$err_detail"
elif [[ "$err_op" -eq 0x44 ]]; then
  echo "Hint: opcode 0x44 = pre-soft-reset PHY snapshot; code byte is OPMDC, detail byte is PHYCFGR." >&2
  printf '  OPMDC(code) decode: 0x%02X\n' "$err_code"
  decode_phycfgr "$err_detail"
elif [[ "$err_op" -eq 0x46 ]]; then
  echo "Hint: opcode 0x46 = readback after SW-mode write without RST; expected PHYCFGR includes OPMD=1 (usually 0x78)." >&2
  printf '  OPMD after SW write: %s\n' "$([[ "$err_code" -eq 0xA1 ]] && echo 'SW config (OPMD=1)' || echo 'not set (OPMD=0)')"
  decode_phycfgr "$err_detail"
elif [[ "$err_op" -eq 0x53 ]]; then
  echo "Hint: opcode 0x53 = combined snapshot; code byte is VERSIONR, detail byte is PHYCFGR." >&2
  printf '  VERSIONR decode: 0x%02X%s\n' "$err_code" "$([[ "$err_code" -eq 0x04 ]] && echo ' (expected for W5500)' || echo ' (unexpected)')"
  decode_phycfgr "$err_detail"
elif [[ "$err_op" -eq 0x43 ]]; then
  echo "Hint: opcode 0x43 = post-RST-write readback (intermediate; RST may still be set); code=0xA1 means OPMD=1 confirmed, detail is PHYCFGR." >&2
  printf '  OPMD from code: %s\n' "$([[ "$err_code" -eq 0xA1 ]] && echo 'SW config (OPMD=1)' || echo 'HW straps (OPMD=0)')"
  decode_phycfgr "$err_detail"
elif [[ "$err_op" -eq 0x45 ]]; then
  echo "Hint: opcode 0x45 = post-reset settled PHYCFGR after explicit SW-mode re-assert; OPMD=1 means SW config is active at end of init." >&2
  printf '  OPMD settled: %s\n' "$([[ "$err_code" -eq 0xA1 ]] && echo 'SW config (OPMD=1) -- autoneg active' || echo 'hardware-controlled mode active (OPMD=0)')"
  decode_phycfgr "$err_detail"
elif [[ "$err_op" -eq 0x54 ]]; then
  echo "Hint: opcode 0x54 = runtime VERSIONR+PHYCFGR snapshot; code byte is VERSIONR, detail byte is current PHYCFGR." >&2
  printf '  VERSIONR decode: 0x%02X%s\n' "$err_code" "$([[ "$err_code" -eq 0x04 ]] && echo ' (expected for W5500)' || echo ' (unexpected)')"
  decode_phycfgr "$err_detail"
elif [[ "$err_op" -eq 0x67 ]]; then
  echo "Hint: opcode 0x67 = entered w5500_connect(); detail byte is socket index." >&2
elif [[ "$err_op" -eq 0x68 ]]; then
  echo "Hint: opcode 0x68 = CMD_CLOSE phase; code=0x08 before issue, code=0xFF means close command wait timeout." >&2
elif [[ "$err_op" -eq 0x69 ]]; then
  echo "Hint: opcode 0x69 = CMD_CLOSE completed." >&2
elif [[ "$err_op" -eq 0x6A ]]; then
  echo "Hint: opcode 0x6A = CMD_OPEN phase; code=0x01 before issue, code=0xFF means open command wait timeout." >&2
elif [[ "$err_op" -eq 0x6B ]]; then
  echo "Hint: opcode 0x6B = CMD_OPEN completed." >&2
elif [[ "$err_op" -eq 0x6C ]]; then
  printf 'Hint: opcode 0x6C = Sn_DPORT readback; code=high byte, detail=low byte. Value=0x%02X%02X (%d). Expected 0x075B (1883).\n' "$err_code" "$err_detail" "$(( (err_code << 8) | err_detail ))"
  if [[ "$(( (err_code << 8) | err_detail ))" -eq 1883 ]]; then
    echo "  DPORT: OK (1883)"
  else
    printf '  DPORT: WRONG! Got %d, expected 1883\n' "$(( (err_code << 8) | err_detail ))"
  fi
elif [[ "$err_op" -eq 0x6D ]]; then
  printf 'Hint: opcode 0x6D = Sn_DIPR[2:3] readback; code=DIPR[2], detail=DIPR[3]. Got %d.%d. Expected 132.50 for 172.17.132.50.\n' "$err_code" "$err_detail"
  if [[ "$err_code" -eq 132 && "$err_detail" -eq 50 ]]; then
    echo "  DIPR[2:3]: OK (132.50)"
  else
    printf '  DIPR[2:3]: WRONG! Got %d.%d, expected 132.50\n' "$err_code" "$err_detail"
  fi
fi

# Decode first-link-up latch.
latch_dec=$((link_latch_hex))
latch_magic=$(((latch_dec >> 24) & 0xFF))
latch_phycfgr=$(((latch_dec >> 8) & 0xFF))
printf '\nFirst-link-up latch raw: %s\n' "$link_latch_hex"
if [[ "$latch_dec" -eq 0 ]]; then
  echo '  Status: NEVER SEEN -- LNK=1 has not been observed since last reset.'
elif [[ "$latch_magic" -eq 0xD6 ]]; then
  echo '  Status: LATCHED -- LNK=1 was observed at least once since last reset.'
  printf '  PHYCFGR at first link-up: 0x%02X\n' "$latch_phycfgr"
  decode_phycfgr "$latch_phycfgr"
else
  printf '  Status: unexpected magic 0x%02X (expected 0xD6); latch may be uninitialized.\n' "$latch_magic"
fi

# Decode connect-params latch.
cp_dec=$((connect_params_hex))
cp_magic=$(((cp_dec >> 24) & 0xFF))
cp_dipr2=$(((cp_dec >> 16) & 0xFF))
cp_dipr3=$(((cp_dec >> 8) & 0xFF))
cp_dport_lo=$((cp_dec & 0xFF))
printf '\nConnect-params latch raw: %s\n' "$connect_params_hex"
if [[ "$cp_dec" -eq 0 ]]; then
  echo '  Status: NOT SET -- connect has not been attempted yet.'
elif [[ "$cp_magic" -eq 0xCC ]]; then
  printf '  DIPR[2:3]: %d.%d\n' "$cp_dipr2" "$cp_dipr3"
  printf '  DPORT low byte: 0x%02X (%d)\n' "$cp_dport_lo" "$cp_dport_lo"
  if [[ "$cp_dipr2" -eq 132 && "$cp_dipr3" -eq 50 ]]; then
    echo '  DIPR[2:3]: OK (132.50 = ...132.50)'
  else
    printf '  DIPR[2:3]: WRONG! Got %d.%d, expected 132.50\n' "$cp_dipr2" "$cp_dipr3"
  fi
  if [[ "$cp_dport_lo" -eq 91 ]]; then
    echo '  DPORT low: OK (0x5B = low byte of 1883)'
  else
    printf '  DPORT low: WRONG! Got 0x%02X, expected 0x5B (low byte of 1883)\n' "$cp_dport_lo"
  fi
else
  printf '  Status: unexpected magic 0x%02X (expected 0xCC); latch may be uninitialized.\n' "$cp_magic"
fi

# Decode post-CMD_CONNECT Sn_SR snapshot.
pc_dec=$((post_connect_hex))
pc_magic=$(((pc_dec >> 24) & 0xFF))
pc_attempt=$(((pc_dec >> 16) & 0xFF))
pc_sr=$(((pc_dec >> 8) & 0xFF))
pc_ir=$((pc_dec & 0xFF))
printf '\nPost-CMD_CONNECT snapshot raw: %s\n' "$post_connect_hex"
if [[ "$pc_dec" -eq 0 ]]; then
  echo '  Status: NOT SET -- CMD_CONNECT has not been reached yet.'
elif [[ "$pc_magic" -eq 0xCE ]]; then
  printf '  Attempt count: %d\n' "$pc_attempt"
  printf '  Sn_SR at ~50ms: 0x%02X' "$pc_sr"
  case "$pc_sr" in
    0x00|0) echo ' (SOCK_CLOSED -- CMD_CONNECT failed silently; SYN was NOT sent)' ;;
    0x15|21) echo ' (SOCK_SYNSENT -- SYN was emitted, waiting for SYN-ACK) *** GOOD ***' ;;
    0x17|23) echo ' (SOCK_ESTABLISHED -- connected!)' ;;
    0x18|24) echo ' (SOCK_CLOSE_WAIT)' ;;
    0x1C|28) echo ' (SOCK_FIN_WAIT)' ;;
    *) printf ' (unexpected state)\n' ;;
  esac
  printf '  Sn_IR at ~50ms: 0x%02X' "$pc_ir"
  [[ $((pc_ir & 0x08)) -ne 0 ]] && printf ' [TIMEOUT]'
  [[ $((pc_ir & 0x04)) -ne 0 ]] && printf ' [DISCON]'
  [[ $((pc_ir & 0x01)) -ne 0 ]] && printf ' [CON]'
  echo
else
  printf '  Status: unexpected magic 0x%02X (expected 0xCE).\n' "$pc_magic"
fi

