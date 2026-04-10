#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

ASSEMBLY_INFO_PATH="$ROOT_DIR/Cubley.Interop/Properties/AssemblyInfo.cs"
NATIVE_INTEROP_PATH="$ROOT_DIR/nf-native/cubley_interop.cpp"
DEFAULT_PE_PATH="$ROOT_DIR/Cubley.Interop/bin/Release/Cubley.Interop.pe"

MODE="check"
PE_PATH="$DEFAULT_PE_PATH"

usage() {
  cat <<'EOF'
Usage:
  ./toolchain/interop-checksum.sh [--check|--fix] [--pe /path/to/Cubley.Interop.pe]

Modes:
  --check  Verify checksums are aligned. Fails on mismatch. (default)
  --fix    Read checksum from PE and update both source files.

Notes:
  - PE checksum is read from CLR_RECORD_ASSEMBLY.nativeMethodsChecksum (offset 20).
  - In --check mode, if PE exists it is also compared against source values.
EOF
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

if [[ ! -f "$ASSEMBLY_INFO_PATH" ]]; then
  echo "Missing file: $ASSEMBLY_INFO_PATH" >&2
  exit 1
fi

if [[ ! -f "$NATIVE_INTEROP_PATH" ]]; then
  echo "Missing file: $NATIVE_INTEROP_PATH" >&2
  exit 1
fi

extract_cs_checksum() {
  sed -n 's/.*AssemblyNativeVersion("\([0-9A-Fa-f]\{8\}\)").*/\1/p' "$ASSEMBLY_INFO_PATH" | head -n1 | tr '[:lower:]' '[:upper:]'
}

extract_native_checksum() {
  sed -n '/g_CLR_AssemblyNative_Cubley_Interop/,/};/p' "$NATIVE_INTEROP_PATH" \
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

CS_SUM="$(extract_cs_checksum)"
NATIVE_SUM="$(extract_native_checksum)"

if [[ -z "$CS_SUM" ]]; then
  echo "Unable to parse AssemblyNativeVersion from $ASSEMBLY_INFO_PATH" >&2
  exit 1
fi

if [[ -z "$NATIVE_SUM" ]]; then
  echo "Unable to parse g_CLR_AssemblyNative_Cubley_Interop checksum from $NATIVE_INTEROP_PATH" >&2
  exit 1
fi

if [[ "$MODE" == "fix" ]]; then
  if [[ ! -f "$PE_PATH" ]]; then
    echo "PE file not found: $PE_PATH" >&2
    echo "Build Cubley.Interop first, then rerun with --fix." >&2
    exit 1
  fi

  PE_SUM="$(extract_pe_checksum "$PE_PATH")"

  sed -E -i 's/AssemblyNativeVersion\("[0-9A-Fa-f]{8}"\)/AssemblyNativeVersion("'"$PE_SUM"'")/' "$ASSEMBLY_INFO_PATH"
  perl -0777 -i -pe 's/(g_CLR_AssemblyNative_Cubley_Interop\s*=\s*\{\s*"Cubley\.Interop",\s*)0x[0-9A-Fa-f]{8}/$1"0x'"$PE_SUM"'"/se' "$NATIVE_INTEROP_PATH"

  echo "Updated checksums to $PE_SUM"
  echo "  - $ASSEMBLY_INFO_PATH"
  echo "  - $NATIVE_INTEROP_PATH"
  exit 0
fi

if [[ "$CS_SUM" != "$NATIVE_SUM" ]]; then
  echo "Checksum mismatch between source files:" >&2
  echo "  AssemblyInfo: $CS_SUM" >&2
  echo "  cubley_interop.cpp: $NATIVE_SUM" >&2
  exit 1
fi

if [[ -f "$PE_PATH" ]]; then
  PE_SUM="$(extract_pe_checksum "$PE_PATH")"
  if [[ "$PE_SUM" != "$CS_SUM" ]]; then
    echo "Checksum mismatch between PE and source:" >&2
    echo "  PE: $PE_SUM ($PE_PATH)" >&2
    echo "  source: $CS_SUM" >&2
    echo "Run: ./toolchain/interop-checksum.sh --fix --pe $PE_PATH" >&2
    exit 1
  fi
fi

echo "Checksum OK: $CS_SUM"
