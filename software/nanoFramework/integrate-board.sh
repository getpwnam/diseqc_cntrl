#!/bin/bash
# Integrate DiSEqC board into nf-interpreter

set -e

echo "========================================"
echo "nanoFramework Board Integration"
echo "========================================"

# Configuration
NF_REPO="$HOME/nf-interpreter"
BOARD_NAME="DISEQC_STM32F407"
PROJECT_DIR="$(pwd)"

# Check if nf-interpreter exists
if [ ! -d "$NF_REPO" ]; then
    echo "ERROR: nf-interpreter not found at $NF_REPO"
    echo "Please clone it first:"
    echo "  cd ~ && git clone --recursive https://github.com/nanoframework/nf-interpreter.git"
    exit 1
fi

# Create board directory in targets-community (for custom/community boards)
BOARD_DIR="$NF_REPO/targets-community/ChibiOS/$BOARD_NAME"
echo "Creating community board directory: $BOARD_DIR"
mkdir -p "$BOARD_DIR/nanoCLR"
mkdir -p "$BOARD_DIR/common"
mkdir -p "$BOARD_DIR/nanoBooter"
mkdir -p "$BOARD_DIR/nanoCLR"
mkdir -p "$BOARD_DIR/common"

# Copy board files
echo "Copying board configuration..."
cp "$PROJECT_DIR/nf-native/board_diseqc.h" "$BOARD_DIR/"

# Use the original mcuconf.h from build directory but add F407 defines
if [ -f "$PROJECT_DIR/build/mcuconf.h" ]; then
    # Create new mcuconf.h with proper defines
    cat > "$BOARD_DIR/mcuconf.h" << 'EOF'
/*
 * STM32F4xx drivers configuration for DiSEqC Controller
 */

#ifndef MCUCONF_H
#define MCUCONF_H

#define STM32F407_MCUCONF
#define STM32F4xx_MCUCONF

EOF

    # Append original mcuconf.h content (skip header and footer)
    if grep -q "#ifndef MCUCONF_H" "$PROJECT_DIR/build/mcuconf.h"; then
        # Extract content between #ifndef and final #endif
        sed -n '/#define STM32F4.*_MCUCONF/,/^#endif.*MCUCONF/ {/^#endif.*MCUCONF/d; p}' "$PROJECT_DIR/build/mcuconf.h" | tail -n +2 >> "$BOARD_DIR/mcuconf.h"
    else
        # No header, append everything except final #endif
        sed '/^#endif.*MCUCONF/d' "$PROJECT_DIR/build/mcuconf.h" >> "$BOARD_DIR/mcuconf.h"
    fi

    # Close the #ifndef (our own #endif)
    echo "" >> "$BOARD_DIR/mcuconf.h"
    echo "#endif /* MCUCONF_H */" >> "$BOARD_DIR/mcuconf.h"

    # Fix RTC to use LSI instead of LSE (we don't have external 32kHz crystal)
    sed -i 's/#define STM32_RTCSEL[[:space:]]*STM32_RTCSEL_LSE/#define STM32_RTCSEL                        STM32_RTCSEL_LSI/g' "$BOARD_DIR/mcuconf.h"

    # Disable watchdog if not configured
    sed -i 's/#define STM32_WDG_USE_IWDG[[:space:]]*TRUE/#define STM32_WDG_USE_IWDG              FALSE/g' "$BOARD_DIR/mcuconf.h"
else
    echo "WARNING: mcuconf.h not found in build directory"
fi

# Create minimal board CMakeLists.txt
echo "Creating board CMakeLists.txt..."
cat > "$BOARD_DIR/CMakeLists.txt" << 'EOFCMAKE'
#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

include(binutils.common)
include(binutils.ChibiOS)

nf_setup_target_build(
    CLR_LINKER_FILE 
        STM32F407xG_CLR
)
EOFCMAKE

echo "Copying native drivers..."
cp "$PROJECT_DIR/nf-native/diseqc_native.h" "$BOARD_DIR/common/"
cp "$PROJECT_DIR/nf-native/diseqc_native.cpp" "$BOARD_DIR/common/"
cp "$PROJECT_DIR/nf-native/lnb_control.h" "$BOARD_DIR/common/"
cp "$PROJECT_DIR/nf-native/lnb_control.cpp" "$BOARD_DIR/common/"

# Create CMakeLists.txt for common directory
echo "Creating common/CMakeLists.txt..."
cat > "$BOARD_DIR/common/CMakeLists.txt" << 'EOF_COMMON_CMAKE'
#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

# append custom board source files
list(APPEND TARGET_COMMON_SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/diseqc_native.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/lnb_control.cpp
)

# append custom board header files
list(APPEND TARGET_COMMON_HEADERS
    ${CMAKE_CURRENT_SOURCE_DIR}/diseqc_native.h
    ${CMAKE_CURRENT_SOURCE_DIR}/lnb_control.h
)
EOF_COMMON_CMAKE

echo "Copying interop layers..."
cp "$PROJECT_DIR/nf-native/diseqc_interop.cpp" "$BOARD_DIR/nanoCLR/"
cp "$PROJECT_DIR/nf-native/lnb_interop.cpp" "$BOARD_DIR/nanoCLR/"

# Create CMakeLists.txt for nanoCLR directory
echo "Creating nanoCLR/CMakeLists.txt..."
cat > "$BOARD_DIR/nanoCLR/CMakeLists.txt" << 'EOF_NANOCLR_CMAKE'
#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

# append interop source files (if any)
# list(APPEND TARGET_NANOCLR_SOURCES
#     ${CMAKE_CURRENT_SOURCE_DIR}/diseqc_interop.cpp
#     ${CMAKE_CURRENT_SOURCE_DIR}/lnb_interop.cpp
# )
EOF_NANOCLR_CMAKE

# Copy ChibiOS config files from similar board (ST_STM32F429I_DISCOVERY)
echo "Copying ChibiOS configuration files from reference board..."
REFERENCE_BOARD="$NF_REPO/targets/ChibiOS/ST_STM32F429I_DISCOVERY"

if [ -d "$REFERENCE_BOARD" ]; then
    # Copy nanoCLR configs
    cp "$REFERENCE_BOARD/nanoCLR/halconf.h" "$BOARD_DIR/nanoCLR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoCLR/halconf_nf.h" "$BOARD_DIR/nanoCLR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoCLR/chconf.h" "$BOARD_DIR/nanoCLR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoCLR/target_board.h.in" "$BOARD_DIR/nanoCLR/" 2>/dev/null || true

    # Disable USB, Serial USB, and Watchdog in halconf.h
    if [ -f "$BOARD_DIR/nanoCLR/halconf.h" ]; then
        sed -i 's/#define HAL_USE_USB[[:space:]]*TRUE/#define HAL_USE_USB                 FALSE/g' "$BOARD_DIR/nanoCLR/halconf.h"
        sed -i 's/#define HAL_USE_SERIAL_USB[[:space:]]*TRUE/#define HAL_USE_SERIAL_USB          FALSE/g' "$BOARD_DIR/nanoCLR/halconf.h"
        # Uncomment and set WDG to FALSE
        sed -i 's|^// #if !defined(HAL_USE_WDG)|#if !defined(HAL_USE_WDG)|g' "$BOARD_DIR/nanoCLR/halconf.h"
        sed -i 's|^// #define HAL_USE_WDG.*|#define HAL_USE_WDG                 FALSE|g' "$BOARD_DIR/nanoCLR/halconf.h"
        sed -i 's|^// #endif$|#endif|g' "$BOARD_DIR/nanoCLR/halconf.h"
    fi

    # Copy nanoBooter configs (optional, but keeps structure consistent)
    cp "$REFERENCE_BOARD/nanoBooter/halconf.h" "$BOARD_DIR/nanoBooter/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoBooter/halconf_nf.h" "$BOARD_DIR/nanoBooter/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/nanoBooter/chconf.h" "$BOARD_DIR/nanoBooter/" 2>/dev/null || true

    # Copy target common files
    cp "$REFERENCE_BOARD/target_common.h.in" "$BOARD_DIR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/target_common.c" "$BOARD_DIR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/target_BlockStorage.c" "$BOARD_DIR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD/target_BlockStorage.h" "$BOARD_DIR/" 2>/dev/null || true

    # Copy all other target_* files
    cp "$REFERENCE_BOARD"/target_*.c "$BOARD_DIR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD"/target_*.h "$BOARD_DIR/" 2>/dev/null || true
    cp "$REFERENCE_BOARD"/target_*.cpp "$BOARD_DIR/" 2>/dev/null || true

    echo "✓ ChibiOS config files copied"
else
    echo "WARNING: Reference board not found, you may need to add ChibiOS configs manually"
fi

# Create minimal board.c and board.h (ChibiOS board definition)
echo "Creating minimal ChibiOS board definition files..."
cat > "$BOARD_DIR/board.h" << 'EOF_BOARD_H'
/*
    ChibiOS - Copyright (C) 2006..2018 Giovanni Di Sirio

    Licensed under the Apache License, Version 2.0 (the "License");
*/

#ifndef BOARD_H
#define BOARD_H

/*
 * Board identifier.
 */
#define BOARD_DISEQC_STM32F407
#define BOARD_NAME                  "DiSEqC Controller STM32F407VGT6"

/*
 * Board oscillators-related settings.
 */
#if !defined(STM32_LSECLK)
#define STM32_LSECLK                32768U
#endif

#if !defined(STM32_HSECLK)
#define STM32_HSECLK                8000000U
#endif

/*
 * Board voltages.
 */
#define STM32_VDD                   330U

/*
 * MCU type as defined in the ST header.
 */
#define STM32F407xx

#if !defined(_FROM_ASM_)
#ifdef __cplusplus
extern "C" {
#endif
  void boardInit(void);
#ifdef __cplusplus
}
#endif
#endif /* _FROM_ASM_ */

#endif /* BOARD_H */
EOF_BOARD_H

cat > "$BOARD_DIR/board.c" << 'EOF_BOARD_C'
/*
    ChibiOS - Copyright (C) 2006..2018 Giovanni Di Sirio

    Licensed under the Apache License, Version 2.0 (the "License");
*/

#include "hal.h"

#if HAL_USE_PAL || defined(__DOXYGEN__)
/**
 * @brief   PAL setup.
 */
const PALConfig pal_default_config = {
#if STM32_HAS_GPIOA
  {VAL_GPIOA_MODER, VAL_GPIOA_OTYPER, VAL_GPIOA_OSPEEDR, VAL_GPIOA_PUPDR,
   VAL_GPIOA_ODR, VAL_GPIOA_AFRL, VAL_GPIOA_AFRH},
#endif
#if STM32_HAS_GPIOB
  {VAL_GPIOB_MODER, VAL_GPIOB_OTYPER, VAL_GPIOB_OSPEEDR, VAL_GPIOB_PUPDR,
   VAL_GPIOB_ODR, VAL_GPIOB_AFRL, VAL_GPIOB_AFRH},
#endif
#if STM32_HAS_GPIOC
  {VAL_GPIOC_MODER, VAL_GPIOC_OTYPER, VAL_GPIOC_OSPEEDR, VAL_GPIOC_PUPDR,
   VAL_GPIOC_ODR, VAL_GPIOC_AFRL, VAL_GPIOC_AFRH},
#endif
#if STM32_HAS_GPIOD
  {VAL_GPIOD_MODER, VAL_GPIOD_OTYPER, VAL_GPIOD_OSPEEDR, VAL_GPIOD_PUPDR,
   VAL_GPIOD_ODR, VAL_GPIOD_AFRL, VAL_GPIOD_AFRH},
#endif
#if STM32_HAS_GPIOE
  {VAL_GPIOE_MODER, VAL_GPIOE_OTYPER, VAL_GPIOE_OSPEEDR, VAL_GPIOE_PUPDR,
   VAL_GPIOE_ODR, VAL_GPIOE_AFRL, VAL_GPIOE_AFRH},
#endif
#if STM32_HAS_GPIOF
  {VAL_GPIOF_MODER, VAL_GPIOF_OTYPER, VAL_GPIOF_OSPEEDR, VAL_GPIOF_PUPDR,
   VAL_GPIOF_ODR, VAL_GPIOF_AFRL, VAL_GPIOF_AFRH},
#endif
#if STM32_HAS_GPIOG
  {VAL_GPIOG_MODER, VAL_GPIOG_OTYPER, VAL_GPIOG_OSPEEDR, VAL_GPIOG_PUPDR,
   VAL_GPIOG_ODR, VAL_GPIOG_AFRL, VAL_GPIOG_AFRH},
#endif
#if STM32_HAS_GPIOH
  {VAL_GPIOH_MODER, VAL_GPIOH_OTYPER, VAL_GPIOH_OSPEEDR, VAL_GPIOH_PUPDR,
   VAL_GPIOH_ODR, VAL_GPIOH_AFRL, VAL_GPIOH_AFRH},
#endif
#if STM32_HAS_GPIOI
  {VAL_GPIOI_MODER, VAL_GPIOI_OTYPER, VAL_GPIOI_OSPEEDR, VAL_GPIOI_PUPDR,
   VAL_GPIOI_ODR, VAL_GPIOI_AFRL, VAL_GPIOI_AFRH}
#endif
};
#endif

/**
 * @brief   Early initialization code.
 */
void __early_init(void) {
  stm32_clock_init();
}

#if HAL_USE_SDC || defined(__DOXYGEN__)
/**
 * @brief   SDC card detection.
 */
bool sdc_lld_is_card_inserted(SDCDriver *sdcp) {
  (void)sdcp;
  return TRUE;
}

/**
 * @brief   SDC card write protection detection.
 */
bool sdc_lld_is_write_protected(SDCDriver *sdcp) {
  (void)sdcp;
  return FALSE;
}
#endif

/**
 * @brief   Board-specific initialization code.
 */
void boardInit(void) {
}
EOF_BOARD_C

# Also need to include board_diseqc.h GPIO definitions in board.h
cat >> "$BOARD_DIR/board.h" << 'EOF_APPEND'

/* Include GPIO pin definitions from board_diseqc.h */
#include "board_diseqc.h"

EOF_APPEND

echo "✓ ChibiOS board definition files created"

echo ""
echo "✓ Board files copied to nf-interpreter"
echo ""
echo "Next steps:"
echo "1. cd $NF_REPO"
echo "2. mkdir build && cd build"
echo "3. cmake -G Ninja -DTARGET_SERIES=STM32F4xx -DRTOS=CHIBIOS -DTARGET_BOARD=$BOARD_NAME .."
echo "4. ninja"
echo ""
echo "Output will be: $NF_REPO/build/nanoCLR.bin"
