#!/usr/bin/env python3

import argparse
from dataclasses import dataclass
from pathlib import Path


FLASH_BASE = 0x08000000
FLASH_SIZE = 1024 * 1024
CONFIG_START = 0x0800C000
CONFIG_SIZE = 16 * 1024
CLR_START = 0x08010000
CLR_SIZE = 704 * 1024
DEPLOYMENT_START = 0x080C0000
DEPLOYMENT_SIZE = 256 * 1024
CUBLEY_CONFIG_RESERVATION = 512
NETWORK_INTERFACE_SIZE = 117
X509_HEADER_SIZE = 8
REPRESENTATIVE_P256_PEM_SIZE = 1067


@dataclass(frozen=True)
class Region:
    name: str
    start: int
    capacity: int
    used: int
    usage_note: str = ""


@dataclass(frozen=True)
class FlashSector:
    number: int
    start: int
    size: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Print Markdown tables for the current Cubley flash layout."
    )
    parser.add_argument(
        "--repo",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="repository root (defaults to the parent of tools/)",
    )
    parser.add_argument(
        "--device-certificate",
        type=Path,
        help="combined certificate/private-key PEM; its size replaces the representative estimate",
    )
    parser.add_argument(
        "--config-used",
        type=int,
        help="measured configuration bytes; overrides the calculated estimate",
    )
    return parser.parse_args()


def artifact_size(path: Path) -> int:
    try:
        return path.resolve(strict=True).stat().st_size
    except FileNotFoundError as error:
        raise SystemExit(f"missing build artifact: {path}\nRun the firmware and managed builds first.") from error


def validate_layout(repo: Path) -> None:
    clr_linker = repo / "firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/STM32F407xG_CLR-DEBUG.ld"
    block_storage = repo / "firmware/targets-local/CUBLEY_F407_0_5/common/Device_BlockStorage-DEBUG.c"
    expected_linker_tokens = ("org = 0x08010000", "len = 1M - 64k - 256k", "org = 0x080C0000", "len = 256k")
    expected_storage_tokens = ("0x08000000", "0x4000", "0x08010000", "0x10000", "0x08020000", "0x20000")

    for path, tokens in ((clr_linker, expected_linker_tokens), (block_storage, expected_storage_tokens)):
        try:
            contents = path.read_text(encoding="ascii")
        except FileNotFoundError as error:
            raise SystemExit(f"missing layout source: {path}") from error
        missing = [token for token in tokens if token not in contents]
        if missing:
            raise SystemExit(f"unsupported flash layout in {path}: missing {', '.join(missing)}")


def stm32f407_sectors() -> list[FlashSector]:
    sizes = [16 * 1024] * 4 + [64 * 1024] + [128 * 1024] * 7
    sectors = []
    address = FLASH_BASE
    for number, size in enumerate(sizes):
        sectors.append(FlashSector(number, address, size))
        address += size
    return sectors


def format_bytes(value: int) -> str:
    if value % 1024 == 0:
        return f"{value // 1024} KiB"
    return f"{value:,} B"


def percent(used: int, capacity: int) -> str:
    return f"{used / capacity * 100:.2f}%"


def range_text(start: int, size: int) -> str:
    return f"`0x{start:08X}-0x{start + size - 1:08X}`"


def region_table(regions: list[Region]) -> None:
    print("## Current Flash Layout\n")
    print("| Block | Address range | Capacity | Used | Fill | Free |")
    print("|---|---:|---:|---:|---:|---:|")
    for region in regions:
        used = f"{region.used:,} B"
        if region.usage_note:
            used += f" {region.usage_note}"
        print(
            f"| {region.name} | {range_text(region.start, region.capacity)} | "
            f"{format_bytes(region.capacity)} | {used} | {percent(region.used, region.capacity)} | "
            f"{region.capacity - region.used:,} B |"
        )

    total_used = sum(region.used for region in regions)
    print(
        f"| **Total flash** | {range_text(FLASH_BASE, FLASH_SIZE)} | **1 MiB** | "
        f"**{total_used:,} B** | **{percent(total_used, FLASH_SIZE)}** | "
        f"**{FLASH_SIZE - total_used:,} B** |"
    )


def sector_assignment(sector: FlashSector) -> str:
    if sector.number <= 2:
        return "nanoBooter"
    if sector.number == 3:
        return "Configuration"
    if sector.number <= 9:
        return "nanoCLR"
    return "Managed deployment"


def overlap_used(region: Region, sector: FlashSector) -> int:
    overlap_start = max(region.start, sector.start)
    overlap_end = min(region.start + region.used, sector.start + sector.size)
    return max(0, overlap_end - overlap_start)


def sector_table(regions: list[Region]) -> None:
    print("\n## Physical Erase Sectors\n")
    print("| Sector | Address range | Capacity | Assignment | Used | Fill |")
    print("|---:|---:|---:|---|---:|---:|")
    region_by_name = {region.name: region for region in regions}
    for sector in stm32f407_sectors():
        assignment = sector_assignment(sector)
        region = region_by_name[assignment]
        if assignment == "Configuration":
            used = region.used
        else:
            used = overlap_used(region, sector)
        print(
            f"| {sector.number} | {range_text(sector.start, sector.size)} | "
            f"{format_bytes(sector.size)} | {assignment} | {used:,} B | "
            f"{percent(used, sector.size)} |"
        )


def main() -> None:
    args = parse_args()
    repo = args.repo.resolve()
    validate_layout(repo)

    certificate_size = (
        artifact_size(args.device_certificate)
        if args.device_certificate
        else REPRESENTATIVE_P256_PEM_SIZE
    )
    estimated_config = (
        args.config_used
        if args.config_used is not None
        else NETWORK_INTERFACE_SIZE + X509_HEADER_SIZE + certificate_size + CUBLEY_CONFIG_RESERVATION
    )
    if estimated_config < 0 or estimated_config > CONFIG_SIZE:
        raise SystemExit(f"configuration usage {estimated_config} is outside the 16 KiB sector")

    regions = [
        Region("nanoBooter", FLASH_BASE, 48 * 1024, artifact_size(repo / "firmware/nf-interpreter/build/nanoBooter.bin")),
        Region("Configuration", CONFIG_START, CONFIG_SIZE, estimated_config, "(estimated)"),
        Region("nanoCLR", CLR_START, CLR_SIZE, artifact_size(repo / "firmware/nf-interpreter/build/nanoCLR.bin")),
        Region(
            "Managed deployment",
            DEPLOYMENT_START,
            DEPLOYMENT_SIZE,
            artifact_size(repo / "software/nanoFramework/build/CubleyControl/latest.deploy.bin"),
        ),
    ]

    for region in regions:
        if region.used > region.capacity:
            raise SystemExit(f"{region.name} exceeds its region by {region.used - region.capacity} bytes")

    region_table(regions)
    sector_table(regions)
    if args.config_used is None:
        source = str(args.device_certificate) if args.device_certificate else "representative 1,067-byte P-256 PEM"
        print(
            "\nConfiguration estimate: 117-byte Ethernet record + 8-byte X.509 header + "
            f"{source} + 512-byte Cubley reservation."
        )


if __name__ == "__main__":
    main()