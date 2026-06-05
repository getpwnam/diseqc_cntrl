#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

CS_PATH="$ROOT_DIR/Cubley.Interop/CubleyInteropNative.cs"
NATIVE_PATH="$ROOT_DIR/nf-native/cubley_interop.cpp"

python3 - "$CS_PATH" "$NATIVE_PATH" <<'PYEOF'
import re
import sys
from pathlib import Path

cs_path = Path(sys.argv[1])
native_path = Path(sys.argv[2])

cs_text = cs_path.read_text(encoding="utf-8")
native_text = native_path.read_text(encoding="utf-8")

class_re = re.compile(r"^\s*public\s+static\s+class\s+([A-Za-z0-9_]+)")
method_re = re.compile(r"^\s*(public|private)\s+static\s+(extern\s+)?[A-Za-z0-9_<>,\[\]\s]+\s+([A-Za-z0-9_]+)\s*\(")

current_class = None
pending_internal = False
internalcall_methods = []
non_extern_methods = []

for line in cs_text.splitlines():
    m_class = class_re.match(line)
    if m_class:
        current_class = m_class.group(1)

    if "[MethodImpl(MethodImplOptions.InternalCall)]" in line:
        pending_internal = True
        continue

    m_method = method_re.match(line)
    if not m_method or current_class is None:
        continue

    is_extern = m_method.group(2) is not None
    method_name = m_method.group(3)
    fq_name = f"{current_class}.{method_name}"

    if pending_internal:
        if not is_extern:
            print(f"ERROR: InternalCall method is not extern: {fq_name}")
            sys.exit(1)
        internalcall_methods.append(fq_name)
    elif not is_extern:
        non_extern_methods.append(fq_name)

    pending_internal = False

if non_extern_methods:
    print("ERROR: Cubley.Interop must be native-only; managed method bodies found:")
    for name in non_extern_methods:
        print(f"  - {name}")
    sys.exit(1)

lookup_match = re.search(
    r"static\s+const\s+CLR_RT_MethodHandler\s+method_lookup\[\]\s*=\s*\{(?P<body>.*?)\};",
    native_text,
    flags=re.S,
)
if not lookup_match:
    print("ERROR: Unable to locate method_lookup[] in native file")
    sys.exit(1)

lookup_body = lookup_match.group("body")
if re.search(r"^\s*NULL\s*,", lookup_body, flags=re.M):
    print("ERROR: method_lookup[] contains NULL entries; Cubley.Interop should map only native methods.")
    sys.exit(1)

lookup_entries = []
for line in lookup_body.splitlines():
    m = re.search(r"//\s*\[(\d+)\]\s+([A-Za-z0-9_]+\.[A-Za-z0-9_]+)", line)
    if not m:
        continue
    idx = int(m.group(1))
    name = m.group(2)
    lookup_entries.append((idx, name))

if not lookup_entries:
    print("ERROR: method_lookup[] comments with [index] Class.Method markers were not found.")
    sys.exit(1)

for expected_idx, (idx, _) in enumerate(lookup_entries):
    if idx != expected_idx:
        print(f"ERROR: method_lookup index drift: expected [{expected_idx}] but found [{idx}].")
        sys.exit(1)

lookup_methods = [name for _, name in lookup_entries]

if internalcall_methods != lookup_methods:
    print("ERROR: InternalCall method order drift between CubleyInteropNative.cs and native method_lookup[].")
    max_len = max(len(internalcall_methods), len(lookup_methods))
    for i in range(max_len):
        managed = internalcall_methods[i] if i < len(internalcall_methods) else "<missing>"
        native = lookup_methods[i] if i < len(lookup_methods) else "<missing>"
        marker = "OK" if managed == native else "DIFF"
        print(f"  [{i:02d}] managed={managed} | native={native}  <-- {marker}")
    sys.exit(1)

print("Interop guard PASS: native-only Cubley.Interop and aligned method order.")
PYEOF
