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
  --clraddr <hex>         CLR flash address (default: 0x08010000)

  --erase-deploy          Erase deployment region before flashing firmware
  --deployaddr <hex>      Deployment start (default: 0x080C0000)
  --deploysize <hex>      Deployment size  (default: 0x00040000)

  --release-layout        Use release layout defaults:
                          clraddr=0x08010000 deployaddr=0x080C0000 deploysize=0x00040000
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
CLR_ADDR="0x08010000"
DEPLOY_ADDR="0x080C0000"
DEPLOY_SIZE="0x00040000"

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
      CLR_ADDR="0x08010000"
      DEPLOY_ADDR="0x080C0000"
      DEPLOY_SIZE="0x00040000"
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

validate_image_address() {
  local image="$1"
  local configured_address="$2"
  local label="$3"
  local elf="${image%.bin}.elf"

  if [[ "$elf" == "$image" || ! -f "$elf" ]]; then
    echo "[flash] Warning: cannot validate $label address without adjacent ELF: $elf" >&2
    return
  fi

  require_cmd arm-none-eabi-readelf
  local linked_address
  linked_address="$(arm-none-eabi-readelf -l "$elf" | awk '$1 == "LOAD" && $3 ~ /^0x08/ { print $3; exit }')"
  if [[ -z "$linked_address" ]]; then
    echo "Unable to determine $label flash address from: $elf" >&2
    exit 1
  fi

  if (( configured_address != linked_address )); then
    printf 'Refusing to flash %s at %s: %s is linked for 0x%08X\n' \
      "$label" "$configured_address" "$elf" "$((linked_address))" >&2
    exit 1
  fi
}

resolve_path() {
  local path_in="$1"
  local resolved=""

  if command -v readlink >/dev/null 2>&1; then
    resolved="$(readlink -f "$path_in" 2>/dev/null || true)"
    if [[ -n "$resolved" ]]; then
      echo "$resolved"
      return
    fi
  fi

  if [[ "$path_in" == /* ]]; then
    echo "$path_in"
  else
    echo "$WORK_DIR/$path_in"
  fi
}

print_completion_summary() {
  local booter_path clr_path
  booter_path="$(resolve_path "$BOOTER_BIN")"
  clr_path="$(resolve_path "$CLR_BIN")"

  echo
  echo "Summary:"
  echo "  preset:        $PRESET"
  echo "  booter binary: $booter_path"
  echo "  clr binary:    $clr_path"
  echo "  booter addr:   $BOOT_ADDR"
  echo "  clr addr:      $CLR_ADDR"
  echo "  deploy addr:   $DEPLOY_ADDR"
  echo "  deploy size:   $DEPLOY_SIZE"

  echo
  echo "st-flash commands (current layout):"
  if [[ "$ERASE_DEPLOY" == "true" ]]; then
    echo "  st-flash erase $DEPLOY_ADDR $DEPLOY_SIZE"
  fi
  if [[ "$FLASH_BOOTER" == "true" ]]; then
    echo "  st-flash write \"$booter_path\" $BOOT_ADDR"
  else
    echo "  # booter write skipped (--no-booter)"
  fi
  echo "  st-flash write \"$clr_path\" $CLR_ADDR"
  if [[ "$DO_RESET" == "true" ]]; then
    echo "  st-flash reset"
  fi
}

clean_build_space_preserve_downloads() {
  local build_dir="$WORK_DIR/build"
  local cmake_files_dir="$build_dir/CMakeFiles"

  if [[ ! -d "$build_dir" ]]; then
    return
  fi

  echo "[build] Clean build directory (preserve downloads/cache): $build_dir"

  # Keep _deps so nf-interpreter/chibios fetch artifacts are reused.
  # Keep CMakeFiles/fc-stamp and CMakeFiles/fc-tmp so FetchContent does not
  # re-populate dependencies on every configure.
  find "$build_dir" -mindepth 1 -maxdepth 1 \
    ! -name "_deps" \
    ! -name "CMakeFiles" \
    -exec rm -rf {} +

  if [[ -d "$cmake_files_dir" ]]; then
    find "$cmake_files_dir" -mindepth 1 -maxdepth 1 \
      ! -name "fc-stamp" \
      ! -name "fc-tmp" \
      -exec rm -rf {} +
  fi
}

prepare_cubley_f407_overlay() {
  local local_target_dir="$WORK_DIR/../targets-local/CUBLEY_F407_0_5"
  local community_target_path="$WORK_DIR/targets-community/ChibiOS/CUBLEY_F407_0_5"
  local interop_module="$WORK_DIR/CMake/Modules/FindINTEROP-CubleyNative.cmake"

  if [[ ! -d "$local_target_dir" ]]; then
    echo "Missing local target directory: $local_target_dir" >&2
    echo "Cannot prepare CUBLEY_F407_0_5 preset assets." >&2
    exit 1
  fi

  mkdir -p "$(dirname "$community_target_path")"

  if [[ -e "$community_target_path" && ! -L "$community_target_path" ]]; then
    echo "[build] Using existing community target directory: $community_target_path"
  else
    ln -sfn "../../../targets-local/CUBLEY_F407_0_5" "$community_target_path"
  fi

  local interop_cpp="$community_target_path/nanoCLR/cubley_interop.cpp"
  local lnbh26_cpp="$community_target_path/nanoCLR/lnbh26_interop.cpp"

  if [[ ! -f "$interop_cpp" || ! -f "$lnbh26_cpp" ]]; then
    echo "Interop source files not found under: $community_target_path/nanoCLR" >&2
    exit 1
  fi

  cat > "$interop_module" <<'EOF'
# Auto-generated by build-flash-cubley.sh for CubleyNative interop registration.
set(CubleyNative_INCLUDE_DIRS "${CMAKE_SOURCE_DIR}/targets-community/ChibiOS/CUBLEY_F407_0_5/nanoCLR")
set(CubleyNative_SOURCES
    "${CMAKE_SOURCE_DIR}/targets-community/ChibiOS/CUBLEY_F407_0_5/nanoCLR/cubley_interop.cpp"
    "${CMAKE_SOURCE_DIR}/targets-community/ChibiOS/CUBLEY_F407_0_5/nanoCLR/lnbh26_interop.cpp")

include(FindPackageHandleStandardArgs)
FIND_PACKAGE_HANDLE_STANDARD_ARGS(INTEROP-CubleyNative DEFAULT_MSG CubleyNative_INCLUDE_DIRS CubleyNative_SOURCES)
EOF
}

do_build() {
  require_cmd cmake
  clean_build_space_preserve_downloads
  if [[ "$PRESET" == "CUBLEY_F407_0_5" ]]; then
    echo "[build] Prepare CUBLEY_F407_0_5 local target + interop module"
    prepare_cubley_f407_overlay
  fi
  echo "[build] Configure preset: $PRESET"
  cmake --preset "$PRESET"
  echo "[build] Build preset: $PRESET (jobs=$JOBS)"
  cmake --build --preset "$PRESET" -j"$JOBS"
}

do_flash() {
  require_cmd st-flash

  if [[ "$FLASH_BOOTER" == "true" ]]; then
    validate_image_address "$BOOTER_BIN" "$BOOT_ADDR" "nanoBooter"
  fi
  validate_image_address "$CLR_BIN" "$CLR_ADDR" "nanoCLR"

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

print_completion_summary

echo "Done."