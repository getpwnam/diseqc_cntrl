#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./build-flash-cubley.sh <mode> [options]

Modes:
  build         Configure + build CUBLEY_F407_0_5 preset
  flash         Flash nanoBooter + nanoCLR from ./build
  build-flash   Build, then flash

Options:
  --preset <name>         CMake preset (default: CUBLEY_F407_0_5)
  --jobs <n>              Build parallel jobs (default: nproc)

  --booter <path>         nanoBooter binary (default: build/nanoBooter.bin)
  --clr <path>            nanoCLR binary (default: build/nanoCLR.bin)

  --bootaddr <hex>        Booter flash address (default: 0x08000000)
  --clraddr <hex>         CLR flash address (default: 0x08008000)

  --erase-deploy          Erase deployment region before flashing firmware
  --deployaddr <hex>      Deployment start (default: 0x08060000)
  --deploysize <hex>      Deployment size  (default: 0x000A0000)

  --release-layout        Use release layout defaults:
                          clraddr=0x08004000 deployaddr=0x08040000 deploysize=0x000C0000
  --no-booter             Skip flashing nanoBooter
  --reset                 Reset target after flashing
  --help                  Show this help

Examples:
  ./build-flash-cubley.sh build
  ./build-flash-cubley.sh build-flash --erase-deploy --reset
  ./build-flash-cubley.sh flash --release-layout --reset
EOF
}

if [[ $# -lt 1 ]]; then
  usage
  exit 2
fi

MODE="$1"
shift

case "$MODE" in
  build|flash|build-flash) ;;
  -h|--help)
    usage
    exit 0
    ;;
  *)
    echo "Invalid mode: $MODE" >&2
    usage
    exit 2
    ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Script now lives under firmware/, while CMake presets and build artifacts
# live under firmware/nf-interpreter/.
if [[ -d "$SCRIPT_DIR/nf-interpreter" ]]; then
  WORK_DIR="$SCRIPT_DIR/nf-interpreter"
elif [[ -f "$SCRIPT_DIR/CMakePresets.json" ]]; then
  # Backward-compatible fallback if the script is copied back into nf-interpreter.
  WORK_DIR="$SCRIPT_DIR"
else
  echo "Unable to locate nf-interpreter workspace from: $SCRIPT_DIR" >&2
  exit 1
fi

cd "$WORK_DIR"

PRESET="CUBLEY_F407_0_5"
JOBS="$(nproc)"

BOOTER_BIN="build/nanoBooter.bin"
CLR_BIN="build/nanoCLR.bin"

BOOT_ADDR="0x08000000"
CLR_ADDR="0x08008000"
DEPLOY_ADDR="0x08060000"
DEPLOY_SIZE="0x000A0000"

ERASE_DEPLOY="false"
FLASH_BOOTER="true"
DO_RESET="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --preset)
      PRESET="$2"
      shift 2
      ;;
    --jobs)
      JOBS="$2"
      shift 2
      ;;
    --booter)
      BOOTER_BIN="$2"
      shift 2
      ;;
    --clr)
      CLR_BIN="$2"
      shift 2
      ;;
    --bootaddr)
      BOOT_ADDR="$2"
      shift 2
      ;;
    --clraddr)
      CLR_ADDR="$2"
      shift 2
      ;;
    --erase-deploy)
      ERASE_DEPLOY="true"
      shift
      ;;
    --deployaddr)
      DEPLOY_ADDR="$2"
      shift 2
      ;;
    --deploysize)
      DEPLOY_SIZE="$2"
      shift 2
      ;;
    --release-layout)
      CLR_ADDR="0x08004000"
      DEPLOY_ADDR="0x08040000"
      DEPLOY_SIZE="0x000C0000"
      shift
      ;;
    --no-booter)
      FLASH_BOOTER="false"
      shift
      ;;
    --reset)
      DO_RESET="true"
      shift
      ;;
    --help|-h)
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

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 2
  fi
}

do_build() {
  require_cmd cmake
  echo "[build] Configure preset: $PRESET"
  cmake --preset "$PRESET"
  echo "[build] Build preset: $PRESET (jobs=$JOBS)"
  cmake --build --preset "$PRESET" -j"$JOBS"
}

do_flash() {
  require_cmd st-flash

  if [[ "$ERASE_DEPLOY" == "true" ]]; then
    echo "[flash] Erase deployment region: $DEPLOY_ADDR size $DEPLOY_SIZE"
    st-flash erase "$DEPLOY_ADDR" "$DEPLOY_SIZE"
  fi

  if [[ "$FLASH_BOOTER" == "true" ]]; then
    if [[ ! -f "$BOOTER_BIN" ]]; then
      echo "Booter image not found: $BOOTER_BIN" >&2
      exit 1
    fi
    echo "[flash] Write booter: $BOOTER_BIN @ $BOOT_ADDR"
    st-flash write "$BOOTER_BIN" "$BOOT_ADDR"
  fi

  if [[ ! -f "$CLR_BIN" ]]; then
    echo "CLR image not found: $CLR_BIN" >&2
    exit 1
  fi
  echo "[flash] Write CLR: $CLR_BIN @ $CLR_ADDR"
  st-flash write "$CLR_BIN" "$CLR_ADDR"

  if [[ "$DO_RESET" == "true" ]]; then
    echo "[flash] Reset target"
    st-flash reset
  fi
}

case "$MODE" in
  build)
    do_build
    ;;
  flash)
    do_flash
    ;;
  build-flash)
    do_build
    do_flash
    ;;
esac

echo "Done."