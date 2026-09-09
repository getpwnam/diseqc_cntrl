#!/usr/bin/env python3
import json
import re
from pathlib import Path


REQUIRED_ASSEMBLIES = [
    "CubleyControl.pe",
    "CubleyNative.pe",
    "CubleyDiseqcManaged.pe",
    "System.Device.Gpio.pe",
    "nanoFramework.Hardware.Stm32.pe",
    "nanoFramework.Runtime.Events.pe",
    "System.Threading.pe",
    "nanoFramework.Runtime.Native.pe",
    "nanoFramework.System.Collections.pe",
    "System.IO.Streams.pe",
    "nanoFramework.System.Text.pe",
    "System.Net.pe",
    "mscorlib.pe",
]

EXPECTED_BUILD_MANIFEST = [
    "$PRIMARY_PE",
    "$CUBLEY_INTEROP_PE",
    "$CUBLEY_DISEQC_MANAGED_PE",
    "$OUTPUT_DIR/System.Device.Gpio.pe",
    "$OUTPUT_DIR/nanoFramework.Hardware.Stm32.pe",
    "$RUNTIME_EVENTS_PE",
    "$OUTPUT_DIR/System.Threading.pe",
    "$OUTPUT_DIR/nanoFramework.Runtime.Native.pe",
    "$OUTPUT_DIR/nanoFramework.System.Collections.pe",
    "$OUTPUT_DIR/System.IO.Streams.pe",
    "$SYSTEM_TEXT_PE",
    "$OUTPUT_DIR/System.Net.pe",
    "$OUTPUT_DIR/mscorlib.pe",
]


def require(condition, message):
    if not condition:
        raise SystemExit(f"FAIL: {message}")


repo_root = Path(__file__).resolve().parents[3]
tasks_path = repo_root / ".vscode/tasks.json"
build_script_path = repo_root / "software/nanoFramework/toolchain/build-CubleyControl.sh"
deploy_script_path = repo_root / "software/nanoFramework/toolchain/deploy-CubleyControl.sh"
project_path = repo_root / "software/nanoFramework/CubleyControl/CubleyControl.nfproj"
debug_booter_linker_path = (
    repo_root
    / "firmware/targets-local/CUBLEY_F407_0_5/nanoBooter/STM32F407xG_booter-DEBUG.ld"
)
debug_clr_linker_path = (
    repo_root
    / "firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/STM32F407xG_CLR-DEBUG.ld"
)

tasks = json.loads(tasks_path.read_text(encoding="utf-8"))
tasks_by_label = {task["label"]: task for task in tasks["tasks"]}
prepare_command = tasks_by_label["nf: prepare CubleyControl debug assemblies"]["command"]
staged_assemblies = re.findall(r'\$src/([^" ]+\.pe)"', prepare_command)
require(
    staged_assemblies == REQUIRED_ASSEMBLIES,
    f"Debug assembly staging differs from required manifest: {staged_assemblies}",
)

erase_command = tasks_by_label["nf: erase managed deploy region (SWD)"]["command"]
require(
    erase_command == "st-flash erase 0x080C0000 0x00040000",
    f"unexpected Debug deployment erase command: {erase_command}",
)

build_script = build_script_path.read_text(encoding="utf-8")
project = project_path.read_text(encoding="utf-8")
require(
    "generate_build_info" in build_script and 'git -C "$ROOT_DIR" rev-parse --short=8 HEAD' in build_script,
    "build script does not generate Git-qualified build identity",
)
require(
    '"-p:BuildInfoSource=$BUILD_INFO_SOURCE"' in build_script,
    "build script does not pass generated build identity to MSBuild",
)
require(
    '<Compile Include="BuildInfo.cs" Condition=" \'$(BuildInfoSource)\' == \'\' " />' in project and
    '<Compile Include="$(BuildInfoSource)" Condition=" \'$(BuildInfoSource)\' != \'\' " />' in project,
    "project does not select generated build identity with a direct-build fallback",
)
manifest_match = re.search(
    r"required_pe_paths=\((?P<body>.*?)\n\s*\)", build_script, flags=re.S
)
require(manifest_match is not None, "required_pe_paths manifest not found")
manifest_body = manifest_match.group("body")
build_manifest = re.findall(r'^\s*"([^"]+)"\s*$', manifest_body, flags=re.M)
require(
    build_manifest == EXPECTED_BUILD_MANIFEST,
    f"build manifest differs from required ordered inputs: {build_manifest}",
)
require(
    'Required deployment assembly missing:' in build_script,
    "build script does not fail closed for a missing required assembly",
)
require(
    'system_text_candidates=("$ROOT_DIR"/packages/nanoFramework.System.Text.*/lib/nanoFramework.System.Text.pe)' in build_script,
    "build script does not resolve the transitive System.Text deployment assembly",
)
require(
    'Required deployment packer is unavailable:' in build_script,
    "build script does not fail closed when the deployment packer is unavailable",
)
require(
    'packed_bundle="$(readlink -f "$latest_bundle")"' in build_script and
    'python3 "$SCRIPT_DIR/inspect_deploy_bundle.py" "$packed_bundle"' in build_script,
    "build script does not resolve and validate the authoritative packed bundle",
)
require(
    'output_tmp="$(mktemp "$OUTPUT_DIR/.${TARGET_NAME}.bin.XXXXXX")"' in build_script and
    'cp "$packed_bundle" "$output_tmp"' in build_script and
    'mv -f "$output_tmp" "$OUTPUT_BIN"' in build_script and
    'cmp -s "$packed_bundle" "$OUTPUT_BIN"' in build_script,
    "build script does not atomically publish and compare the compatibility bundle",
)
require(
    'bundle_name="${TARGET_NAME}_bundle_${timestamp}.bin"' not in build_script,
    "build script still republishes the validated bundle through a second timestamped copy",
)

deploy_script = deploy_script_path.read_text(encoding="utf-8")
require(
    'ADDRESS="0x080C0000"' in deploy_script,
    "deploy helper default is not the Debug deployment start",
)

debug_booter_linker = debug_booter_linker_path.read_text(encoding="utf-8")
debug_clr_linker = debug_clr_linker_path.read_text(encoding="utf-8")
require(
    "org = 0x08000000, len = 48k" in debug_booter_linker,
    "Debug nanoBooter linker range changed",
)
require(
    "org = 0x0800C000" in debug_clr_linker,
    "Debug nanoCLR linker origin changed",
)
require(
    "deployment (rx) : org = 0x080C0000, len = 256k" in debug_clr_linker,
    "Debug deployment linker range changed",
)

print("PASS: CubleyControl deployment manifest and Debug flash layout are aligned.")