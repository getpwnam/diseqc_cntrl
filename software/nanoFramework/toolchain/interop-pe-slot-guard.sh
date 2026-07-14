#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PE_PATH="${1:-}"
INTEROP_GUARD_TOOL="$SCRIPT_DIR/interop-guard.sh"

usage() {
  cat <<'EOF'
Usage:
  ./toolchain/interop-pe-slot-guard.sh /path/to/<assembly>.pe

Notes:
  - For Cubley.Interop.pe, this enforces managed/native slot-order invariants
    by invoking interop-guard.sh.
  - For other PE names, this script exits successfully (no-op) to avoid
    blocking unrelated workflows.
EOF
}

if [[ -z "$PE_PATH" || "$PE_PATH" == "-h" || "$PE_PATH" == "--help" ]]; then
  usage
  exit 2
fi

if [[ ! -f "$PE_PATH" ]]; then
  echo "PE not found: $PE_PATH" >&2
  exit 1
fi

# Fast sanity check: CLR_RECORD_ASSEMBLY.nativeMethodsChecksum must exist.
python3 - <<'PYEOF' "$PE_PATH"
import struct
import sys

pe_path = sys.argv[1]
with open(pe_path, 'rb') as f:
    data = f.read()
if len(data) < 24:
    raise SystemExit(f"PE too short for nativeMethodsChecksum: {pe_path}")
_ = struct.unpack_from('<I', data, 20)[0]
PYEOF

base_name="$(basename "$PE_PATH")"
case "$base_name" in
  Cubley.Interop.pe)
    if [[ ! -x "$INTEROP_GUARD_TOOL" ]]; then
      echo "Missing or non-executable guard tool: $INTEROP_GUARD_TOOL" >&2
      exit 1
    fi
    "$INTEROP_GUARD_TOOL"
    ;;
  *)
    # Unknown assembly flavor: intentionally do not fail.
    ;;
esac
