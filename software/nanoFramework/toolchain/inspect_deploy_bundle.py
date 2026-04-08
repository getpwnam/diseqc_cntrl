#!/usr/bin/env python3
"""Inspect nanoFramework deployment bundles made of NFMRK assembly records.

This script validates marker layout and prints one line per assembly record.
"""

from __future__ import annotations

import argparse
import re
import struct
import sys
from pathlib import Path

NFMRK1 = b"NFMRK1"
NFMRK2 = b"NFMRK2"

# NFMRK2 (modern)
HEADER_SIZE = 128
TOTAL_INDEX = 17
STARTS_OFFSET = 36

# NFMRK1 (legacy)
LEGACY_TOTAL_INDEX = 15
LEGACY_STARTS_OFFSET = 40


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Inspect nanoFramework deployment bundle records")
    parser.add_argument("bundle", type=Path, help="Bundle file (.bin or .pe)")
    parser.add_argument(
        "--max-name-scan-bytes",
        type=int,
        default=4096,
        help="Max bytes per assembly payload to scan for name hints (default: 4096)",
    )
    return parser.parse_args()


def guess_name(data: bytes) -> str:
    # Heuristic scan for likely assembly tokens inside metadata/string blobs.
    text = data.decode("latin-1", errors="ignore")
    candidates = re.findall(r"[A-Za-z_][A-Za-z0-9_.]{2,80}", text)

    preferred_prefixes = (
        "System.",
        "nanoFramework.",
    )

    for token in candidates:
        if token in ("mscorlib", "BlinkBringup"):
            return token
        if token.startswith(preferred_prefixes):
            return token

    # Last fallback: return first non-path-ish token.
    for token in candidates:
        if "/" not in token and "\\" not in token:
            return token

    return "<unknown>"


def inspect_bundle(path: Path, max_name_scan_bytes: int) -> int:
    data = path.read_bytes()
    pos = 0
    count = 0

    print(f"FILE: {path}")
    print(f"SIZE: {len(data)} bytes")

    while pos + 8 <= len(data):
        prefix = data[pos : pos + 6]

        if prefix not in (NFMRK1, NFMRK2):
            tail = data[pos:]
            if tail and all(b == 0xFF for b in tail):
                print(f"TRAILING_ERASED_FLASH from 0x{pos:08X}")
                return 0
            print(f"ERROR: unknown marker at 0x{pos:08X}: {data[pos:pos+8].hex()}")
            return 2

        if pos + HEADER_SIZE > len(data):
            print(f"ERROR: truncated header at 0x{pos:08X}")
            return 2

        try:
            if prefix == NFMRK2:
                total_size = struct.unpack_from("<I", data, pos + STARTS_OFFSET + (TOTAL_INDEX * 4))[0]
            else:
                total_size = struct.unpack_from(
                    "<I", data, pos + LEGACY_STARTS_OFFSET + (LEGACY_TOTAL_INDEX * 4)
                )[0]
        except struct.error:
            print(f"ERROR: failed reading total_size at 0x{pos:08X}")
            return 2

        if total_size < HEADER_SIZE:
            print(f"ERROR: invalid total_size={total_size} at 0x{pos:08X}")
            return 2

        end = pos + total_size
        if end > len(data):
            print(
                f"ERROR: record at 0x{pos:08X} overruns bundle (end=0x{end:08X}, size={len(data)})"
            )
            return 2

        chunk = data[pos:end]
        name_probe = chunk[: min(total_size, max_name_scan_bytes)]
        guessed = guess_name(name_probe)

        marker = "NFMRK1" if prefix == NFMRK1 else "NFMRK2"
        print(
            f"RECORD[{count}] marker={marker} offset=0x{pos:08X} size={total_size:6d} guess={guessed}"
        )

        pos = end
        count += 1

    if pos != len(data):
        print(f"WARN: parsing stopped at 0x{pos:08X}, file end is 0x{len(data):08X}")

    print(f"RECORD_COUNT: {count}")
    return 0


def main() -> int:
    args = parse_args()
    if not args.bundle.exists():
        print(f"ERROR: file not found: {args.bundle}")
        return 2
    return inspect_bundle(args.bundle, args.max_name_scan_bytes)


if __name__ == "__main__":
    sys.exit(main())
