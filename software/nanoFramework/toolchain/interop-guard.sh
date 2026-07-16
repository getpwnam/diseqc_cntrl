#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

CS_PATH="$ROOT_DIR/CubleyNative.Interop/CubleyInteropNative.cs"
NATIVE_PATH=""

resolve_native_path() {
    local candidates=(
        "$ROOT_DIR/nf-native/cubley_interop.cpp"
        "$ROOT_DIR/../../firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/cubley_interop.cpp"
        "$ROOT_DIR/../../firmware/nf-interpreter/targets-community/ChibiOS/CUBLEY_F407_0_5/nanoCLR/cubley_interop.cpp"
    )

    local c
    for c in "${candidates[@]}"; do
        if [[ -f "$c" ]]; then
            printf '%s\n' "$c"
            return 0
        fi
    done

    return 1
}

usage() {
    cat <<'EOF'
Usage:
    ./toolchain/interop-guard.sh [--cs /path/to/CubleyInteropNative.cs] [--native /path/to/cubley_interop.cpp]

Defaults:
    --cs      software/nanoFramework/CubleyNative.Interop/CubleyInteropNative.cs
    --native  auto-detected (targets-local preferred)
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --cs)
            CS_PATH="${2:-}"
            shift 2
            ;;
        --native)
            NATIVE_PATH="${2:-}"
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

if [[ -z "$NATIVE_PATH" ]]; then
    NATIVE_PATH="$(resolve_native_path || true)"
fi

if [[ ! -f "$CS_PATH" ]]; then
    echo "Missing file: $CS_PATH" >&2
    exit 1
fi

if [[ ! -f "$NATIVE_PATH" ]]; then
    echo "Missing file: $NATIVE_PATH" >&2
    exit 1
fi

python3 - "$CS_PATH" "$NATIVE_PATH" <<'PYEOF'
import re
import sys
from pathlib import Path

cs_path = Path(sys.argv[1])
native_path = Path(sys.argv[2])

cs_text = cs_path.read_text(encoding="utf-8")
native_text = native_path.read_text(encoding="utf-8")

class_re = re.compile(r"^\s*public\s+static\s+(?:partial\s+)?class\s+([A-Za-z0-9_]+)")
method_re = re.compile(r"^\s*(public|private)\s+static\s+(extern\s+)?[A-Za-z0-9_<>,\[\]\s]+\s+([A-Za-z0-9_]+)\s*\(")

current_class = None
pending_internal = False
internalcall_methods = []
non_extern_methods = []

# Frozen baseline slots are immutable. New methods may only append.
V1_BASELINE = [
    "DiagMailbox.NativeSet",
    "DiagMailbox.NativeGet",
    "DiagMailbox.NativeGetLastNativeError",
    "Fram24C128.NativeInit",
    "Fram24C128.NativeWrite",
    "Fram24C128.NativeRead",
    "LNBH26.NativeInit",
    "LNBH26.NativeSetEnable",
    "LNBH26.NativeReadStatus",
    "LNBH26.NativeReadStatusPair",
    "LNBH26.NativeSetPolarizationForChannel",
    "LNBH26.NativeSetBandForChannel",
    "LNBH26.NativeSetLowPowerForChannel",
    "LNBH26.NativeSetDiseqcInputModeForChannel",
    "LNBH26.NativeGetPolarizationForChannel",
    "LNBH26.NativeGetBandForChannel",
    "LNBH26.NativeGetLastError",
    "LNBH26.NativeGetLastErrorDetail",
    "LNBH26Registers.NativeReadRegister",
    "UsbCdcConsole.NativeIsEnabled",
    "UsbCdcConsole.NativeReadByte",
    "UsbCdcConsole.NativeWrite",
]

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
    print("ERROR: CubleyNative interop surface must be native-only; managed method bodies found:")
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
    print("ERROR: method_lookup[] contains NULL entries; frozen baseline maps native methods only.")
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

if len(lookup_methods) < len(V1_BASELINE):
    print(
        "ERROR: method_lookup[] has fewer entries than the baseline; "
        "baseline slots cannot be removed."
    )
    print(f"  baseline slots: {len(V1_BASELINE)}")
    print(f"  current slots:  {len(lookup_methods)}")
    sys.exit(1)

prefix_drift = []
for i, expected in enumerate(V1_BASELINE):
    actual = lookup_methods[i]
    if actual != expected:
        prefix_drift.append((i, expected, actual))

if prefix_drift:
    print("ERROR: Non-append slot drift detected in immutable baseline.")
    for idx, expected, actual in prefix_drift:
        print(f"  [{idx:02d}] expected={expected} | actual={actual}")
    last_baseline_slot = len(V1_BASELINE) - 1
    print(
        "Only append-only additions are allowed after "
        f"slot {last_baseline_slot}."
    )
    sys.exit(1)

if internalcall_methods != lookup_methods:
    print("ERROR: InternalCall method order drift between CubleyInteropNative.cs and native method_lookup[].")
    max_len = max(len(internalcall_methods), len(lookup_methods))
    for i in range(max_len):
        managed = internalcall_methods[i] if i < len(internalcall_methods) else "<missing>"
        native = lookup_methods[i] if i < len(lookup_methods) else "<missing>"
        marker = "OK" if managed == native else "DIFF"
        print(f"  [{i:02d}] managed={managed} | native={native}  <-- {marker}")
    sys.exit(1)

appended = len(lookup_methods) - len(V1_BASELINE)
print(
    "Interop guard PASS: native-only CubleyNative interop surface, aligned method order, "
    f"and immutable baseline preserved (appended slots: {appended})."
)
PYEOF
