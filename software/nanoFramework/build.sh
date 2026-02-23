#!/bin/bash
# nanoFramework Build Script for DiSEqC Controller
# Builds firmware using Docker Compose V2
# Uses: docker compose (not docker-compose)

set -e  # Exit on error

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
TARGET_NAME="M0DMF_DISEQC_F407"
BUILD_TYPE="Release"
BUILD_PROFILE="${NF_BUILD_PROFILE:-minimal}"

case "$BUILD_PROFILE" in
    minimal)
        ENABLE_SYSTEM_NET="OFF"
        ENABLE_CONFIG_BLOCK="OFF"
        ENABLE_SNTP="OFF"
        ENABLE_MBEDTLS="OFF"
        ENABLE_HAL_MAC="FALSE"
        ENABLE_STM32_MAC_ETH="FALSE"
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
        PROFILE_STATUS="scaffold"
        PROFILE_NOTE="Native W5500 transport scaffold (System.Net/lwIP disabled)"
        ;;
    network)
        ENABLE_SYSTEM_NET="ON"
        ENABLE_CONFIG_BLOCK="ON"
        ENABLE_SNTP="OFF"
        ENABLE_MBEDTLS="OFF"
        ENABLE_HAL_MAC="TRUE"
        ENABLE_STM32_MAC_ETH="TRUE"
        PROFILE_STATUS="deprecated"
        PROFILE_NOTE="Temporary compatibility profile; scheduled for removal after native W5500 path is validated"
        ;;
    *)
        echo -e "${RED}Unknown NF_BUILD_PROFILE='$BUILD_PROFILE'. Use 'minimal', 'w5500-native', or 'network'.${NC}"
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
    git checkout develop
    git submodule update --init --recursive
else
    echo -e "${GREEN}nf-interpreter already exists${NC}"
fi

# Create target directory structure
echo -e "${YELLOW}Setting up target directory...${NC}"
TARGET_DIR="$NF_INTERPRETER_DIR/targets/ChibiOS/$TARGET_NAME"

if [ ! -d "$TARGET_DIR" ]; then
    mkdir -p $TARGET_DIR
    mkdir -p $TARGET_DIR/nanoCLR
    mkdir -p $TARGET_DIR/common
fi

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

# Copy required ChibiOS target config files from an STM32F4 reference target
REFERENCE_BOARD="$NF_INTERPRETER_DIR/targets-community/ChibiOS/ST_STM32F4_DISCOVERY"
if [ -d "$REFERENCE_BOARD" ]; then
    echo -e "${YELLOW}Copying reference target config files...${NC}"

    mkdir -p "$TARGET_DIR/nanoCLR" "$TARGET_DIR/nanoBooter"

    # Core target configuration sources used by API find modules
    cp "$REFERENCE_BOARD"/target_*.c "$TARGET_DIR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD"/target_*.h "$TARGET_DIR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD"/target_*.cpp "$TARGET_DIR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/target_common.h.in" "$TARGET_DIR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/common/Device_BlockStorage.c" "$TARGET_DIR/common/" 2>/dev/null || true

    # ChibiOS/HAL configuration files
    cp "$REFERENCE_BOARD/nanoCLR/halconf.h" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoCLR/halconf_nf.h" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoCLR/chconf.h" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoCLR/main.c" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoCLR/nanoHAL.cpp" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoCLR/target_board.h.in" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoCLR/STM32F407xG_CLR.ld" "$TARGET_DIR/nanoCLR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoBooter/halconf.h" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoBooter/halconf_nf.h" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoBooter/chconf.h" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoBooter/target_board.h.in" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoBooter/STM32F407xG_booter.ld" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoBooter/main.c" "$TARGET_DIR/nanoBooter/" 2>/dev/null || true

    # Use complete reference mcuconf as base to satisfy all STM32F4 required definitions
    cp "$REFERENCE_BOARD/mcuconf.h" "$TARGET_DIR/mcuconf.h" 2>/dev/null || true
    cp "$REFERENCE_BOARD/mcuconf.h" "$TARGET_DIR/nanoCLR/mcuconf.h" 2>/dev/null || true
    cp "$REFERENCE_BOARD/mcuconf.h" "$TARGET_DIR/nanoBooter/mcuconf.h" 2>/dev/null || true

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
#define STM32_SERIAL_USE_USART2             TRUE
EOF_MCU_OVERRIDES

            cat >> "$mcu" << EOF_MCU_PROFILE_OVERRIDES

#undef STM32_MAC_USE_ETH
#define STM32_MAC_USE_ETH                  ${ENABLE_STM32_MAC_ETH}
EOF_MCU_PROFILE_OVERRIDES
        fi
    done

    # Increase nanoCLR code space for this custom target by reducing deployment region.
    if [ -f "$TARGET_DIR/nanoCLR/STM32F407xG_CLR.ld" ]; then
        sed -E -i 's/768[Kk]/512k/g' "$TARGET_DIR/nanoCLR/STM32F407xG_CLR.ld"
    fi

    # Provide wire protocol serial configuration header expected by ChibiOS common sources
    if [ -f "$NF_INTERPRETER_DIR/targets-community/ChibiOS/ST_STM32F411_DISCOVERY/common/serialcfg.h" ]; then
        cp "$NF_INTERPRETER_DIR/targets-community/ChibiOS/ST_STM32F411_DISCOVERY/common/serialcfg.h" "$TARGET_DIR/common/" 2>/dev/null || true
    fi
fi

if [ -f /work/build/nanoBooter_main.c ]; then
    cp /work/build/nanoBooter_main.c "$TARGET_DIR/nanoBooter/main.c"
fi

if [ -f /work/build/nanoCLR_main.c ]; then
    cp /work/build/nanoCLR_main.c "$TARGET_DIR/nanoCLR/main.c"
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
list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../common/diseqc_native.cpp")
list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../common/lnb_control.cpp")
list(APPEND NANOCLR_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../common/Device_BlockStorage.c")
set(NANOCLR_PROJECT_SOURCES ${NANOCLR_PROJECT_SOURCES} CACHE INTERNAL "make global")
EOF_NANOCLR_CMAKE

cat > "$TARGET_DIR/nanoBooter/CMakeLists.txt" << 'EOF_NANOBOOTER_CMAKE'
#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

# append nanoBooter source files
list(APPEND NANOBOOTER_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/main.c")
# make var global
set(NANOBOOTER_PROJECT_SOURCES ${NANOBOOTER_PROJECT_SOURCES} CACHE INTERNAL "make global")
EOF_NANOBOOTER_CMAKE

# Copy CMake and config files (if we create them)
if [ -f /work/build/CMakeLists.txt ]; then
    cp /work/build/CMakeLists.txt $TARGET_DIR/
fi
if [ -f /work/build/mcuconf.h ]; then
    cp /work/build/mcuconf.h $TARGET_DIR/
    cp /work/build/mcuconf.h $TARGET_DIR/nanoCLR/
    cp /work/build/mcuconf.h $TARGET_DIR/nanoBooter/
fi
if [ -f /work/build/halconf.h ]; then
    cp /work/build/halconf.h $TARGET_DIR/
fi

# Ensure HAL settings match this board capabilities in both firmware images
for cfg in "$TARGET_DIR/nanoCLR/halconf.h" "$TARGET_DIR/nanoBooter/halconf.h"; do
    if [ -f "$cfg" ]; then
        cat >> "$cfg" << EOF_HAL_OVERRIDES

#undef HAL_USE_USB
#define HAL_USE_USB                         FALSE
#undef HAL_USE_SERIAL_USB
#define HAL_USE_SERIAL_USB                  FALSE
#undef HAL_USE_SERIAL
#define HAL_USE_SERIAL                      TRUE
#undef HAL_USE_GPT
#define HAL_USE_GPT                         TRUE
#undef HAL_USE_PWM
#define HAL_USE_PWM                         TRUE
#undef HAL_USE_PAL
#define HAL_USE_PAL                         TRUE
#undef HAL_USE_MAC
#define HAL_USE_MAC                         ${ENABLE_HAL_MAC}
#undef HAL_USE_WDG
#define HAL_USE_WDG                         FALSE
EOF_HAL_OVERRIDES
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

# Clear stale CMake cache when options/toolchain change
rm -f CMakeCache.txt
rm -rf CMakeFiles

# Clear stale ChibiOS FetchContent state (handles interrupted/locked SVN checkouts)
if [ -d "$BUILD_DIR/_deps" ]; then
    echo -e "${YELLOW}Cleaning stale ChibiOS fetch state...${NC}"
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
    -DSUPPORT_ANY_BASE_CONVERSION=OFF \
    -DNF_FEATURE_DEBUGGER=ON \
    -DNF_FEATURE_RTC=ON \
    -DNF_FEATURE_WATCHDOG=OFF \
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
cmake --build . --config $BUILD_TYPE

# Check if build succeeded
if [ -f "nanoCLR.bin" ]; then
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}Build SUCCESS!${NC}"
    echo -e "${GREEN}========================================${NC}"
    
    # Copy output to work directory
    cp nanoCLR.bin /work/build/nanoCLR.bin
    cp nanoCLR.hex /work/build/nanoCLR.hex
    cp nanoCLR.elf /work/build/nanoCLR.elf
    
    echo -e "${GREEN}Firmware files copied to: /work/build/${NC}"
    echo -e "${GREEN}  - nanoCLR.bin${NC}"
    echo -e "${GREEN}  - nanoCLR.hex${NC}"
    echo -e "${GREEN}  - nanoCLR.elf${NC}"
    
    echo ""
    echo "To flash to board:"
    echo "  st-flash write build/nanoCLR.bin 0x08000000"
else
    echo -e "${RED}========================================${NC}"
    echo -e "${RED}Build FAILED${NC}"
    echo -e "${RED}========================================${NC}"
    exit 1
fi
