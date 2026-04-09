#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
ELF_PATH="${1:-$ROOT_DIR/build/nanoCLR.elf}"
OPENOCD_BIN="${OPENOCD_BIN:-openocd}"
GDB_BIN="${GDB_BIN:-gdb-multiarch}"
OUT_FILE="${OUT_FILE:-$ROOT_DIR/.debug/clr_startup_probe.out}"
TIMEOUT_SECS="${TIMEOUT_SECS:-12}"

if [[ ! -f "$ELF_PATH" ]]; then
  echo "ELF not found: $ELF_PATH" >&2
  exit 1
fi

if ! command -v "$OPENOCD_BIN" >/dev/null 2>&1; then
  echo "openocd not found" >&2
  exit 1
fi

if ! command -v "$GDB_BIN" >/dev/null 2>&1; then
  echo "gdb-multiarch not found (override with GDB_BIN)" >&2
  exit 1
fi

if ! command -v arm-none-eabi-nm >/dev/null 2>&1; then
  echo "arm-none-eabi-nm not found" >&2
  exit 1
fi

mkdir -p "$(dirname "$OUT_FILE")"

tmp_dir="$(mktemp -d /tmp/clr-startup-probe-XXXXXX)"
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
CREATEINSTANCE_ADDR="$(resolve_addr 'CLR_RT_Assembly::CreateInstance')"
RESOLVEALL_ADDR="$(resolve_addr 'CLR_RT_TypeSystem::ResolveAll')"
PREPARE_ADDR="$(resolve_addr 'CLR_RT_TypeSystem::PrepareForExecution')"
NEWTHREAD_ADDR="$(resolve_addr 'CLR_RT_ExecutionEngine::NewThread')"
EXECUTEIL_ADDR="$(resolve_addr 'CLR_RT_Thread::Execute_IL')"
NATIVEWRITE_ADDR="$(resolve_addr 'Library_sys_dev_gpio_native_System_Device_Gpio_GpioController::NativeWrite___VOID__I4__U1')"
MAILBOXSET_ADDR="$(resolve_addr 'Library_diseqc_interop_DiseqC_NativeSetBringupStatus___STATIC__VOID__U4')"
MAILBOXGET_ADDR="$(resolve_addr 'Library_diseqc_interop_DiseqC_NativeGetBringupStatus___STATIC__U4')"
W5500OPEN_ADDR="$(resolve_addr 'Library_diseqc_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4')"
W5500CLOSE_ADDR="$(resolve_addr 'Library_diseqc_interop_W5500Socket_NativeClose___STATIC__I4__I4')"

probe_one() {
  local label="$1"
  local addr="$2"
  local gdb_script="$tmp_dir/$label.gdb"
  local gdb_out="$tmp_dir/$label.out"
  local openocd_log="$tmp_dir/$label.openocd.log"

  if [[ -z "$addr" ]]; then
    echo "$label=ADDR_NOT_FOUND" >> "$OUT_FILE"
    return 0
  fi

  cat > "$gdb_script" <<EOF_GDB
set pagination off
set confirm off
target extended-remote :3333
hb *$addr
commands
silent
printf "$label=HIT\\n"
quit
end
monitor reset init
continue
EOF_GDB

  pkill -f "openocd -f interface/stlink.cfg -f target/stm32f4x.cfg" >/dev/null 2>&1 || true
  "$OPENOCD_BIN" -f interface/stlink.cfg -f target/stm32f4x.cfg >"$openocd_log" 2>&1 &
  local openocd_pid=$!

  local rc=1
  local hit=0
  for _ in 1 2 3 4 5 6 7 8 9 10; do
    if timeout "$TIMEOUT_SECS" "$GDB_BIN" "$ELF_PATH" -q -batch -x "$gdb_script" >"$gdb_out" 2>&1; then
      rc=0
      break
    fi
    rc=$?
  done

  kill "$openocd_pid" >/dev/null 2>&1 || true

  if grep -q "$label=HIT" "$gdb_out"; then
    hit=1
  fi

  if [[ "$hit" -eq 1 ]]; then
    echo "$label=HIT" >> "$OUT_FILE"
  else
    echo "$label=MISS rc=$rc" >> "$OUT_FILE"
    echo "--- $label gdb ---" >> "$OUT_FILE"
    tail -n 20 "$gdb_out" >> "$OUT_FILE" 2>/dev/null || true
  fi
}

{
  echo "ELF=$ELF_PATH"
  echo "CLRSTARTUP_ADDR=${CLRSTARTUP_ADDR:-MISSING}"
  echo "CREATEINSTANCE_ADDR=${CREATEINSTANCE_ADDR:-MISSING}"
  echo "RESOLVEALL_ADDR=${RESOLVEALL_ADDR:-MISSING}"
  echo "PREPARE_ADDR=${PREPARE_ADDR:-MISSING}"
  echo "NEWTHREAD_ADDR=${NEWTHREAD_ADDR:-MISSING}"
  echo "EXECUTEIL_ADDR=${EXECUTEIL_ADDR:-MISSING}"
  echo "NATIVEWRITE_ADDR=${NATIVEWRITE_ADDR:-MISSING}"
  echo "MAILBOXSET_ADDR=${MAILBOXSET_ADDR:-MISSING}"
  echo "MAILBOXGET_ADDR=${MAILBOXGET_ADDR:-MISSING}"
  echo "W5500OPEN_ADDR=${W5500OPEN_ADDR:-MISSING}"
  echo "W5500CLOSE_ADDR=${W5500CLOSE_ADDR:-MISSING}"
} > "$OUT_FILE"

probe_one CLRSTARTUP "$CLRSTARTUP_ADDR"
probe_one CREATEINSTANCE "$CREATEINSTANCE_ADDR"
probe_one RESOLVEALL "$RESOLVEALL_ADDR"
probe_one PREPAREFOREXEC "$PREPARE_ADDR"
probe_one NEWTHREAD "$NEWTHREAD_ADDR"
probe_one EXECUTE_IL "$EXECUTEIL_ADDR"
probe_one NATIVE_WRITE "$NATIVEWRITE_ADDR"
probe_one MAILBOX_SET "$MAILBOXSET_ADDR"
probe_one MAILBOX_GET "$MAILBOXGET_ADDR"
probe_one W5500_OPEN "$W5500OPEN_ADDR"
probe_one W5500_CLOSE "$W5500CLOSE_ADDR"

cat "$OUT_FILE"
