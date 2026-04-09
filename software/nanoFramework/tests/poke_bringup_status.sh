#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NF_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ELF_PATH="${1:-$NF_ROOT/build/nanoCLR.elf}"
VALUE_HEX="${2:-0xD50F0100}"
GDB_BIN="${GDB_BIN:-gdb-multiarch}"
OPENOCD_BIN="${OPENOCD_BIN:-openocd}"

tmp_dir="$(mktemp -d /tmp/poke-mailbox-XXXXXX)"
trap 'rm -rf "$tmp_dir"' EXIT

gdb_script="$tmp_dir/poke.gdb"
openocd_log="$tmp_dir/openocd.log"

cat > "$gdb_script" <<EOF_GDB
set pagination off
set confirm off
target extended-remote :3333
monitor halt
set {unsigned int}0x20005c78 = $VALUE_HEX
x/wx 0x20005c78
quit
EOF_GDB

$OPENOCD_BIN -f interface/stlink.cfg -f target/stm32f4x.cfg >"$openocd_log" 2>&1 &
openocd_pid=$!
trap 'kill "$openocd_pid" >/dev/null 2>&1 || true; rm -rf "$tmp_dir"' EXIT

"$GDB_BIN" "$ELF_PATH" -q -batch -x "$gdb_script"
kill "$openocd_pid" >/dev/null 2>&1 || true
