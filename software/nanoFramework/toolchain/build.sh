#!/bin/bash
# nanoFramework Build Script for DiSEqC Controller
# Builds firmware using Docker Compose V2
# Uses: docker compose (not docker-compose)

set -e  # Exit on error

# ── Host bootstrap ──────────────────────────────────────────────────────────
# This script is designed to run inside the Docker build container.
# If called from the host (/.dockerenv absent), re-invoke via docker compose.
if [ ! -f "/.dockerenv" ]; then
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    COMPOSE_DIR="$(dirname "$SCRIPT_DIR")"
    exec docker compose -f "$COMPOSE_DIR/docker-compose.yml" run --rm \
        -e NF_BUILD_PROFILE="${1:-${NF_BUILD_PROFILE:-minimal}}" \
    -e NF_INTERPRETER_REF="${NF_INTERPRETER_REF:-main}" \
    -e NF_UPDATE_INTERPRETER="${NF_UPDATE_INTERPRETER:-1}" \
        nanoframework-build /work/toolchain/build.sh
fi
# ────────────────────────────────────────────────────────────────────────────

echo "========================================"
echo "nanoFramework DiSEqC Controller Build"
echo "========================================"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
NF_INTERPRETER_REPO="https://github.com/nanoframework/nf-interpreter.git"
NF_INTERPRETER_DIR="/nf-interpreter"
NF_INTERPRETER_REF="${NF_INTERPRETER_REF:-main}"
TARGET_NAME="M0DMF_DISEQC_F407"
BUILD_TYPE="Release"
BUILD_PROFILE="${NF_BUILD_PROFILE:-${1:-minimal}}"

# Performance knobs (override via environment variables).
# - NF_BUILD_JOBS: parallel compile jobs (default: all host cores)
# - NF_INCREMENTAL_BUILD: 1 keeps CMake cache between same-profile builds, 0 forces cache clean
# - NF_CLEAN_FETCHCONTENT: 1 purges _deps/chibios fetch state, 0 keeps it for faster rebuilds
# - NF_UPDATE_INTERPRETER: 1 runs git fetch/pull on nf-interpreter, 0 skips network update
BUILD_JOBS="${NF_BUILD_JOBS:-$(nproc)}"
INCREMENTAL_BUILD="${NF_INCREMENTAL_BUILD:-1}"
CLEAN_FETCHCONTENT="${NF_CLEAN_FETCHCONTENT:-0}"
UPDATE_INTERPRETER="${NF_UPDATE_INTERPRETER:-0}"

ENABLE_HSI_PLL="0"  # HSE 8MHz crystal fitted; individual profiles override to HSI if needed

case "$BUILD_PROFILE" in
    minimal)
        ENABLE_SYSTEM_NET="OFF"
        ENABLE_CONFIG_BLOCK="ON"
        ENABLE_SNTP="OFF"
        ENABLE_MBEDTLS="OFF"
        ENABLE_HAL_MAC="FALSE"
        ENABLE_STM32_MAC_ETH="FALSE"
        ENABLE_OTG1="FALSE"
        ENABLE_OTG2="FALSE"
        ENABLE_HAL_USB="FALSE"
        ENABLE_HAL_SERIAL_USB="FALSE"
        ENABLE_USB_NO_VBUS_SENSE="FALSE"
        ENABLE_BRINGUP_SMOKE="FALSE"
        ENABLE_BRINGUP_HARDALIVE="FALSE"
        ENABLE_FEATURE_RTC="ON"
        ENABLE_HAL_RTC="TRUE"
        ENABLE_HSI_PLL="0"
        PROFILE_STATUS="stable"
        PROFILE_NOTE="Minimal non-network firmware profile"
        ;;
    w5500-native)
        ENABLE_SYSTEM_NET="OFF"
        ENABLE_CONFIG_BLOCK="OFF"
        ENABLE_SNTP="OFF"
        ENABLE_MBEDTLS="OFF"
        ENABLE_HAL_MAC="FALSE"
        ENABLE_STM32_MAC_ETH="FALSE"
        ENABLE_OTG1="FALSE"
        ENABLE_OTG2="FALSE"
        ENABLE_HAL_USB="FALSE"
        ENABLE_HAL_SERIAL_USB="FALSE"
        ENABLE_USB_NO_VBUS_SENSE="FALSE"
        ENABLE_BRINGUP_SMOKE="FALSE"
        ENABLE_BRINGUP_HARDALIVE="FALSE"
        ENABLE_FEATURE_RTC="ON"
        ENABLE_HAL_RTC="TRUE"
        PROFILE_STATUS="scaffold"
        PROFILE_NOTE="Native W5500 transport scaffold (System.Net/lwIP disabled)"
        ;;
    bringup-smoke)
        ENABLE_SYSTEM_NET="OFF"
        ENABLE_CONFIG_BLOCK="OFF"
        ENABLE_SNTP="OFF"
        ENABLE_MBEDTLS="OFF"
        ENABLE_HAL_MAC="FALSE"
        ENABLE_STM32_MAC_ETH="FALSE"
        ENABLE_OTG1="FALSE"
        ENABLE_OTG2="FALSE"
        ENABLE_HAL_USB="FALSE"
        ENABLE_HAL_SERIAL_USB="FALSE"
        ENABLE_USB_NO_VBUS_SENSE="FALSE"
        ENABLE_BRINGUP_SMOKE="TRUE"
        ENABLE_BRINGUP_HARDALIVE="FALSE"
        ENABLE_FEATURE_RTC="OFF"
        ENABLE_HAL_RTC="FALSE"
        ENABLE_HSI_PLL="0"
        PROFILE_STATUS="experimental"
        PROFILE_NOTE="Hardware bring-up smoke profile (PA2 blink + USART3 heartbeat)"
        ;;
    bringup-hardalive)
        ENABLE_SYSTEM_NET="OFF"
        ENABLE_CONFIG_BLOCK="OFF"
        ENABLE_SNTP="OFF"
        ENABLE_MBEDTLS="OFF"
        ENABLE_HAL_MAC="FALSE"
        ENABLE_STM32_MAC_ETH="FALSE"
        ENABLE_OTG1="FALSE"
        ENABLE_OTG2="FALSE"
        ENABLE_HAL_USB="FALSE"
        ENABLE_HAL_SERIAL_USB="FALSE"
        ENABLE_USB_NO_VBUS_SENSE="FALSE"
        ENABLE_BRINGUP_SMOKE="FALSE"
        ENABLE_BRINGUP_HARDALIVE="TRUE"
        ENABLE_FEATURE_RTC="OFF"
        ENABLE_HAL_RTC="FALSE"
        ENABLE_HSI_PLL="0"
        PROFILE_STATUS="experimental"
        PROFILE_NOTE="Bare-metal bring-up profile (PA2 + PB10 hard toggle, no RTOS/CLR startup)"
        ;;
    usb-first)
        ENABLE_SYSTEM_NET="OFF"
        ENABLE_CONFIG_BLOCK="OFF"
        ENABLE_SNTP="OFF"
        ENABLE_MBEDTLS="OFF"
        ENABLE_HAL_MAC="FALSE"
        ENABLE_STM32_MAC_ETH="FALSE"
        ENABLE_OTG1="TRUE"
        ENABLE_OTG2="FALSE"
        ENABLE_HAL_USB="TRUE"
        ENABLE_HAL_SERIAL_USB="TRUE"
        ENABLE_USB_NO_VBUS_SENSE="FALSE"
        ENABLE_BRINGUP_SMOKE="FALSE"
        ENABLE_BRINGUP_HARDALIVE="FALSE"
        ENABLE_FEATURE_RTC="ON"
        ENABLE_HAL_RTC="TRUE"
        PROFILE_STATUS="experimental"
        PROFILE_NOTE="USB-first bring-up profile (OTG1 enabled, UART wire protocol fallback retained)"
        ;;
    usb-no-vbus-sense)
        ENABLE_SYSTEM_NET="OFF"
        ENABLE_CONFIG_BLOCK="OFF"
        ENABLE_SNTP="OFF"
        ENABLE_MBEDTLS="OFF"
        ENABLE_HAL_MAC="FALSE"
        ENABLE_STM32_MAC_ETH="FALSE"
        ENABLE_OTG1="TRUE"
        ENABLE_OTG2="FALSE"
        ENABLE_HAL_USB="TRUE"
        ENABLE_HAL_SERIAL_USB="TRUE"
        ENABLE_USB_NO_VBUS_SENSE="TRUE"
        ENABLE_BRINGUP_SMOKE="FALSE"
        ENABLE_BRINGUP_HARDALIVE="FALSE"
        ENABLE_FEATURE_RTC="ON"
        ENABLE_HAL_RTC="TRUE"
        PROFILE_STATUS="experimental"
        PROFILE_NOTE="USB bring-up profile for boards without PA9 VBUS_SENSE wiring (forces PA9 pulldown)"
        ;;
    network)
        ENABLE_SYSTEM_NET="ON"
        ENABLE_CONFIG_BLOCK="ON"
        ENABLE_SNTP="OFF"
        ENABLE_MBEDTLS="OFF"
        ENABLE_HAL_MAC="TRUE"
        ENABLE_STM32_MAC_ETH="TRUE"
        ENABLE_OTG1="FALSE"
        ENABLE_OTG2="FALSE"
        ENABLE_HAL_USB="FALSE"
        ENABLE_HAL_SERIAL_USB="FALSE"
        ENABLE_USB_NO_VBUS_SENSE="FALSE"
        ENABLE_BRINGUP_SMOKE="FALSE"
        ENABLE_BRINGUP_HARDALIVE="FALSE"
        ENABLE_FEATURE_RTC="ON"
        ENABLE_HAL_RTC="TRUE"
        PROFILE_STATUS="deprecated"
        PROFILE_NOTE="Temporary compatibility profile; scheduled for removal after native W5500 path is validated"
        ;;
    *)
        echo -e "${RED}Unknown NF_BUILD_PROFILE='$BUILD_PROFILE'. Use 'minimal', 'w5500-native', 'bringup-smoke', 'bringup-hardalive', 'usb-first', 'usb-no-vbus-sense', or 'network'.${NC}"
        exit 1
        ;;
esac

echo -e "${YELLOW}Build profile: ${BUILD_PROFILE}${NC}"
echo -e "${YELLOW}Profile note: ${PROFILE_NOTE}${NC}"
echo -e "${YELLOW}Target board: ${TARGET_NAME}${NC}"

if [ "$PROFILE_STATUS" = "deprecated" ]; then
    echo -e "${YELLOW}WARNING: 'network' profile is deprecated and will be removed after native W5500 transport validation.${NC}"
fi

# Check if nf-interpreter exists
if [ ! -d "$NF_INTERPRETER_DIR/.git" ]; then
    echo -e "${YELLOW}Cloning nf-interpreter repository...${NC}"
    git clone --recursive $NF_INTERPRETER_REPO $NF_INTERPRETER_DIR
    cd $NF_INTERPRETER_DIR
    git fetch --all --tags --prune || true
    git checkout "$NF_INTERPRETER_REF"
    git submodule update --init --recursive
else
    echo -e "${GREEN}nf-interpreter already exists${NC}"

    # Optionally update existing clone. Skipped by default for faster local rebuilds.
    cd "$NF_INTERPRETER_DIR"
    git checkout -- CMake/Modules/FindChibiOS.cmake src/CLR/Core/TypeSystem.cpp src/CLR/Startup/CLRStartup.cpp || true
    if [ "$UPDATE_INTERPRETER" = "1" ]; then
        echo -e "${YELLOW}Updating nf-interpreter (NF_UPDATE_INTERPRETER=1)...${NC}"
        git fetch --all --tags --prune || true
        git checkout "$NF_INTERPRETER_REF"
        git pull --ff-only || true
        git submodule update --init --recursive || true
    else
        echo -e "${YELLOW}Skipping nf-interpreter update (NF_UPDATE_INTERPRETER=0).${NC}"
        git fetch --tags --prune || true
        git checkout "$NF_INTERPRETER_REF"
        git submodule update --init --recursive || true
    fi

    if ! git rev-parse --verify --quiet "$NF_INTERPRETER_REF" >/dev/null; then
        echo -e "${RED}Unable to verify nf-interpreter ref '$NF_INTERPRETER_REF'.${NC}"
        exit 1
    fi

    CURRENT_REF="$(git rev-parse --short HEAD)"
    echo -e "${YELLOW}Using nf-interpreter ref: $NF_INTERPRETER_REF ($CURRENT_REF)${NC}"
fi

# Keep the generated nanoCLR target header globally visible for CLR support
# libraries, but do not export the nanoBooter target header globally. When
# both are present the shared CLR sources can resolve the wrong target_board.h
# based on include order.
echo -e "${YELLOW}Patching ChibiOS include path leakage...${NC}"
sed -i '\|list(APPEND CHIBIOS_INCLUDE_DIRS ${CMAKE_BINARY_DIR}/targets/ChibiOS/${TARGET_BOARD}/nanoBooter)|d' \
    "$NF_INTERPRETER_DIR/CMake/Modules/FindChibiOS.cmake"

# Older interpreter refs contain a typo that seeds CHIBIOS source lookup with
# an invalid NOTFOUND token, which later propagates to target_sources().
sed -i 's/set(CHIBIOS_SRC_FILE SRC_FILE -NOTFOUND)/set(CHIBIOS_SRC_FILE SRC_FILE-NOTFOUND)/' \
    "$NF_INTERPRETER_DIR/CMake/Modules/FindChibiOS.cmake"

# ChibiOS stable_21.11.x provides newlib syscall stubs under
# os/various/newlib_bindings; older FindChibiOS snapshots only search
# os/various (non-recursive) and miss this path.
if ! grep -Fq '/os/various/newlib_bindings' "$NF_INTERPRETER_DIR/CMake/Modules/FindChibiOS.cmake"; then
    sed -i '/${chibios_SOURCE_DIR}\/os\/various/a\            ${chibios_SOURCE_DIR}/os/various/newlib_bindings' \
        "$NF_INTERPRETER_DIR/CMake/Modules/FindChibiOS.cmake"
fi

# Upstream _nanoCLR CMake appends into INTERNAL cache lists without clearing.
# On repeated configure runs this can retain stale sources (e.g. watchdog) even
# after options are turned off. Force-reset those lists each configure.
if ! grep -Fq 'reset nanoCLR source cache lists' "$NF_INTERPRETER_DIR/targets/ChibiOS/_nanoCLR/CMakeLists.txt"; then
    sed -i '/# append nanoHAL/i \
# reset nanoCLR source cache lists\
set(TARGET_CHIBIOS_NANOCLR_SOURCES "" CACHE INTERNAL "reset nanoCLR sources" FORCE)\
set(TARGET_CHIBIOS_NANOCLR_INCLUDE_DIRS "" CACHE INTERNAL "reset nanoCLR includes" FORCE)\
' "$NF_INTERPRETER_DIR/targets/ChibiOS/_nanoCLR/CMakeLists.txt"
fi

if grep -Fq 'list(APPEND CHIBIOS_INCLUDE_DIRS ${CMAKE_BINARY_DIR}/targets/ChibiOS/${TARGET_BOARD}/nanoBooter)' \
    "$NF_INTERPRETER_DIR/CMake/Modules/FindChibiOS.cmake"; then
    echo -e "${RED}Failed to remove leaked nanoBooter ChibiOS include path.${NC}"
    exit 1
fi

if ! grep -Fq 'list(APPEND CHIBIOS_INCLUDE_DIRS ${CMAKE_BINARY_DIR}/targets/ChibiOS/${TARGET_BOARD}/nanoCLR)' \
    "$NF_INTERPRETER_DIR/CMake/Modules/FindChibiOS.cmake"; then
    echo -e "${RED}Missing required nanoCLR ChibiOS include path.${NC}"
    exit 1
fi

echo -e "${YELLOW}Using upstream CLR assembly loader (no legacy runtime patching).${NC}"

# Create target directory structure
echo -e "${YELLOW}Setting up target directory...${NC}"
TARGET_DIR="$NF_INTERPRETER_DIR/targets/ChibiOS/$TARGET_NAME"

# Recreate target directory on each run to avoid stale config/header files
# persisting in the Docker volume between builds.
rm -rf "$TARGET_DIR"
mkdir -p "$TARGET_DIR"
mkdir -p "$TARGET_DIR/nanoCLR"
mkdir -p "$TARGET_DIR/nanoBooter"
mkdir -p "$TARGET_DIR/common"

# Copy board files
echo -e "${YELLOW}Copying board configuration files...${NC}"
cp /work/nf-native/board_diseqc.h $TARGET_DIR/
cp /work/nf-native/board_diseqc.h $TARGET_DIR/board.h
cp /work/nf-native/board_diseqc.cpp $TARGET_DIR/board.c
cp /work/nf-native/diseqc_native.h $TARGET_DIR/common/
cp /work/nf-native/diseqc_native.cpp $TARGET_DIR/common/
cp /work/nf-native/lnb_control.h $TARGET_DIR/common/
cp /work/nf-native/lnb_control.cpp $TARGET_DIR/common/
cp /work/nf-native/diseqc_interop.cpp $TARGET_DIR/nanoCLR/
cp /work/nf-native/lnb_interop.cpp $TARGET_DIR/nanoCLR/
cp /work/nf-native/w5500_interop.cpp $TARGET_DIR/nanoCLR/

# Select wire-protocol transport at board level to avoid SERIAL_DRIVER
# redefinition conflicts when USB serial is enabled.
if [ "$ENABLE_HAL_SERIAL_USB" = "TRUE" ] && [ -f "$TARGET_DIR/board.h" ]; then
    sed -E -i 's/^#define[[:space:]]+SERIAL_DRIVER[[:space:]]+SD3/#define SERIAL_DRIVER               SDU1/' "$TARGET_DIR/board.h"
fi

# Hardware revision without VBUS_SENSE connected: force PA9 to pulldown so
# USB FS VBUS input does not float during bring-up.
if [ "$ENABLE_USB_NO_VBUS_SENSE" = "TRUE" ] && [ -f "$TARGET_DIR/board.h" ]; then
    tmp_board_h="$(mktemp)"
    awk '
        BEGIN { skip_moder = 0; skip_pupdr = 0 }

        /^#define VAL_GPIOA_MODER/ {
            print "#define VAL_GPIOA_MODER             (PIN_MODE_OUTPUT(GPIOA_PIN2) |              \\";
            print "                                     PIN_MODE_ALTERNATE(GPIOA_PIN8) |           \\";
            print "                                     PIN_MODE_ANALOG(GPIOA_PIN9) |              \\";
            print "                                     PIN_MODE_ALTERNATE(GPIOA_PIN13) |          \\";
            print "                                     PIN_MODE_ALTERNATE(GPIOA_PIN14))";
            skip_moder = 1;
            next;
        }

        skip_moder == 1 {
            skip_moder = 0;
            next;
        }

        /^#define VAL_GPIOA_PUPDR/ {
            print "#define VAL_GPIOA_PUPDR             (PIN_PUPDR_FLOATING(GPIOA_PIN2) |           \\";
            print "                                     PIN_PUPDR_PULLUP(GPIOA_PIN8) |             \\";
            print "                                     PIN_PUPDR_PULLDOWN(GPIOA_PIN9))";
            skip_pupdr = 1;
            next;
        }

        skip_pupdr == 1 {
            if ($0 ~ /GPIOA_PIN8\)\)/) {
                skip_pupdr = 0;
            }
            next;
        }

        { print }
    ' "$TARGET_DIR/board.h" > "$tmp_board_h"
    mv "$tmp_board_h" "$TARGET_DIR/board.h"
fi

# Copy required ChibiOS target config files from local overrides first,
# then fill missing files from an STM32F4 reference target.
LOCAL_TARGET_OVERRIDES_DIR="/work/nf-native/target-overrides"

copy_if_absent() {
    local src="$1"
    local dst="$2"

    if [ -f "$src" ] && [ ! -f "$dst" ]; then
        cp "$src" "$dst"
    fi
}

copy_glob_if_absent() {
    local pattern="$1"
    local dst_dir="$2"
    local src

    for src in $pattern; do
        [ -f "$src" ] || continue
        copy_if_absent "$src" "$dst_dir/$(basename "$src")"
    done
}

mkdir -p "$TARGET_DIR/common" "$TARGET_DIR/nanoCLR" "$TARGET_DIR/nanoBooter"

# Force use of upstream/reference nanoHAL implementation.
# A previously copied local nanoHAL.cpp can persist across builds in this
# target tree and unexpectedly override debugger/wire-protocol behavior.
rm -f "$TARGET_DIR/nanoCLR/nanoHAL.cpp"

if [ -d "$LOCAL_TARGET_OVERRIDES_DIR" ]; then
    echo -e "${YELLOW}Applying local target overrides from $LOCAL_TARGET_OVERRIDES_DIR...${NC}"

    # Core target configuration sources used by API find modules.
    cp "$LOCAL_TARGET_OVERRIDES_DIR"/target_*.c "$TARGET_DIR/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR"/target_*.h "$TARGET_DIR/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR"/target_*.cpp "$TARGET_DIR/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/target_common.h.in" "$TARGET_DIR/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/common/Device_BlockStorage.c" "$TARGET_DIR/common/" 2>/dev/null || true
    if [ "$ENABLE_HAL_SERIAL_USB" = "TRUE" ]; then
        cp "$LOCAL_TARGET_OVERRIDES_DIR/common/usbcfg.c" "$TARGET_DIR/common/" 2>/dev/null || true
        cp "$LOCAL_TARGET_OVERRIDES_DIR/common/usbcfg.h" "$TARGET_DIR/common/" 2>/dev/null || true
    fi

    # ChibiOS/HAL configuration files.
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoCLR/halconf.h" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoCLR/halconf_nf.h" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoCLR/chconf.h" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoCLR/main.c" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoCLR/target_board.h.in" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoCLR/STM32F407xG_CLR.ld" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoBooter/halconf.h" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoBooter/halconf_nf.h" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoBooter/chconf.h" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoBooter/target_board.h.in" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoBooter/STM32F407xG_booter.ld" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true
    cp "$LOCAL_TARGET_OVERRIDES_DIR/nanoBooter/main.c" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true
fi

REFERENCE_BOARD=""
for candidate in \
    "$NF_INTERPRETER_DIR/targets-community/ChibiOS/ST_STM32F4_DISCOVERY" \
    "$NF_INTERPRETER_DIR/targets/ChibiOS/ST_STM32F429I_DISCOVERY"; do
    if [ -d "$candidate" ]; then
        REFERENCE_BOARD="$candidate"
        break
    fi
done

if [ -n "$REFERENCE_BOARD" ]; then
    echo -e "${YELLOW}Backfilling missing target files from reference: $REFERENCE_BOARD${NC}"

    # Core target configuration sources used by API find modules.
    copy_glob_if_absent "$REFERENCE_BOARD/target_*.c" "$TARGET_DIR"
    copy_glob_if_absent "$REFERENCE_BOARD/target_*.h" "$TARGET_DIR"
    copy_glob_if_absent "$REFERENCE_BOARD/target_*.cpp" "$TARGET_DIR"
    copy_if_absent "$REFERENCE_BOARD/target_common.h.in" "$TARGET_DIR/target_common.h.in"
    copy_if_absent "$REFERENCE_BOARD/common/Device_BlockStorage.c" "$TARGET_DIR/common/Device_BlockStorage.c"
    if [ "$ENABLE_HAL_SERIAL_USB" = "TRUE" ]; then
        copy_if_absent "$REFERENCE_BOARD/common/usbcfg.c" "$TARGET_DIR/common/usbcfg.c"
        copy_if_absent "$REFERENCE_BOARD/common/usbcfg.h" "$TARGET_DIR/common/usbcfg.h"
    fi

    # ChibiOS/HAL configuration files.
    copy_if_absent "$REFERENCE_BOARD/nanoCLR/halconf.h" "$TARGET_DIR/nanoCLR/halconf.h"
    copy_if_absent "$REFERENCE_BOARD/nanoCLR/halconf_nf.h" "$TARGET_DIR/nanoCLR/halconf_nf.h"
    copy_if_absent "$REFERENCE_BOARD/nanoCLR/chconf.h" "$TARGET_DIR/nanoCLR/chconf.h"
    copy_if_absent "$REFERENCE_BOARD/nanoCLR/main.c" "$TARGET_DIR/nanoCLR/main.c"
    copy_if_absent "$REFERENCE_BOARD/nanoCLR/nanoHAL.cpp" "$TARGET_DIR/nanoCLR/nanoHAL.cpp"
    copy_if_absent "$REFERENCE_BOARD/nanoCLR/target_board.h.in" "$TARGET_DIR/nanoCLR/target_board.h.in"
    copy_if_absent "$REFERENCE_BOARD/nanoCLR/STM32F407xG_CLR.ld" "$TARGET_DIR/nanoCLR/STM32F407xG_CLR.ld"
    copy_if_absent "$REFERENCE_BOARD/nanoBooter/halconf.h" "$TARGET_DIR/nanoBooter/halconf.h"
    copy_if_absent "$REFERENCE_BOARD/nanoBooter/halconf_nf.h" "$TARGET_DIR/nanoBooter/halconf_nf.h"
    copy_if_absent "$REFERENCE_BOARD/nanoBooter/chconf.h" "$TARGET_DIR/nanoBooter/chconf.h"
    copy_if_absent "$REFERENCE_BOARD/nanoBooter/target_board.h.in" "$TARGET_DIR/nanoBooter/target_board.h.in"
    copy_if_absent "$REFERENCE_BOARD/nanoBooter/STM32F407xG_booter.ld" "$TARGET_DIR/nanoBooter/STM32F407xG_booter.ld"
    copy_if_absent "$REFERENCE_BOARD/nanoBooter/main.c" "$TARGET_DIR/nanoBooter/main.c"

    # Ensure linker scripts exist with the exact names expected by /work/build/CMakeLists.txt
    if [ ! -f "$TARGET_DIR/nanoCLR/STM32F407xG_CLR.ld" ]; then
        REF_CLR_LD="$(find "$REFERENCE_BOARD/nanoCLR" -maxdepth 1 -type f -name '*_CLR.ld' | head -n1)"
        if [ -n "$REF_CLR_LD" ]; then
            cp "$REF_CLR_LD" "$TARGET_DIR/nanoCLR/STM32F407xG_CLR.ld"
        fi
    fi

    if [ ! -f "$TARGET_DIR/nanoBooter/STM32F407xG_booter.ld" ]; then
        REF_BOOTER_LD="$(find "$REFERENCE_BOARD/nanoBooter" -maxdepth 1 -type f -name '*_booter.ld' | head -n1)"
        if [ -n "$REF_BOOTER_LD" ]; then
            cp "$REF_BOOTER_LD" "$TARGET_DIR/nanoBooter/STM32F407xG_booter.ld"
        fi
    fi

    # Use complete reference mcuconf as base to satisfy all STM32F4 required definitions
    if [ -f "$REFERENCE_BOARD/mcuconf.h" ]; then
        cp "$REFERENCE_BOARD/mcuconf.h" "$TARGET_DIR/mcuconf.h" 2>/dev/null || true
        cp "$REFERENCE_BOARD/mcuconf.h" "$TARGET_DIR/nanoCLR/mcuconf.h" 2>/dev/null || true
        cp "$REFERENCE_BOARD/mcuconf.h" "$TARGET_DIR/nanoBooter/mcuconf.h" 2>/dev/null || true
    elif [ -f "$REFERENCE_BOARD/nanoCLR/mcuconf.h" ]; then
        cp "$REFERENCE_BOARD/nanoCLR/mcuconf.h" "$TARGET_DIR/mcuconf.h" 2>/dev/null || true
        cp "$REFERENCE_BOARD/nanoCLR/mcuconf.h" "$TARGET_DIR/nanoCLR/mcuconf.h" 2>/dev/null || true
        cp "$REFERENCE_BOARD/nanoCLR/mcuconf.h" "$TARGET_DIR/nanoBooter/mcuconf.h" 2>/dev/null || true
    fi

    # If a workspace mcuconf is provided, use it as the base before appending
    # profile-specific overrides below.
    if [ -f /work/build/mcuconf.h ]; then
        cp /work/build/mcuconf.h "$TARGET_DIR/"
        cp /work/build/mcuconf.h "$TARGET_DIR/nanoCLR/"
        cp /work/build/mcuconf.h "$TARGET_DIR/nanoBooter/"
    fi

    # Apply board-specific peripheral usage overrides
    for mcu in "$TARGET_DIR/mcuconf.h" "$TARGET_DIR/nanoCLR/mcuconf.h" "$TARGET_DIR/nanoBooter/mcuconf.h"; do
        if [ -f "$mcu" ]; then
            cat >> "$mcu" << 'EOF_MCU_OVERRIDES'

#undef STM32_PWM_USE_TIM1
#define STM32_PWM_USE_TIM1                  FALSE
#undef STM32_PWM_USE_TIM4
#define STM32_PWM_USE_TIM4                  TRUE

#undef STM32_GPT_USE_TIM2
#define STM32_GPT_USE_TIM2                  FALSE
#undef STM32_GPT_USE_TIM5
#define STM32_GPT_USE_TIM5                  TRUE

#undef STM32_I2C_USE_I2C1
#define STM32_I2C_USE_I2C1                  TRUE

#undef STM32_SPI_USE_SPI1
#define STM32_SPI_USE_SPI1                  TRUE

#undef STM32_SERIAL_USE_USART2
#define STM32_SERIAL_USE_USART2             FALSE
#undef STM32_SERIAL_USE_USART3
#define STM32_SERIAL_USE_USART3             TRUE
EOF_MCU_OVERRIDES

            cat >> "$mcu" << EOF_MCU_PROFILE_OVERRIDES

#undef STM32_MAC_USE_ETH
#define STM32_MAC_USE_ETH                  ${ENABLE_STM32_MAC_ETH}

// RTC clock source: use internal LSI; this board does not require LSE.
// Keeps main system clock on HSE+PLL while avoiding RTC init hangs.
#undef STM32_LSI_ENABLED
#define STM32_LSI_ENABLED                  TRUE
#undef STM32_LSE_ENABLED
#define STM32_LSE_ENABLED                  FALSE
#undef STM32_RTCSEL
#define STM32_RTCSEL                       STM32_RTCSEL_LSI

EOF_MCU_PROFILE_OVERRIDES

            if [ "${ENABLE_HSI_PLL}" = "1" ]; then
                cat >> "$mcu" << 'EOF_MCU_HSI_PLL'
// HSI PLL override: HSE crystal absent/unverified.
// HSI=16MHz, PLLM=8 -> VCO_in=2MHz, PLLN=168, PLLP=2 -> SYSCLK=168MHz.
// APB1 (/4) = 42MHz, APB2 (/2) = 84MHz - matches reference clock tree.
#undef STM32_HSE_ENABLED
#define STM32_HSE_ENABLED                   FALSE
#undef STM32_LSE_ENABLED
#define STM32_LSE_ENABLED                   FALSE
#undef STM32_PLLSRC
#define STM32_PLLSRC                        STM32_PLLSRC_HSI
#undef STM32_PLLM_VALUE
#define STM32_PLLM_VALUE                    8
#undef STM32_PLLN_VALUE
#define STM32_PLLN_VALUE                    168
#undef STM32_PLLP_VALUE
#define STM32_PLLP_VALUE                    2
#undef STM32_PLLQ_VALUE
#define STM32_PLLQ_VALUE                    7
EOF_MCU_HSI_PLL
            fi
        fi
    done

    # Normalize CLR/deployment layout for STM32F407xG.
    # - Keep managed deployment at 0x080C0000 (256KB)
    # - Allocate remaining flash after booter to nanoCLR (752KB)
    if [ -f "$TARGET_DIR/nanoCLR/STM32F407xG_CLR.ld" ]; then
        sed -E -i \
            -e 's|^(\s*flash0\s*\(rx\)\s*:\s*org\s*=\s*0x08004000,\s*len\s*=\s*).*$|\1 1M - 16k - 256k     /* flash size less the space reserved for nanoBooter and application deployment*/|' \
            -e 's|^(\s*deployment\s*\(rx\)\s*:\s*org\s*=\s*)0x[0-9A-Fa-f]+,\s*len\s*=\s*[^ ]+.*$|\10x080C0000, len = 256k                /* space reserved for application deployment */|' \
            "$TARGET_DIR/nanoCLR/STM32F407xG_CLR.ld"
    fi

    # Keep block-storage regions aligned with the linker layout.
    # Reference STM32F4 Discovery storage map reserves deployment at 0x08040000,
    # but this target keeps nanoCLR up to 0x080C0000 and deploys into the last
    # two 128KB sectors (0x080C0000-0x080FFFFF).
    
    # Comprehensive block-storage patching: apply to Device_BlockStorage.c AND any target_*.c files
    BLOCK_STORAGE_PATCH_PATTERN='-E -i -e "s|\{[[:space:]]*BlockRange_BLOCKTYPE_CODE[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*\}[[:space:]]*,?[[:space:]]*//[[:space:]]*0x08020000[[:space:]]*nanoCLR|{BlockRange_BLOCKTYPE_CODE, 0, 4},      // 0x08020000 nanoCLR|" -e "s#\{[[:space:]]*BlockRange_BLOCKTYPE_DEPLOYMENT[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*\}[[:space:]]*,?[[:space:]]*//[[:space:]]*(0x08040000|0x080C0000)[[:space:]]*deployment#{BlockRange_BLOCKTYPE_DEPLOYMENT, 5, 6} // 0x080C0000 deployment#"'
    
    if [ -f "$TARGET_DIR/common/Device_BlockStorage.c" ]; then
        sed -E -i \
            -e 's|\{[[:space:]]*BlockRange_BLOCKTYPE_CODE[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*\}[[:space:]]*,?[[:space:]]*//[[:space:]]*0x08020000[[:space:]]*nanoCLR|{BlockRange_BLOCKTYPE_CODE, 0, 4},      // 0x08020000 nanoCLR|' \
            -e 's#\{[[:space:]]*BlockRange_BLOCKTYPE_DEPLOYMENT[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*\}[[:space:]]*,?[[:space:]]*//[[:space:]]*(0x08040000|0x080C0000)[[:space:]]*deployment#{BlockRange_BLOCKTYPE_DEPLOYMENT, 5, 6} // 0x080C0000 deployment#' \
            "$TARGET_DIR/common/Device_BlockStorage.c"

        if ! grep -Eq '\{BlockRange_BLOCKTYPE_CODE,[[:space:]]*0,[[:space:]]*4\}' "$TARGET_DIR/common/Device_BlockStorage.c"; then
            echo -e "${RED}Device_BlockStorage.c patch failed: expected CODE range 0..4 was not found.${NC}"
            exit 1
        fi

        if ! grep -Eq '\{BlockRange_BLOCKTYPE_DEPLOYMENT,[[:space:]]*5,[[:space:]]*6\}' "$TARGET_DIR/common/Device_BlockStorage.c"; then
            echo -e "${RED}Device_BlockStorage.c patch failed: expected DEPLOYMENT range 5..6 was not found.${NC}"
            exit 1
        fi
    fi
    
    # Also patch any target_*.c files that may contain BlockRange definitions
    for target_storage_file in "$TARGET_DIR"/target_*.c; do
        if [ -f "$target_storage_file" ] && grep -q "BlockRange_BLOCKTYPE" "$target_storage_file"; then
            echo -e "${YELLOW}Patching block-storage in $(basename $target_storage_file)...${NC}"
            sed -E -i \
                -e 's|\{[[:space:]]*BlockRange_BLOCKTYPE_CODE[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*\}[[:space:]]*,?[[:space:]]*//[[:space:]]*0x08020000[[:space:]]*nanoCLR|{BlockRange_BLOCKTYPE_CODE, 0, 4},      // 0x08020000 nanoCLR|' \
                -e 's#\{[[:space:]]*BlockRange_BLOCKTYPE_DEPLOYMENT[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*,[[:space:]]*[0-9]+[[:space:]]*\}[[:space:]]*,?[[:space:]]*//[[:space:]]*(0x08040000|0x080C0000)[[:space:]]*deployment#{BlockRange_BLOCKTYPE_DEPLOYMENT, 5, 6} // 0x080C0000 deployment#' \
                "$target_storage_file"
        fi
    done

    # Provide a deterministic wire-protocol serial configuration header.
    # Do not inherit reference-board SERIAL_DRIVER values.
    if [ "$ENABLE_HAL_SERIAL_USB" = "TRUE" ]; then
        cat > "$TARGET_DIR/common/serialcfg.h" << 'EOF_SERIALCFG_USB'
#ifndef SERIALCFG_H
#define SERIALCFG_H

#define SERIAL_DRIVER           SDU1

#endif // SERIALCFG_H
EOF_SERIALCFG_USB
    else
        cat > "$TARGET_DIR/common/serialcfg.h" << 'EOF_SERIALCFG_UART'
#ifndef SERIALCFG_H
#define SERIALCFG_H

#define SERIAL_DRIVER           SD3

#endif // SERIALCFG_H
EOF_SERIALCFG_UART
    fi
fi

if [ ! -d "$REFERENCE_BOARD" ]; then
    echo -e "${RED}Reference board files not found in expected locations.${NC}"
    echo -e "${RED}Checked:${NC}"
    echo -e "${RED}  - $NF_INTERPRETER_DIR/targets-community/ChibiOS/ST_STM32F4_DISCOVERY${NC}"
    echo -e "${RED}  - $NF_INTERPRETER_DIR/targets/ChibiOS/ST_STM32F429I_DISCOVERY${NC}"
    exit 1
fi

if [ ! -f "$TARGET_DIR/nanoCLR/STM32F407xG_CLR.ld" ] || [ ! -f "$TARGET_DIR/nanoBooter/STM32F407xG_booter.ld" ]; then
    echo -e "${RED}Missing required linker scripts after reference copy.${NC}"
    echo -e "${RED}Expected:${NC}"
    echo -e "${RED}  - $TARGET_DIR/nanoCLR/STM32F407xG_CLR.ld${NC}"
    echo -e "${RED}  - $TARGET_DIR/nanoBooter/STM32F407xG_booter.ld${NC}"
    exit 1
fi

if [ "$ENABLE_HAL_SERIAL_USB" = "TRUE" ]; then
    echo -e "${YELLOW}USB serial profile: keeping reference USB nanoCLR main.${NC}"

    # Reference nanoBooter mains often depend on board-specific button/LED macros.
    # Use this target's minimal booter main to keep boot path board-agnostic.
    if [ -f /work/build/nanoBooter_main.c ]; then
        cp /work/build/nanoBooter_main.c "$TARGET_DIR/nanoBooter/main.c"
    fi
else
    if [ -f /work/build/nanoBooter_main.c ]; then
        cp /work/build/nanoBooter_main.c "$TARGET_DIR/nanoBooter/main.c"
    fi

    if [ "$ENABLE_BRINGUP_HARDALIVE" = "TRUE" ] && [ -f /work/build/nanoCLR_hardalive_main.c ]; then
        cp /work/build/nanoCLR_hardalive_main.c "$TARGET_DIR/nanoCLR/main.c"
    elif [ "$ENABLE_BRINGUP_SMOKE" = "TRUE" ] && [ -f /work/build/nanoCLR_bringup_main.c ]; then
        cp /work/build/nanoCLR_bringup_main.c "$TARGET_DIR/nanoCLR/main.c"
    elif [ -f /work/build/nanoCLR_main.c ]; then
        cp /work/build/nanoCLR_main.c "$TARGET_DIR/nanoCLR/main.c"
    fi
fi

# Ensure target subdirectory CMakeLists exist and include custom sources
cat > "$TARGET_DIR/common/CMakeLists.txt" << 'EOF_COMMON_CMAKE'
#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

# keep file present for target layout parity
EOF_COMMON_CMAKE

cat > "$TARGET_DIR/nanoCLR/CMakeLists.txt" << 'EOF_NANOCLR_CMAKE'
#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/main.c")
list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/nanoHAL.cpp")
list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/diseqc_interop.cpp")
list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/lnb_interop.cpp")
list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/w5500_interop.cpp")
list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../common/diseqc_native.cpp")
list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../common/lnb_control.cpp")
list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../common/Device_BlockStorage.c")
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/../common/usbcfg.c")
    list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../common/usbcfg.c")
endif()
set(NANOCLR_PROJECT_SOURCES ${NANOCLR_PROJECT_SOURCES} CACHE INTERNAL "make global")
EOF_NANOCLR_CMAKE

cat > "$TARGET_DIR/nanoBooter/CMakeLists.txt" << 'EOF_NANOBOOTER_CMAKE'
#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

# append nanoBooter source files
list(APPEND NANOBOOTER_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/main.c")
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/../common/usbcfg.c")
    list(APPEND NANOBOOTER_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../common/usbcfg.c")
endif()
# make var global
set(NANOBOOTER_PROJECT_SOURCES ${NANOBOOTER_PROJECT_SOURCES} CACHE INTERNAL "make global")
EOF_NANOBOOTER_CMAKE

# nf-interpreter main uses Kconfig to generate nf_config.h, which is included
# by generated target_os.h. Provide a board-specific defconfig for this custom
# target so the header is always produced.
cat > "$TARGET_DIR/defconfig" << EOF_DEFCONFIG
# defconfig for $TARGET_NAME (generated by toolchain/build.sh)
CONFIG_RTOS_CHIBIOS=y
CONFIG_TARGET_BOARD="$TARGET_NAME"
CONFIG_TARGET_SERIES="STM32F4xx"
CONFIG_NF_FEATURE_DEBUGGER=y
CONFIG_NF_DEBUG_ASSERT=y
CONFIG_API_HARDWARE_STM32=y
CONFIG_API_SYSTEM_DEVICE_GPIO=y
CONFIG_API_SYSTEM_DEVICE_I2C=y
CONFIG_API_SYSTEM_DEVICE_SPI=y
CONFIG_API_SYSTEM_MATH=y
CONFIG_API_NANOFRAMEWORK_RUNTIME_EVENTS=y
CONFIG_API_SYSTEM_RUNTIME_SERIALIZATION=y
# CONFIG_NF_FEATURE_WATCHDOG is not set
EOF_DEFCONFIG

if [ "$ENABLE_SYSTEM_NET" = "ON" ]; then
    echo "CONFIG_API_SYSTEM_NET=y" >> "$TARGET_DIR/defconfig"
else
    echo "# CONFIG_API_SYSTEM_NET is not set" >> "$TARGET_DIR/defconfig"
fi

if [ "$ENABLE_CONFIG_BLOCK" = "ON" ]; then
    echo "CONFIG_NF_FEATURE_HAS_CONFIG_BLOCK=y" >> "$TARGET_DIR/defconfig"
else
    echo "# CONFIG_NF_FEATURE_HAS_CONFIG_BLOCK is not set" >> "$TARGET_DIR/defconfig"
fi

if [ "$ENABLE_FEATURE_RTC" = "ON" ]; then
    echo "CONFIG_NF_FEATURE_RTC=y" >> "$TARGET_DIR/defconfig"
else
    echo "# CONFIG_NF_FEATURE_RTC is not set" >> "$TARGET_DIR/defconfig"
fi

if [ "$ENABLE_SNTP" = "ON" ]; then
    echo "CONFIG_NF_NETWORKING_SNTP=y" >> "$TARGET_DIR/defconfig"
else
    echo "# CONFIG_NF_NETWORKING_SNTP is not set" >> "$TARGET_DIR/defconfig"
fi

if [ "$ENABLE_MBEDTLS" = "ON" ]; then
    echo "CONFIG_NF_SECURITY_MBEDTLS=y" >> "$TARGET_DIR/defconfig"
else
    echo "# CONFIG_NF_SECURITY_MBEDTLS is not set" >> "$TARGET_DIR/defconfig"
fi

# Copy CMake file override (if provided)
if [ -f /work/build/CMakeLists.txt ]; then
    cp /work/build/CMakeLists.txt $TARGET_DIR/
fi

# Ensure HAL settings match this board capabilities in both firmware images
for cfg in "$TARGET_DIR/nanoCLR/halconf.h" "$TARGET_DIR/nanoBooter/halconf.h"; do
    if [ -f "$cfg" ]; then
        RTC_HAL_SETTING="$ENABLE_HAL_RTC"
        if [[ "$cfg" == *"/nanoBooter/"* ]]; then
            RTC_HAL_SETTING="FALSE"
        fi

        # ChibiOS 9.x requires these markers in halconf.h.
        if ! grep -q "_CHIBIOS_HAL_CONF_" "$cfg"; then
            sed -i '/#define HALCONF_H/a #define _CHIBIOS_HAL_CONF_' "$cfg"
        fi
        if ! grep -q "_CHIBIOS_HAL_CONF_VER_9_1_" "$cfg"; then
            sed -i '/#define _CHIBIOS_HAL_CONF_/a #define _CHIBIOS_HAL_CONF_VER_9_1_' "$cfg"
        fi

        cat >> "$cfg" << EOF_HAL_OVERRIDES

#undef HAL_USE_USB
#define HAL_USE_USB                         ${ENABLE_HAL_USB}
#undef HAL_USE_SERIAL_USB
#define HAL_USE_SERIAL_USB                  ${ENABLE_HAL_SERIAL_USB}
#undef HAL_USE_SERIAL
#define HAL_USE_SERIAL                      TRUE
#undef HAL_USE_GPT
#define HAL_USE_GPT                         TRUE
#undef HAL_USE_PWM
#define HAL_USE_PWM                         TRUE
#undef HAL_USE_PAL
#define HAL_USE_PAL                         TRUE
#undef HAL_USE_RTC
#define HAL_USE_RTC                         ${RTC_HAL_SETTING}
#undef HAL_USE_MAC
#define HAL_USE_MAC                         ${ENABLE_HAL_MAC}
#undef HAL_USE_WDG
#define HAL_USE_WDG                         FALSE
EOF_HAL_OVERRIDES
    fi
done

# Ensure USB OTG usage flags match selected build profile
for mcu in "$TARGET_DIR/mcuconf.h" "$TARGET_DIR/nanoCLR/mcuconf.h" "$TARGET_DIR/nanoBooter/mcuconf.h"; do
    if [ -f "$mcu" ]; then
        cat >> "$mcu" << EOF_MCU_USB_OVERRIDES

#undef STM32_USB_USE_OTG1
#define STM32_USB_USE_OTG1                  ${ENABLE_OTG1}
#undef STM32_USB_USE_OTG2
#define STM32_USB_USE_OTG2                  ${ENABLE_OTG2}
EOF_MCU_USB_OVERRIDES

        if [ "${ENABLE_HSI_PLL}" = "1" ]; then
            cat >> "$mcu" << 'EOF_MCU_HSI_PLL_FINAL'
// HSI PLL override: HSE crystal absent/unverified.
// HSI=16MHz, PLLM=8 -> VCO_in=2MHz, PLLN=168, PLLP=2 -> SYSCLK=168MHz.
// APB1 (/4) = 42MHz, APB2 (/2) = 84MHz - matches reference clock tree.
#undef STM32_HSE_ENABLED
#define STM32_HSE_ENABLED                   FALSE
#undef STM32_LSE_ENABLED
#define STM32_LSE_ENABLED                   FALSE
#undef STM32_PLLSRC
#define STM32_PLLSRC                        STM32_PLLSRC_HSI
#undef STM32_PLLM_VALUE
#define STM32_PLLM_VALUE                    8
#undef STM32_PLLN_VALUE
#define STM32_PLLN_VALUE                    168
#undef STM32_PLLP_VALUE
#define STM32_PLLP_VALUE                    2
#undef STM32_PLLQ_VALUE
#define STM32_PLLQ_VALUE                    7
EOF_MCU_HSI_PLL_FINAL
        fi
    fi
done

# Workaround for ChibiOS common network close path that assumes internal STM32 MAC (ETHD1).
# This target uses external W5500 over SPI.
if [ "$BUILD_PROFILE" = "network" ] && [ -f "$NF_INTERPRETER_DIR/targets/ChibiOS/_common/Target_Network.cpp" ]; then
    sed -i 's/^[[:space:]]*macStop(&ETHD1);[[:space:]]*$/            \/\/ W5500 profile: no internal MAC stop required/' \
        "$NF_INTERPRETER_DIR/targets/ChibiOS/_common/Target_Network.cpp"
fi

# Create build directory
echo -e "${YELLOW}Creating build directory...${NC}"
BUILD_DIR="$NF_INTERPRETER_DIR/build/$TARGET_NAME"
mkdir -p $BUILD_DIR
cd $BUILD_DIR

# Full clean when switching build profiles to prevent incremental build contamination.
# Switching profiles changes the linker script layout, entry-point binary size, and
# CMake option flags; cached object files from a previous profile produce corrupt
# binaries whose reset-handler vectors point outside the new binary.
PROFILE_MARKER_FILE="$BUILD_DIR/.last_profile"
LAST_PROFILE=""
if [ -f "$PROFILE_MARKER_FILE" ]; then
    LAST_PROFILE=$(cat "$PROFILE_MARKER_FILE")
fi
if [ "$LAST_PROFILE" != "$BUILD_PROFILE" ]; then
    if [ -n "$LAST_PROFILE" ]; then
        echo -e "${YELLOW}Build profile changed from '$LAST_PROFILE' to '$BUILD_PROFILE' — full clean.${NC}"
    else
        echo -e "${YELLOW}No previous profile marker — full clean for safety.${NC}"
    fi
    rm -rf "$BUILD_DIR"
    mkdir -p "$BUILD_DIR"
    cd $BUILD_DIR
fi
echo "$BUILD_PROFILE" > "$PROFILE_MARKER_FILE"

# Keep CMake cache for incremental builds when profile is unchanged.
if [ "$INCREMENTAL_BUILD" = "0" ]; then
    echo -e "${YELLOW}NF_INCREMENTAL_BUILD=0 -> clearing CMake cache.${NC}"
    rm -f CMakeCache.txt
    rm -rf CMakeFiles
fi

# Clear ChibiOS FetchContent state only on demand.
if [ "$CLEAN_FETCHCONTENT" = "1" ] && [ -d "$BUILD_DIR/_deps" ]; then
    echo -e "${YELLOW}NF_CLEAN_FETCHCONTENT=1 -> cleaning ChibiOS fetch state...${NC}"
    for wc in "$BUILD_DIR"/_deps/chibios-src*; do
        if [ -d "$wc/.svn" ]; then
            svn cleanup "$wc" >/dev/null 2>&1 || true
        fi
    done
    rm -rf "$BUILD_DIR"/_deps/chibios-src*
    rm -rf "$BUILD_DIR"/_deps/chibios-subbuild
fi

# Explicit toolchain configuration (required by some nf-interpreter CMake flows)
export CC=arm-none-eabi-gcc
export CXX=arm-none-eabi-g++
export ASM=arm-none-eabi-gcc
export SIZE=arm-none-eabi-size
export OBJDUMP=arm-none-eabi-objdump
export NM=arm-none-eabi-nm

# Configure with CMake
echo -e "${YELLOW}Configuring with CMake...${NC}"
cmake -G Ninja \
    -DCMAKE_BUILD_TYPE=$BUILD_TYPE \
    -DCMAKE_TRY_COMPILE_TARGET_TYPE=STATIC_LIBRARY \
    -DCMAKE_C_COMPILER=arm-none-eabi-gcc \
    -DCMAKE_CXX_COMPILER=arm-none-eabi-g++ \
    -DCMAKE_ASM_COMPILER=arm-none-eabi-gcc \
    -DCMAKE_SIZE=arm-none-eabi-size \
    -DCMAKE_OBJDUMP=arm-none-eabi-objdump \
    -DCMAKE_NM=arm-none-eabi-nm \
    -DTARGET_SERIES=STM32F4xx \
    -DRTOS=ChibiOS \
    -DTARGET_BOARD=$TARGET_NAME \
    -DNF_TARGET_DEFCONFIG=targets/ChibiOS/$TARGET_NAME/defconfig \
    -DSUPPORT_ANY_BASE_CONVERSION=OFF \
    -DNF_FEATURE_DEBUGGER=ON \
    -DNF_FEATURE_RTC=$ENABLE_FEATURE_RTC \
    -DNF_FEATURE_WATCHDOG=OFF \
    -DHAL_USE_WDG_OPTION=FALSE \
    -DNF_FEATURE_HAS_CONFIG_BLOCK=$ENABLE_CONFIG_BLOCK \
    -DAPI_System.Device.Gpio=ON \
    -DAPI_System.Device.Spi=ON \
    -DAPI_System.Device.I2c=ON \
    -DAPI_System.Net=$ENABLE_SYSTEM_NET \
    -DNF_SECURITY_MBEDTLS=$ENABLE_MBEDTLS \
    -DNF_NETWORKING_SNTP=$ENABLE_SNTP \
    -DUSE_RNG=OFF \
    $NF_INTERPRETER_DIR

# Workaround: some target setups emit a post-link dump command without tool path.
if [ -f "$BUILD_DIR/build.ninja" ]; then
    sed -i 's/&& -A -x /&& arm-none-eabi-size -A -x /g' "$BUILD_DIR/build.ninja"
    sed -i 's/ -A -x / arm-none-eabi-size -A -x /g' "$BUILD_DIR/build.ninja"
    sed -E -i "s|&& cd ${BUILD_DIR}/targets/ChibiOS/${TARGET_NAME} && .* -A -x [^ ]+|&& true|g" "$BUILD_DIR/build.ninja"
    sed -E -i 's|arm-none-eabi-size -A -x[^&]*|true|g' "$BUILD_DIR/build.ninja"
fi

# Build
echo -e "${YELLOW}Building firmware...${NC}"
echo -e "${YELLOW}Using $BUILD_JOBS parallel jobs (NF_BUILD_JOBS).${NC}"
cmake --build . --config $BUILD_TYPE --parallel "$BUILD_JOBS"

# Check if build succeeded
if [ -f "nanoCLR.bin" ]; then
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}Build SUCCESS!${NC}"
    echo -e "${GREEN}========================================${NC}"
    
    # Copy output to work directory
    cp nanoCLR.bin /work/build/nanoCLR.bin
    cp nanoCLR.hex /work/build/nanoCLR.hex
    cp nanoCLR.elf /work/build/nanoCLR.elf
    if [ -f "nanoBooter.bin" ]; then
        cp nanoBooter.bin /work/build/nanoBooter.bin
    fi
    if [ -f "nanoBooter.hex" ]; then
        cp nanoBooter.hex /work/build/nanoBooter.hex
    fi
    if [ -f "nanoBooter.elf" ]; then
        cp nanoBooter.elf /work/build/nanoBooter.elf
    fi
    
    echo -e "${GREEN}Firmware files copied to: /work/build/${NC}"
    if [ -f "nanoBooter.bin" ]; then
        echo -e "${GREEN}  - nanoBooter.bin${NC}"
        echo -e "${GREEN}  - nanoBooter.hex${NC}"
        echo -e "${GREEN}  - nanoBooter.elf${NC}"
    fi
    echo -e "${GREEN}  - nanoCLR.bin${NC}"
    echo -e "${GREEN}  - nanoCLR.hex${NC}"
    echo -e "${GREEN}  - nanoCLR.elf${NC}"
    
    echo ""
    echo "To flash to board:"
    if [ -f "nanoBooter.bin" ]; then
        echo "  st-flash write build/nanoBooter.bin 0x08000000"
    fi
    echo "  st-flash write build/nanoCLR.bin 0x08004000"
else
    echo -e "${RED}========================================${NC}"
    echo -e "${RED}Build FAILED${NC}"
    echo -e "${RED}========================================${NC}"
    exit 1
fi
