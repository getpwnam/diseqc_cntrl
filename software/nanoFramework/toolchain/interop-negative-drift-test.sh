#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

GUARD_TOOL="$SCRIPT_DIR/interop-guard.sh"
SOURCE_CS="$ROOT_DIR/Cubley.Interop/CubleyInteropNative.cs"
SOURCE_NATIVE="$ROOT_DIR/nf-native/cubley_interop.cpp"

if [[ ! -x "$GUARD_TOOL" ]]; then
  echo "Missing or non-executable guard tool: $GUARD_TOOL" >&2
  exit 1
fi

TMP_DIR="$(mktemp -d /tmp/interop-drift-fixture-XXXXXX)"
cleanup() {
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

FIXTURE_CS="$TMP_DIR/CubleyInteropNative.cs"
FIXTURE_NATIVE="$TMP_DIR/cubley_interop.cpp"

cp "$SOURCE_CS" "$FIXTURE_CS"
cp "$SOURCE_NATIVE" "$FIXTURE_NATIVE"

python3 - "$FIXTURE_NATIVE" <<'PYEOF'
import re
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")

pattern = re.compile(r"(static\s+const\s+CLR_RT_MethodHandler\s+method_lookup\[\]\s*=\s*\{)(.*?)(\n\};)", re.S)
match = pattern.search(text)
if not match:
    raise SystemExit("Unable to locate method_lookup[] block for fixture mutation")

body = match.group(2)
lines = body.splitlines()

entry_indices = [i for i, line in enumerate(lines) if "// [" in line and "." in line]
if len(entry_indices) < 2:
    raise SystemExit("Not enough method_lookup entries for fixture mutation")

first = entry_indices[0]
second = entry_indices[1]
lines[first], lines[second] = lines[second], lines[first]

mutated = text[:match.start(2)] + "\n".join(lines) + text[match.end(2):]
path.write_text(mutated, encoding="utf-8")
PYEOF

set +e
guard_output="$($GUARD_TOOL --cs "$FIXTURE_CS" --native "$FIXTURE_NATIVE" 2>&1)"
guard_rc=$?
set -e

if [[ $guard_rc -eq 0 ]]; then
  echo "FAIL: guard unexpectedly passed intentional non-append drift fixture." >&2
  exit 1
fi

if ! grep -Eq "Non-append slot drift detected|method_lookup index drift|InternalCall method order drift" <<< "$guard_output"; then
  echo "FAIL: guard failed for fixture but did not report expected drift diagnostics." >&2
  echo "$guard_output" >&2
  exit 1
fi

echo "PASS: intentional non-append drift fixture was detected and blocked."