#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

GUARD_TOOL="$SCRIPT_DIR/interop-guard.sh"
SOURCE_CS="$ROOT_DIR/CubleyNative.Interop/CubleyInteropNative.cs"
SOURCE_NATIVE="$ROOT_DIR/../../firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/cubley_interop.cpp"

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

entry_regex = re.compile(r"^\s*(Library_cubley_interop_[A-Za-z0-9_]+)\s*,")
entry_indices = [i for i, line in enumerate(lines) if entry_regex.search(line)]
if len(entry_indices) < 2:
    raise SystemExit("Not enough method_lookup entries for fixture mutation")

first_idx = entry_indices[0]
second_idx = entry_indices[1]

first_match = entry_regex.search(lines[first_idx])
second_match = entry_regex.search(lines[second_idx])
if not first_match or not second_match:
  raise SystemExit("Failed to parse method_lookup comment markers for fixture mutation")

first_handler = first_match.group(1)
second_handler = second_match.group(1)

# Intentionally swap actual handler expressions while leaving comments unchanged.
lines[first_idx] = lines[first_idx].replace(first_handler, second_handler, 1)
lines[second_idx] = lines[second_idx].replace(second_handler, first_handler, 1)

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

if ! grep -Fq "handler expressions do not match managed signatures" <<< "$guard_output"; then
  echo "FAIL: guard failed but did not detect actual handler drift." >&2
  echo "$guard_output" >&2
  exit 1
fi

echo "PASS: intentional native handler drift was detected and blocked."

cp "$SOURCE_NATIVE" "$FIXTURE_NATIVE"
python3 - "$FIXTURE_CS" <<'PYEOF'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
old = "public static extern int NativeSetEnable(int channel, bool enable);"
new = "public static extern int NativeSetEnable(int channel, int enable);"
if text.count(old) != 1:
    raise SystemExit("Unable to locate NativeSetEnable signature for fixture mutation")
path.write_text(text.replace(old, new), encoding="utf-8")
PYEOF

set +e
guard_output="$($GUARD_TOOL --cs "$FIXTURE_CS" --native "$FIXTURE_NATIVE" 2>&1)"
guard_rc=$?
set -e

if [[ $guard_rc -eq 0 ]]; then
  echo "FAIL: guard unexpectedly passed intentional managed signature drift fixture." >&2
  exit 1
fi

if ! grep -Fq "handler expressions do not match managed signatures" <<< "$guard_output"; then
  echo "FAIL: guard failed but did not detect managed signature drift." >&2
  echo "$guard_output" >&2
  exit 1
fi

echo "PASS: intentional managed signature drift was detected and blocked."