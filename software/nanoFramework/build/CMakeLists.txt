#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

cmake_minimum_required(VERSION 3.15)

# Project name
set(TARGET_NAME DISEQC_STM32F407)

# Set MCU and target series
set(TARGET_SERIES STM32F4xx)
set(TARGET_CHIP STM32F407VG)

# Board name
set(TARGET_BOARD_NAME "DiSEqC Controller")

# Custom board configuration
set(CUSTOM_BOARD_CONFIG ON)

# Include common configurations
include(binutils.ChibiOS)
include(binutils.NXP)

# Board files location
set(BOARD_PATH ${CMAKE_CURRENT_SOURCE_DIR})

# Add board-specific files
list(APPEND TARGET_CHIBIOS_BOARD_FILES
    ${CMAKE_CURRENT_SOURCE_DIR}/board_diseqc.h
)

# Add DiSEqC native driver files
list(APPEND TARGET_CHIBIOS_COMMON_SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/common/diseqc_native.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/common/lnb_control.cpp
)

# Add interop files  
list(APPEND TARGET_CHIBIOS_NANOCLR_SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/nanoCLR/diseqc_interop.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/nanoCLR/lnb_interop.cpp
)

# Include paths
list(APPEND TARGET_CHIBIOS_COMMON_INCLUDE_DIRS
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/common
)

# Flash and RAM sizes
set(TARGET_FLASH_SIZE 0x100000)  # 1MB
set(TARGET_RAM_SIZE 0x20000)     # 128KB

# Linker script
set(TARGET_LD_FILE STM32F407xG)

# Enable features
set(NF_FEATURE_DEBUGGER ON CACHE BOOL "Enable debugger")
set(NF_FEATURE_RTC ON CACHE BOOL "Enable RTC")
set(API_System.Device.Gpio ON CACHE BOOL "Enable GPIO API")
set(API_System.Device.Spi ON CACHE BOOL "Enable SPI API")
set(API_System.Device.I2c ON CACHE BOOL "Enable I2C API")
set(API_nanoFramework.System.Net ON CACHE BOOL "Enable Networking")

# Include base CMake
include(NF_ChibiOS)
