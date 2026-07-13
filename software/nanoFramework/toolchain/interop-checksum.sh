#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

MODE="check"
ASSEMBLY_NAME="Cubley.Interop"
PE_PATH=""

CUBLEY_ASSEMBLY_INFO_PATH="$ROOT_DIR/Cubley.Interop/Properties/AssemblyInfo.cs"
CUBLEY_NATIVE_INTEROP_PATH="$ROOT_DIR/nf-native/cubley_interop.cpp"
CUBLEY_NATIVE_SYMBOL="g_CLR_AssemblyNative_Cubley_Interop"
CUBLEY_DEFAULT_PE_PATH="$ROOT_DIR/build/DiSEqC_Control/Cubley.Interop.pe"

SMOKE_ASSEMBLY_INFO_PATH="$ROOT_DIR/SmokeW5500.Interop/Properties/AssemblyInfo.cs"
SMOKE_NATIVE_INTEROP_PATH="$ROOT_DIR/nf-native/smoke_w5500_interop.cpp"
SMOKE_NATIVE_SYMBOL="g_CLR_AssemblyNative_SmokeW5500_Interop"
SMOKE_DEFAULT_PE_PATH="$ROOT_DIR/build/CubleySmokeTier2_W5500/SmokeW5500.Interop.pe"

ASSEMBLY_INFO_PATH=""
NATIVE_INTEROP_PATH=""
NATIVE_SYMBOL=""
DEFAULT_PE_PATH=""

usage() {
  cat <<'EOF'
Usage:
  ./toolchain/interop-checksum.sh [--check|--fix] [--assembly <name>] [--pe /path/to/<assembly>.pe]

Modes:
  --check    Verify checksums are aligned. Fails on mismatch. (default)
  --fix      Read checksum from PE and update managed + native source values.

Assemblies:
  Cubley.Interop
  SmokeW5500.Interop

Notes:
  - PE checksum is read from CLR_RECORD_ASSEMBLY.nativeMethodsChecksum (offset 20).
  - In --check mode, if PE exists it is also compared against source values.
EOF
}

set_targets() {
  case "$ASSEMBLY_NAME" in
    Cubley.Interop)
      ASSEMBLY_INFO_PATH="$CUBLEY_ASSEMBLY_INFO_PATH"
      NATIVE_INTEROP_PATH="$CUBLEY_NATIVE_INTEROP_PATH"
      NATIVE_SYMBOL="$CUBLEY_NATIVE_SYMBOL"
      DEFAULT_PE_PATH="$CUBLEY_DEFAULT_PE_PATH"
      ;;
    SmokeW5500.Interop)
      ASSEMBLY_INFO_PATH="$SMOKE_ASSEMBLY_INFO_PATH"
      NATIVE_INTEROP_PATH="$SMOKE_NATIVE_INTEROP_PATH"
      NATIVE_SYMBOL="$SMOKE_NATIVE_SYMBOL"
      DEFAULT_PE_PATH="$SMOKE_DEFAULT_PE_PATH"
      ;;
    *)
      echo "Unsupported assembly '$ASSEMBLY_NAME'." >&2
      echo "Supported: Cubley.Interop, SmokeW5500.Interop" >&2
      exit 2
      ;;
  esac
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --check)
      MODE="check"
      shift
      ;;
    --fix)
      MODE="fix"
      shift
      ;;
    --assembly)
      ASSEMBLY_NAME="${2:-}"
      shift 2
      ;;
    --pe)
      PE_PATH="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 2
      ;;
  esac
done

set_targets

if [[ -z "$PE_PATH" ]]; then
  PE_PATH="$DEFAULT_PE_PATH"
fi

for required_file in "$ASSEMBLY_INFO_PATH" "$NATIVE_INTEROP_PATH"; do
  if [[ ! -f "$required_file" ]]; then
    echo "Missing file: $required_file" >&2
    exit 1
  fi
done

extract_cs_checksum() {
  sed -n 's/.*AssemblyNativeVersion("\([0-9A-Fa-f]\{8\}\)").*/\1/p' "$ASSEMBLY_INFO_PATH" | head -n1 | tr '[:lower:]' '[:upper:]'
}

extract_native_checksum() {
  sed -n "/$NATIVE_SYMBOL/,/};/p" "$NATIVE_INTEROP_PATH" \
    | grep -o '0x[0-9A-Fa-f]\{8\}' \
    | head -n1 \
    | cut -c3- \
    | tr '[:lower:]' '[:upper:]'
}

extract_pe_checksum() {
  local pe="$1"
  python3 - <<'PYEOF' "$pe"
import struct
import sys

pe_path = sys.argv[1]
with open(pe_path, 'rb') as f:
    data = f.read()

if len(data) < 24:
    raise SystemExit("PE file too short to contain nativeMethodsChecksum")

val = struct.unpack_from('<I', data, 20)[0]
print(f"{val:08X}")
PYEOF
}

assert_native_version_scope() {
  local offenders
  offenders="$(grep -R -n --include='AssemblyInfo.cs' -F 'AssemblyNativeVersion("' "$ROOT_DIR" \
    | cut -d: -f1 \
    | sort -u \
    | grep -Ev "^$CUBLEY_ASSEMBLY_INFO_PATH$|^$SMOKE_ASSEMBLY_INFO_PATH$" || true)"

  if [[ -n "$offenders" ]]; then
    echo "Invalid AssemblyNativeVersion usage detected outside allowed interop assemblies:" >&2
    while IFS= read -r offender; do
      [[ -n "$offender" ]] || continue
      echo "  - $offender" >&2
    done <<< "$offenders"
    echo "Allowed files:" >&2
    echo "  - $CUBLEY_ASSEMBLY_INFO_PATH" >&2
    echo "  - $SMOKE_ASSEMBLY_INFO_PATH" >&2
    exit 1
  fi
}

assert_native_version_scope

CS_SUM="$(extract_cs_checksum)"
NATIVE_SUM="$(extract_native_checksum)"

if [[ -z "$CS_SUM" ]]; then
  echo "Unable to parse AssemblyNativeVersion from $ASSEMBLY_INFO_PATH" >&2
  exit 1
fi

if [[ -z "$NATIVE_SUM" ]]; then
  echo "Unable to parse $NATIVE_SYMBOL checksum from $NATIVE_INTEROP_PATH" >&2
  exit 1
fi

if [[ "$MODE" == "fix" ]]; then
  if [[ ! -f "$PE_PATH" ]]; then
    echo "PE file not found: $PE_PATH" >&2
    echo "Build $ASSEMBLY_NAME first, then rerun with --fix." >&2
    exit 1
  fi

  PE_SUM="$(extract_pe_checksum "$PE_PATH")"

  python3 - <<'PYEOF' "$ASSEMBLY_INFO_PATH" "$NATIVE_INTEROP_PATH" "$NATIVE_SYMBOL" "$PE_SUM"
import re
import sys

assembly_info_path, native_path, native_symbol, pe_sum = sys.argv[1:5]

with open(assembly_info_path, 'r', encoding='utf-8') as f:
    cs_text = f.read()

if 'AssemblyNativeVersion(' in cs_text:
    cs_text, cs_count = re.subn(r'AssemblyNativeVersion\("[0-9A-Fa-f]{8}"\)', f'AssemblyNativeVersion("{pe_sum}")', cs_text, count=1)
else:
    if not cs_text.endswith('\n'):
        cs_text += '\n'
    cs_text += f'[assembly: AssemblyNativeVersion("{pe_sum}")]\n'
    cs_count = 1

if cs_count != 1:
    raise SystemExit(f"Failed to update AssemblyNativeVersion in {assembly_info_path}")

with open(assembly_info_path, 'w', encoding='utf-8') as f:
    f.write(cs_text)

with open(native_path, 'r', encoding='utf-8') as f:
    native_text = f.read()

start = native_text.find(native_symbol)
if start < 0:
    raise SystemExit(f"Could not find symbol {native_symbol} in {native_path}")

open_brace = native_text.find('{', start)
close_brace = native_text.find('};', open_brace)
if open_brace < 0 or close_brace < 0:
    raise SystemExit(f"Could not locate initializer block for {native_symbol} in {native_path}")

block = native_text[open_brace:close_brace]
block_new, block_count = re.subn(r'0x[0-9A-Fa-f]{8}', f'0x{pe_sum}', block, count=1)
if block_count != 1:
    raise SystemExit(f"Could not update checksum constant in {native_path}")

native_text = native_text[:open_brace] + block_new + native_text[close_brace:]

with open(native_path, 'w', encoding='utf-8') as f:
    f.write(native_text)
PYEOF

  echo "Updated checksums to $PE_SUM for $ASSEMBLY_NAME"
  echo "  - $ASSEMBLY_INFO_PATH"
  echo "  - $NATIVE_INTEROP_PATH"
  exit 0
fi

if [[ "$CS_SUM" != "$NATIVE_SUM" ]]; then
  echo "Checksum mismatch between source files ($ASSEMBLY_NAME):" >&2
  echo "  AssemblyInfo: $CS_SUM" >&2
  echo "  native table: $NATIVE_SUM" >&2
  exit 1
fi

if [[ -f "$PE_PATH" ]]; then
  PE_SUM="$(extract_pe_checksum "$PE_PATH")"
  if [[ "$PE_SUM" != "$CS_SUM" ]]; then
    echo "Checksum mismatch between PE and source ($ASSEMBLY_NAME):" >&2
    echo "  PE: $PE_SUM ($PE_PATH)" >&2
    echo "  source: $CS_SUM" >&2
    echo "Run: ./toolchain/interop-checksum.sh --fix --assembly $ASSEMBLY_NAME --pe $PE_PATH" >&2
    exit 1
  fi
fi

echo "Checksum OK ($ASSEMBLY_NAME): $CS_SUM"
