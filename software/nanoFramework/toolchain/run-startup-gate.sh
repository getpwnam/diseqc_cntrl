#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
OUT_FILE="$ROOT_DIR/.debug/gdb_startup_gate.out"
TIMEOUT_SECS="${TIMEOUT_SECS:-15}"

PROBE_CREATE_CMD="$ROOT_DIR/.debug/gdb_probe_createinstance_once.cmd"
PROBE_RESOLVE_CMD="$ROOT_DIR/.debug/gdb_probe_resolveall_once.cmd"
PROBE_AFTER_CMD="$ROOT_DIR/.debug/gdb_probe_after_resolveall.cmd"
PROBE_PREPARE_CMD="$ROOT_DIR/.debug/gdb_probe_prepareforexec_once.cmd"
PROBE_NEWTHREAD_CMD="$ROOT_DIR/.debug/gdb_probe_newthread_once.cmd"
PROBE_EXECUTE_CMD="$ROOT_DIR/.debug/gdb_probe_execute_il_once.cmd"
PROBE_NATIVE_CMD="$ROOT_DIR/.debug/gdb_probe_nativewrite_once.cmd"

for f in \
  "$PROBE_CREATE_CMD" \
  "$PROBE_RESOLVE_CMD" \
  "$PROBE_AFTER_CMD" \
  "$PROBE_PREPARE_CMD" \
  "$PROBE_NEWTHREAD_CMD" \
  "$PROBE_EXECUTE_CMD" \
  "$PROBE_NATIVE_CMD"
do
  if [[ ! -f "$f" ]]; then
    echo "ERROR: missing probe file: $f" >&2
    exit 1
  fi
done

run_probe() {
  local cmd_file="$1"
  local out_file="$2"

  set +e
  timeout "$TIMEOUT_SECS" gdb-multiarch -batch -x "$cmd_file" "$ROOT_DIR/build/nanoCLR.elf" > "$out_file" 2>&1
  local rc=$?
  set -e

  if [[ $rc -ne 0 && $rc -ne 124 ]]; then
    echo "WARN: probe failed rc=$rc ($cmd_file)" >&2
  fi
}

TMP_CREATE="$ROOT_DIR/.debug/gate_create.out"
TMP_RESOLVE="$ROOT_DIR/.debug/gate_resolve.out"
TMP_AFTER="$ROOT_DIR/.debug/gate_after.out"
TMP_PREPARE="$ROOT_DIR/.debug/gate_prepare.out"
TMP_NEWTHREAD="$ROOT_DIR/.debug/gate_newthread.out"
TMP_EXECUTE="$ROOT_DIR/.debug/gate_execute.out"
TMP_NATIVE="$ROOT_DIR/.debug/gate_native.out"

run_probe "$PROBE_CREATE_CMD" "$TMP_CREATE"
run_probe "$PROBE_RESOLVE_CMD" "$TMP_RESOLVE"
run_probe "$PROBE_AFTER_CMD" "$TMP_AFTER"
run_probe "$PROBE_PREPARE_CMD" "$TMP_PREPARE"
run_probe "$PROBE_NEWTHREAD_CMD" "$TMP_NEWTHREAD"
run_probe "$PROBE_EXECUTE_CMD" "$TMP_EXECUTE"
run_probe "$PROBE_NATIVE_CMD" "$TMP_NATIVE"

CREATE_HIT=0
RESOLVE_HIT=0
AFTER_HIT=0
PREPARE_HIT=0
NEWTHREAD_HIT=0
EXECUTE_HIT=0
NATIVE_HIT=0
RESOLVE_HR="UNKNOWN"

grep -q 'CREATEINSTANCE_HIT' "$TMP_CREATE" && CREATE_HIT=1 || true
grep -q 'RESOLVEALL_HIT' "$TMP_RESOLVE" && RESOLVE_HIT=1 || true
grep -q 'AFTER_RESOLVEALL' "$TMP_AFTER" && AFTER_HIT=1 || true
grep -q 'PREPAREFOREXEC_HIT' "$TMP_PREPARE" && PREPARE_HIT=1 || true
grep -q 'NEWTHREAD_HIT' "$TMP_NEWTHREAD" && NEWTHREAD_HIT=1 || true
grep -q 'EXECUTE_IL_HIT' "$TMP_EXECUTE" && EXECUTE_HIT=1 || true
grep -q 'NATIVE_WRITE_HIT' "$TMP_NATIVE" && NATIVE_HIT=1 || true

if grep -q 'AFTER_RESOLVEALL' "$TMP_AFTER"; then
  RESOLVE_HR="$(awk '/AFTER_RESOLVEALL/{for(i=1;i<=NF;i++){if($i ~ /^0x[0-9a-fA-F]+$/){last=$i}}} END{if(last!="") print last; else print "UNKNOWN"}' "$TMP_AFTER")"
fi

RESOLVE_OK=0
if [[ "$RESOLVE_HR" =~ ^0x[0-9a-fA-F]+$ ]]; then
  if (( RESOLVE_HR == 0 )); then
    RESOLVE_OK=1
  fi
fi

{
  echo "GATE_CREATEINSTANCE=$CREATE_HIT"
  echo "GATE_RESOLVEALL=$RESOLVE_HIT"
  echo "GATE_AFTER_RESOLVEALL=$AFTER_HIT"
  echo "GATE_RESOLVEALL_HR=$RESOLVE_HR"
  echo "GATE_PREPAREFOREXEC=$PREPARE_HIT"
  echo "GATE_NEWTHREAD=$NEWTHREAD_HIT"
  echo "GATE_EXECUTE_IL=$EXECUTE_HIT"
  echo "GATE_NATIVEWRITE=$NATIVE_HIT"
} > "$OUT_FILE"

echo "WROTE: $OUT_FILE"
cat "$OUT_FILE"

if [[ "$CREATE_HIT" -eq 1 && "$RESOLVE_HIT" -eq 1 && "$AFTER_HIT" -eq 1 && "$RESOLVE_OK" -eq 1 && "$PREPARE_HIT" -eq 1 && "$NEWTHREAD_HIT" -eq 1 && "$EXECUTE_HIT" -eq 1 && "$NATIVE_HIT" -eq 1 ]]; then
  echo "GATE_STATUS=PASS"
  exit 0
fi

echo "GATE_STATUS=FAIL"
  exit 1
