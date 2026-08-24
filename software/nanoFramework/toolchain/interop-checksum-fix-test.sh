#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SOURCE_NATIVE="$ROOT_DIR/../../firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/cubley_interop.cpp"
SOURCE_ASSEMBLY_INFO="$ROOT_DIR/CubleyNative.Interop/Properties/AssemblyInfo.cs"

for required_file in "$SOURCE_NATIVE" "$SOURCE_ASSEMBLY_INFO"; do
  if [[ ! -f "$required_file" ]]; then
    echo "Missing fixture source: $required_file" >&2
    exit 1
  fi
done

TMP_DIR="$(mktemp -d /tmp/interop-checksum-fix-XXXXXX)"
cleanup() {
  chmod -R u+w "$TMP_DIR" 2>/dev/null || true
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

FIXTURE_ROOT="$TMP_DIR/software/nanoFramework"
FIXTURE_TOOLCHAIN="$FIXTURE_ROOT/toolchain"
FIXTURE_ASSEMBLY_INFO="$FIXTURE_ROOT/CubleyNative.Interop/Properties/AssemblyInfo.cs"
FIXTURE_NATIVE="$TMP_DIR/firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/cubley_interop.cpp"
FIXTURE_PE="$FIXTURE_ROOT/build/CubleyControl/CubleyNative.pe"

mkdir -p \
  "$FIXTURE_TOOLCHAIN" \
  "$(dirname "$FIXTURE_ASSEMBLY_INFO")" \
  "$(dirname "$FIXTURE_NATIVE")" \
  "$(dirname "$FIXTURE_PE")"

cp "$SCRIPT_DIR/interop-checksum.sh" "$FIXTURE_TOOLCHAIN/"
cp "$SOURCE_ASSEMBLY_INFO" "$FIXTURE_ASSEMBLY_INFO"
cp "$SOURCE_NATIVE" "$FIXTURE_NATIVE"
python3 - "$FIXTURE_PE" <<'PYEOF'
import struct
import sys
from pathlib import Path

data = bytearray(24)
struct.pack_into("<I", data, 20, 0x55A991DA)
Path(sys.argv[1]).write_bytes(data)
PYEOF
sed -i 's/AssemblyNativeVersion("[0-9A-Fa-f]\{8\}")/AssemblyNativeVersion("DEADBEEF")/' "$FIXTURE_ASSEMBLY_INFO"

FIXTURE_SCRIPT="$FIXTURE_TOOLCHAIN/interop-checksum.sh"
"$FIXTURE_SCRIPT" --fix --assembly CubleyNative --pe "$FIXTURE_PE" >/dev/null
first_hashes="$(sha256sum "$FIXTURE_ASSEMBLY_INFO" "$FIXTURE_NATIVE")"
"$FIXTURE_SCRIPT" --fix --assembly CubleyNative --pe "$FIXTURE_PE" >/dev/null
second_hashes="$(sha256sum "$FIXTURE_ASSEMBLY_INFO" "$FIXTURE_NATIVE")"

if [[ "$first_hashes" != "$second_hashes" ]]; then
  echo "FAIL: checksum --fix is not idempotent." >&2
  exit 1
fi

"$FIXTURE_SCRIPT" --check --assembly CubleyNative --pe "$FIXTURE_PE" >/dev/null
echo "PASS: checksum --fix is idempotent and produces aligned sources."

sed -i 's/AssemblyNativeVersion("[0-9A-Fa-f]\{8\}")/AssemblyNativeVersion("DEADBEEF")/' "$FIXTURE_ASSEMBLY_INFO"
managed_hash_before="$(sha256sum "$FIXTURE_ASSEMBLY_INFO")"
chmod a-w "$FIXTURE_NATIVE"

set +e
fix_output="$($FIXTURE_SCRIPT --fix --assembly CubleyNative --pe "$FIXTURE_PE" 2>&1)"
fix_rc=$?
set -e

if [[ $fix_rc -eq 0 ]]; then
  echo "FAIL: checksum --fix unexpectedly succeeded with an unwritable native source." >&2
  exit 1
fi

managed_hash_after="$(sha256sum "$FIXTURE_ASSEMBLY_INFO")"
if [[ "$managed_hash_before" != "$managed_hash_after" ]]; then
  echo "FAIL: checksum --fix partially updated managed metadata before native write failure." >&2
  echo "$fix_output" >&2
  exit 1
fi

echo "PASS: checksum --fix rejects unwritable sources without partial updates."