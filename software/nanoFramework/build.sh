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
TARGET_NAME="DISEQC_STM32F407"
BUILD_TYPE="Release"

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
TARGET_DIR="$NF_INTERPRETER_DIR/targets/CHIBIOS/$TARGET_NAME"

if [ ! -d "$TARGET_DIR" ]; then
    mkdir -p $TARGET_DIR
    mkdir -p $TARGET_DIR/nanoCLR
    mkdir -p $TARGET_DIR/common
fi

# Copy board files
echo -e "${YELLOW}Copying board configuration files...${NC}"
cp /work/nf-native/board_diseqc.h $TARGET_DIR/
cp /work/nf-native/diseqc_native.h $TARGET_DIR/common/
cp /work/nf-native/diseqc_native.cpp $TARGET_DIR/common/
cp /work/nf-native/lnb_control.h $TARGET_DIR/common/
cp /work/nf-native/lnb_control.cpp $TARGET_DIR/common/
cp /work/nf-native/diseqc_interop.cpp $TARGET_DIR/nanoCLR/
cp /work/nf-native/lnb_interop.cpp $TARGET_DIR/nanoCLR/

# Copy CMake and config files (if we create them)
if [ -f /work/build/CMakeLists.txt ]; then
    cp /work/build/CMakeLists.txt $TARGET_DIR/
fi
if [ -f /work/build/mcuconf.h ]; then
    cp /work/build/mcuconf.h $TARGET_DIR/
fi
if [ -f /work/build/halconf.h ]; then
    cp /work/build/halconf.h $TARGET_DIR/
fi

# Create build directory
echo -e "${YELLOW}Creating build directory...${NC}"
BUILD_DIR="$NF_INTERPRETER_DIR/build/$TARGET_NAME"
mkdir -p $BUILD_DIR
cd $BUILD_DIR

# Configure with CMake
echo -e "${YELLOW}Configuring with CMake...${NC}"
cmake -G Ninja \
    -DCMAKE_BUILD_TYPE=$BUILD_TYPE \
    -DTARGET_SERIES=STM32F4xx \
    -DRTOS=CHIBIOS \
    -DSUPPORT_ANY_BASE_CONVERSION=OFF \
    -DNF_FEATURE_DEBUGGER=ON \
    -DNF_FEATURE_RTC=ON \
    -DAPI_System.Device.Gpio=ON \
    -DAPI_System.Device.Spi=ON \
    -DAPI_System.Device.I2c=ON \
    -DAPI_System.Net=ON \
    -DNF_NETWORKING_SNTP=OFF \
    -DUSE_RNG=OFF \
    $NF_INTERPRETER_DIR

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
