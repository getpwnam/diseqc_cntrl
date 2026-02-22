#!/bin/bash
# Native Build Script for DiSEqC Controller
# Builds nanoFramework firmware with custom board

set -e

echo "========================================"
echo "nanoFramework Native Build"
echo "========================================"

# Configuration
NF_DIR="$HOME/nf-interpreter"
PROJECT_DIR="$(pwd)"
BOARD_NAME="DISEQC_STM32F407"

# Check prerequisites
command -v arm-none-eabi-gcc >/dev/null 2>&1 || {
    echo "ERROR: arm-none-eabi-gcc not found"
    echo "Install with: sudo apt install gcc-arm-none-eabi"
    exit 1
}

command -v cmake >/dev/null 2>&1 || {
    echo "ERROR: cmake not found"
    echo "Install with: sudo apt install cmake"
    exit 1
}

command -v ninja >/dev/null 2>&1 || {
    echo "ERROR: ninja not found"
    echo "Install with: sudo apt install ninja-build"
    exit 1
}

# Check if nf-interpreter exists
if [ ! -d "$NF_DIR" ]; then
    echo "ERROR: nf-interpreter not found at $NF_DIR"
    echo ""
    echo "Clone it first:"
    echo "  cd ~ && git clone --recursive https://github.com/nanoframework/nf-interpreter.git"
    echo ""
    exit 1
fi

# Integrate board files
echo "Integrating board files..."
./integrate-board.sh

# Build
echo "Building firmware..."
cd "$NF_DIR"

# Create build directory if it doesn't exist
mkdir -p build
cd build

# Configure
echo "Configuring with CMake..."
cmake -G Ninja \
    -DCMAKE_TOOLCHAIN_FILE=$NF_DIR/CMake/toolchain.arm-none-eabi.cmake \
    -DCMAKE_BUILD_TYPE=Release \
    -DTARGET_SERIES=STM32F4xx \
    -DRTOS=ChibiOS \
    -DTARGET_BOARD=$BOARD_NAME \
    -DNF_FEATURE_DEBUGGER=ON \
    -DNF_FEATURE_RTC=ON \
    ..

# Build
echo "Compiling..."
ninja

# Check if build succeeded
if [ ! -f "nanoCLR.bin" ]; then
    echo "ERROR: Build failed - nanoCLR.bin not found"
    exit 1
fi

# Copy output
echo "Copying output files..."
cp nanoCLR.bin "$PROJECT_DIR/build/"
cp nanoCLR.hex "$PROJECT_DIR/build/" 2>/dev/null || true
cp nanoCLR.elf "$PROJECT_DIR/build/" 2>/dev/null || true

echo ""
echo "========================================"
echo "Build SUCCESS!"
echo "========================================"
echo ""
echo "Firmware location:"
echo "  $PROJECT_DIR/build/nanoCLR.bin"
echo ""
echo "To flash to board:"
echo "  st-flash write build/nanoCLR.bin 0x08000000"
echo ""
