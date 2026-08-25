#!/usr/bin/env python3
import json
import re
from pathlib import Path


REQUIRED_ASSEMBLIES = [
    "CubleyControl.pe",
    "CubleyNative.pe",
    "CubleyLnbh26Managed.pe",
    "CubleyDiseqcManaged.pe",
    "System.Device.Gpio.pe",
    "System.Device.Pwm.pe",
    "nanoFramework.Runtime.Events.pe",
    "System.Threading.pe",
    "nanoFramework.Runtime.Native.pe",
    "nanoFramework.System.Collections.pe",
    "System.IO.Streams.pe",
    "nanoFramework.System.Text.pe",
    "System.Net.pe",
    "nanoFramework.M2Mqtt.Core.pe",
    "nanoFramework.M2Mqtt.pe",
    "mscorlib.pe",
]

EXPECTED_BUILD_MANIFEST = [
    "$PRIMARY_PE",
    "$CUBLEY_INTEROP_PE",
    "$CUBLEY_LNBH26_MANAGED_PE",
    "$CUBLEY_DISEQC_MANAGED_PE",
    "$OUTPUT_DIR/System.Device.Gpio.pe",
    "$OUTPUT_DIR/System.Device.Pwm.pe",
    "$RUNTIME_EVENTS_PE",
    "$OUTPUT_DIR/System.Threading.pe",
    "$OUTPUT_DIR/nanoFramework.Runtime.Native.pe",
    "$OUTPUT_DIR/nanoFramework.System.Collections.pe",
    "$OUTPUT_DIR/System.IO.Streams.pe",
    "$OUTPUT_DIR/nanoFramework.System.Text.pe",
    "$OUTPUT_DIR/System.Net.pe",
    "$OUTPUT_DIR/nanoFramework.M2Mqtt.Core.pe",
    "$OUTPUT_DIR/nanoFramework.M2Mqtt.pe",
    "$OUTPUT_DIR/mscorlib.pe",
]


def require(condition, message):
    if not condition:
        raise SystemExit(f"FAIL: {message}")


repo_root = Path(__file__).resolve().parents[3]
tasks_path = repo_root / ".vscode/tasks.json"
build_script_path = repo_root / "software/nanoFramework/toolchain/build-CubleyControl.sh"
deploy_script_path = repo_root / "software/nanoFramework/toolchain/deploy-CubleyControl.sh"
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
    erase_command == "st-flash erase 0x08060000 0x000A0000",
    f"unexpected Debug deployment erase command: {erase_command}",
)

build_script = build_script_path.read_text(encoding="utf-8")
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

deploy_script = deploy_script_path.read_text(encoding="utf-8")
require(
    'ADDRESS="0x08060000"' in deploy_script,
    "deploy helper default is not the Debug deployment start",
)

debug_booter_linker = debug_booter_linker_path.read_text(encoding="utf-8")
debug_clr_linker = debug_clr_linker_path.read_text(encoding="utf-8")
require(
    "org = 0x08000000, len = 32k" in debug_booter_linker,
    "Debug nanoBooter linker range changed",
)
require(
    "org = 0x08008000" in debug_clr_linker,
    "Debug nanoCLR linker origin changed",
)
require(
    "deployment (rx) : org = 0x08060000, len = 640k" in debug_clr_linker,
    "Debug deployment linker range changed",
)

print("PASS: CubleyControl deployment manifest and Debug flash layout are aligned.")