#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <elf_path> <break_addr_hex> [label] [timeout_s] [attempts]" >&2
  exit 1
fi

ELF_PATH="$1"
BREAK_ADDR="$2"
LABEL="${3:-BP}"
TIMEOUT_S="${4:-18}"
ATTEMPTS="${5:-${PROBE_ATTEMPTS:-8}}"
OPENOCD_BIN="${OPENOCD_BIN:-openocd}"
GDB_BIN="${GDB_BIN:-gdb-multiarch}"

if [[ ! -f "$ELF_PATH" ]]; then
  echo "ELF not found: $ELF_PATH" >&2
  exit 1
fi

for cmd in "$OPENOCD_BIN" "$GDB_BIN"; do
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "Required command not found: $cmd" >&2
    exit 1
  fi
done

tmp_dir="$(mktemp -d /tmp/one-bp-XXXXXX)"
cleanup() {
  pkill -f "openocd -f interface/stlink.cfg -f target/stm32f4x.cfg" >/dev/null 2>&1 || true
  rm -rf "$tmp_dir"
}
trap cleanup EXIT

gdb_script="$tmp_dir/probe.gdb"
gdb_out="$tmp_dir/gdb.out"
openocd_log="$tmp_dir/openocd.log"

cat > "$gdb_script" <<EOF
set pagination off
set confirm off
target extended-remote :3333
hb *$BREAK_ADDR
commands
silent
printf "$LABEL=HIT\\n"
quit
end
monitor reset halt
monitor reset init
continue
EOF

pkill -f "openocd -f interface/stlink.cfg -f target/stm32f4x.cfg" >/dev/null 2>&1 || true
"$OPENOCD_BIN" -f interface/stlink.cfg -f target/stm32f4x.cfg >"$openocd_log" 2>&1 &
openocd_pid=$!
sleep 1.5

rc=1
for _ in $(seq 1 "$ATTEMPTS"); do
  set +e
  timeout "$TIMEOUT_S" "$GDB_BIN" "$ELF_PATH" -q -batch -x "$gdb_script" >"$gdb_out" 2>&1
  rc=$?
  set -e

  if grep -q "$LABEL=HIT" "$gdb_out"; then
    break
  fi
done

kill "$openocd_pid" >/dev/null 2>&1 || true

if grep -q "$LABEL=HIT" "$gdb_out"; then
  echo "$LABEL=HIT"
  sed -n '1,80p' "$gdb_out"
  exit 0
fi

echo "$LABEL=MISS rc=$rc"
sed -n '1,120p' "$gdb_out"
exit 1
