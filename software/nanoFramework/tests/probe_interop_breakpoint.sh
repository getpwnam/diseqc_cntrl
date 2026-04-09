#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NF_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ELF_PATH="${1:-$NF_ROOT/build/nanoCLR.elf}"
BREAK_ADDR="${2:-}"
LABEL="${3:-HIT}"
GDB_BIN="${GDB_BIN:-gdb-multiarch}"
OPENOCD_BIN="${OPENOCD_BIN:-openocd}"

if [[ ! -f "$ELF_PATH" ]]; then
  echo "ELF not found: $ELF_PATH" >&2
  exit 1
fi

if [[ -z "$BREAK_ADDR" ]]; then
  echo "Usage: $0 <elf-path> <break-address> [label]" >&2
  exit 2
fi

tmp_dir="$(mktemp -d /tmp/probe-interop-bp-XXXXXX)"
trap 'rm -rf "$tmp_dir"' EXIT

gdb_script="$tmp_dir/probe.gdb"
openocd_log="$tmp_dir/openocd.log"
gdb_out="$tmp_dir/gdb.out"

cat > "$gdb_script" <<EOF_GDB
set pagination off
set confirm off
target extended-remote :3333
monitor reset init
break *$BREAK_ADDR
commands
silent
printf "$LABEL\\n"
quit
end
continue
EOF_GDB

$OPENOCD_BIN -f interface/stlink.cfg -f target/stm32f4x.cfg >"$openocd_log" 2>&1 &
openocd_pid=$!
trap 'kill "$openocd_pid" >/dev/null 2>&1 || true; rm -rf "$tmp_dir"' EXIT

if timeout 20s "$GDB_BIN" "$ELF_PATH" -q -batch -x "$gdb_script" >"$gdb_out" 2>&1; then
  rc=0
else
  rc=$?
fi

kill "$openocd_pid" >/dev/null 2>&1 || true

echo "RC:$rc"
echo "--- gdb ---"
cat "$gdb_out"
echo "--- openocd (tail) ---"
tail -n 40 "$openocd_log" || true

exit "$rc"
