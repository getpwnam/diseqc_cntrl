#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NF_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
NATIVE_PATH="$NF_ROOT/nf-native/cubley_interop.cpp"

if [[ ! -f "$NATIVE_PATH" ]]; then
  echo "Native interop file not found: $NATIVE_PATH" >&2
  exit 1
fi

python3 - "$NATIVE_PATH" <<'PYEOF'
import re
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")

required_global = "volatile uint32_t g_cubley_diag_boot_probe_latched = 0;"
if required_global not in text:
    raise SystemExit("FAIL: missing explicit boot probe latch state global")

fn_match = re.search(
    r"HRESULT\s+Library_cubley_interop_DiagnosticsMailbox_NativeTryLatchBootProbe___STATIC__BOOLEAN__U4\s*\(CLR_RT_StackFrame&\s+stack\)\s*\{(?P<body>.*?)\n\}",
    text,
    flags=re.S,
)
if not fn_match:
    raise SystemExit("FAIL: unable to locate NativeTryLatchBootProbe implementation")

body = fn_match.group("body")

required_snippets = [
    "g_cubley_diag_boot_probe_latched != 0",
    "(statusWord & kCubleyStatusMagicMask) != kCubleyStatusMagic",
    "stage != kCubleyBootProbeStage",
    "g_cubley_diag_boot_probe_status = statusWord;",
    "g_cubley_diag_boot_probe_latched = 1;",
]

for snippet in required_snippets:
    if snippet not in body:
        raise SystemExit(f"FAIL: expected snippet not found in latch function: {snippet}")

if "g_cubley_diag_current_status" in body:
    raise SystemExit("FAIL: latch function should not write transient current status")

print("PASS: Tier-0 interop latch semantics guardrails are present.")
PYEOF
