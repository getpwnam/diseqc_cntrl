#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
ELF_PATH="${1:-$ROOT_DIR/build/nanoCLR.elf}"
OPENOCD_BIN="${OPENOCD_BIN:-openocd}"
GDB_BIN="${GDB_BIN:-gdb-multiarch}"
RUN_SECS="${RUN_SECS:-8}"
OUT_FILE="${OUT_FILE:-$ROOT_DIR/.debug/clr_hang_probe.out}"

if [[ ! -f "$ELF_PATH" ]]; then
  echo "ELF not found: $ELF_PATH" >&2
  exit 1
fi

for cmd in "$OPENOCD_BIN" "$GDB_BIN" arm-none-eabi-nm; do
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "Required command not found: $cmd" >&2
    exit 1
  fi
done

mkdir -p "$(dirname "$OUT_FILE")"

tmp_dir="$(mktemp -d /tmp/clr-hang-probe-XXXXXX)"
cleanup() {
  pkill -f "openocd -f interface/stlink.cfg -f target/stm32f4x.cfg" >/dev/null 2>&1 || true
  rm -rf "$tmp_dir"
}
trap cleanup EXIT

resolve_addr() {
  local pattern="$1"
  set +o pipefail
  arm-none-eabi-nm -n "$ELF_PATH" | c++filt | awk -v pat="$pattern" '$0 ~ pat { print "0x" $1; exit }'
  set -o pipefail
}

CLRSTARTUP_ADDR="$(resolve_addr ' ClrStartup$')"
RESOLVEALL_ADDR="$(resolve_addr 'CLR_RT_TypeSystem::ResolveAll')"
PREPARE_ADDR="$(resolve_addr 'CLR_RT_TypeSystem::PrepareForExecution')"
NEWTHREAD_ADDR="$(resolve_addr 'CLR_RT_ExecutionEngine::NewThread')"
EXECUTEIL_ADDR="$(resolve_addr 'CLR_RT_Thread::Execute_IL')"
MAILBOXSET_ADDR="$(resolve_addr 'Library_diseqc_interop_DiseqC_NativeSetBringupStatus___STATIC__VOID__U4')"
MAILBOXGET_ADDR="$(resolve_addr 'Library_diseqc_interop_DiseqC_NativeGetBringupStatus___STATIC__U4')"

if [[ -z "$CLRSTARTUP_ADDR" ]]; then
  echo "Unable to locate ClrStartup symbol." >&2
  exit 1
fi

gdb_script="$tmp_dir/probe.gdb"
gdb_out="$tmp_dir/probe.out"
openocd_log="$tmp_dir/openocd.log"

cat > "$gdb_script" <<EOF_GDB
set pagination off
set confirm off
set print thread-events off

set \$hit_clrstartup = 0
set \$hit_resolveall = 0
set \$hit_prepare = 0
set \$hit_newthread = 0
set \$hit_mailboxset = 0
set \$hit_mailboxget = 0

target extended-remote :3333

hb *$CLRSTARTUP_ADDR
commands
silent
set \$hit_clrstartup = \$hit_clrstartup + 1
continue
end
EOF_GDB

if [[ -n "$RESOLVEALL_ADDR" ]]; then
cat >> "$gdb_script" <<EOF_GDB
hb *$RESOLVEALL_ADDR
commands
silent
set \$hit_resolveall = \$hit_resolveall + 1
continue
end
EOF_GDB
fi

if [[ -n "$PREPARE_ADDR" ]]; then
cat >> "$gdb_script" <<EOF_GDB
hb *$PREPARE_ADDR
commands
silent
set \$hit_prepare = \$hit_prepare + 1
continue
end
EOF_GDB
fi

if [[ -n "$NEWTHREAD_ADDR" ]]; then
cat >> "$gdb_script" <<EOF_GDB
hb *$NEWTHREAD_ADDR
commands
silent
set \$hit_newthread = \$hit_newthread + 1
continue
end
EOF_GDB
fi

if [[ -n "$MAILBOXSET_ADDR" ]]; then
cat >> "$gdb_script" <<EOF_GDB
hb *$MAILBOXSET_ADDR
commands
silent
set \$hit_mailboxset = \$hit_mailboxset + 1
continue
end
EOF_GDB
fi

if [[ -n "$MAILBOXGET_ADDR" ]]; then
cat >> "$gdb_script" <<EOF_GDB
hb *$MAILBOXGET_ADDR
commands
silent
set \$hit_mailboxget = \$hit_mailboxget + 1
continue
end
EOF_GDB
fi

cat >> "$gdb_script" <<EOF_GDB
monitor reset init
continue
shell sleep $RUN_SECS
interrupt

printf "HIT_CLRSTARTUP=%d\\n", \$hit_clrstartup
printf "HIT_RESOLVEALL=%d\\n", \$hit_resolveall
printf "HIT_PREPARE=%d\\n", \$hit_prepare
printf "HIT_NEWTHREAD=%d\\n", \$hit_newthread
printf "HIT_MAILBOXSET=%d\\n", \$hit_mailboxset
printf "HIT_MAILBOXGET=%d\\n", \$hit_mailboxget

printf "FINAL_PC=0x%08x\\n", \$pc
info symbol \$pc
bt 12
x/16i \$pc-16
quit
EOF_GDB

pkill -f "openocd -f interface/stlink.cfg -f target/stm32f4x.cfg" >/dev/null 2>&1 || true
"$OPENOCD_BIN" -f interface/stlink.cfg -f target/stm32f4x.cfg >"$openocd_log" 2>&1 &
openocd_pid=$!

# Give openocd a moment to bind ports.
sleep 0.5

set +e
"$GDB_BIN" "$ELF_PATH" -q -batch -x "$gdb_script" >"$gdb_out" 2>&1
rc=$?
set -e

kill "$openocd_pid" >/dev/null 2>&1 || true

{
  echo "ELF=$ELF_PATH"
  echo "RUN_SECS=$RUN_SECS"
  echo "CLRSTARTUP_ADDR=${CLRSTARTUP_ADDR:-MISSING}"
  echo "RESOLVEALL_ADDR=${RESOLVEALL_ADDR:-MISSING}"
  echo "PREPARE_ADDR=${PREPARE_ADDR:-MISSING}"
  echo "NEWTHREAD_ADDR=${NEWTHREAD_ADDR:-MISSING}"
  echo "EXECUTEIL_ADDR=${EXECUTEIL_ADDR:-MISSING}"
  echo "MAILBOXSET_ADDR=${MAILBOXSET_ADDR:-MISSING}"
  echo "MAILBOXGET_ADDR=${MAILBOXGET_ADDR:-MISSING}"
  echo "GDB_RC=$rc"
  echo "----- GDB OUTPUT -----"
  cat "$gdb_out"
  echo "----- OPENOCD (tail) -----"
  tail -n 40 "$openocd_log" || true
} > "$OUT_FILE"

cat "$OUT_FILE"
