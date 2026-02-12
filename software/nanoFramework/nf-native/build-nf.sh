#!/bin/bash
#
# Build script for nanoFramework with DiSEqC native driver
# Usage: ./build-nf.sh [clean|flash|debug]
#

set -e  # Exit on error

# Configuration
NF_INTERPRETER_PATH="${NF_INTERPRETER_PATH:-$HOME/nf-interpreter}"
TARGET_BOARD="DISEQC_STM32F407"
BUILD_TYPE="${BUILD_TYPE:-Release}"
BUILD_DIR="$NF_INTERPRETER_PATH/build"
TOOLCHAIN_PREFIX="${TOOLCHAIN_PREFIX:-/usr}"
CHIBIOS_SOURCE="${CHIBIOS_SOURCE:-$HOME/ChibiOS}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Functions
function print_info {
    echo -e "${GREEN}[INFO]${NC} $1"
}

function print_warn {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

function print_error {
    echo -e "${RED}[ERROR]${NC} $1"
}

function check_prerequisites {
    print_info "Checking prerequisites..."
    
    # Check if nf-interpreter exists
    if [ ! -d "$NF_INTERPRETER_PATH" ]; then
        print_error "nf-interpreter not found at $NF_INTERPRETER_PATH"
        print_info "Clone with: git clone https://github.com/nanoframework/nf-interpreter.git"
        exit 1
    fi
    
    # Check if ChibiOS exists
    if [ ! -d "$CHIBIOS_SOURCE" ]; then
        print_error "ChibiOS not found at $CHIBIOS_SOURCE"
        print_info "Set CHIBIOS_SOURCE environment variable or download ChibiOS"
        exit 1
    fi
    
    # Check for ARM toolchain
    if ! command -v arm-none-eabi-gcc &> /dev/null; then
        print_error "ARM toolchain not found"
        print_info "Install with: sudo apt-get install gcc-arm-none-eabi"
        exit 1
    fi
    
    # Check for CMake
    if ! command -v cmake &> /dev/null; then
        print_error "CMake not found"
        print_info "Install with: sudo apt-get install cmake"
        exit 1
    fi
    
    print_info "Prerequisites OK"
}

function setup_target {
    print_info "Setting up custom target..."
    
    TARGET_DIR="$NF_INTERPRETER_PATH/targets/$TARGET_BOARD"
    
    # Check if native driver files exist in current directory
    if [ ! -f "diseqc_native.h" ]; then
        print_error "Native driver files not found in current directory"
        print_info "Expected files: diseqc_native.h, diseqc_native.cpp, etc."
        exit 1
    fi
    
    # Create target directory
    mkdir -p "$TARGET_DIR/nanoCLR"
    
    # Copy files
    print_info "Copying native driver files..."
    cp diseqc_native.h "$TARGET_DIR/nanoCLR/"
    cp diseqc_native.cpp "$TARGET_DIR/nanoCLR/"
    cp diseqc_interop.cpp "$TARGET_DIR/nanoCLR/"
    cp board_diseqc.h "$TARGET_DIR/"
    cp board_diseqc.cpp "$TARGET_DIR/"
    
    # Create CMakeLists.txt
    print_info "Creating CMakeLists.txt..."
    cat > "$TARGET_DIR/nanoCLR/CMakeLists.txt" << 'EOF'
#
# DiSEqC Native Driver
#

list(APPEND TARGET_NANO_CLR_SOURCES
    ${CMAKE_CURRENT_LIST_DIR}/diseqc_native.cpp
    ${CMAKE_CURRENT_LIST_DIR}/diseqc_interop.cpp
)

list(APPEND TARGET_NANO_CLR_HEADERS
    ${CMAKE_CURRENT_LIST_DIR}/diseqc_native.h
)
EOF
    
    print_info "Target setup complete"
}

function clean_build {
    print_info "Cleaning build directory..."
    rm -rf "$BUILD_DIR"
    mkdir -p "$BUILD_DIR"
}

function configure_cmake {
    print_info "Configuring CMake..."
    
    cd "$BUILD_DIR"
    
    cmake \
        -DTARGET_BOARD=$TARGET_BOARD \
        -DCMAKE_BUILD_TYPE=$BUILD_TYPE \
        -DTOOLCHAIN_PREFIX=$TOOLCHAIN_PREFIX \
        -DCHIBIOS_SOURCE=$CHIBIOS_SOURCE \
        -DNF_FEATURE_DEBUGGER=ON \
        -DAPI_nanoFramework.Hardware.Stm32=ON \
        -DAPI_Windows.Devices.Gpio=ON \
        -DAPI_Windows.Devices.Spi=ON \
        -DAPI_Windows.Devices.SerialCommunication=ON \
        ..
    
    if [ $? -ne 0 ]; then
        print_error "CMake configuration failed"
        exit 1
    fi
    
    print_info "CMake configuration complete"
}

function build_firmware {
    print_info "Building firmware..."
    
    cd "$BUILD_DIR"
    
    cmake --build . --target nanoCLR -j$(nproc)
    
    if [ $? -ne 0 ]; then
        print_error "Build failed"
        exit 1
    fi
    
    print_info "Build complete!"
    print_info "Binary: $BUILD_DIR/nanoCLR.bin"
    print_info "Hex:    $BUILD_DIR/nanoCLR.hex"
}

function flash_firmware {
    print_info "Flashing firmware to board..."
    
    if [ ! -f "$BUILD_DIR/nanoCLR.bin" ]; then
        print_error "Binary not found. Build first."
        exit 1
    fi
    
    # Try st-flash first
    if command -v st-flash &> /dev/null; then
        st-flash write "$BUILD_DIR/nanoCLR.bin" 0x08000000
    # Try openocd
    elif command -v openocd &> /dev/null; then
        openocd -f interface/stlink.cfg -f target/stm32f4x.cfg \
            -c "program $BUILD_DIR/nanoCLR.bin 0x08000000 verify reset exit"
    else
        print_error "No flashing tool found (st-flash or openocd)"
        exit 1
    fi
    
    if [ $? -eq 0 ]; then
        print_info "Firmware flashed successfully!"
    else
        print_error "Flashing failed"
        exit 1
    fi
}

function debug_firmware {
    print_info "Starting debugger..."
    
    if [ ! -f "$BUILD_DIR/nanoCLR.elf" ]; then
        print_error "ELF file not found. Build first."
        exit 1
    fi
    
    # Start OpenOCD in background
    openocd -f interface/stlink.cfg -f target/stm32f4x.cfg &
    OPENOCD_PID=$!
    
    sleep 2
    
    # Start GDB
    arm-none-eabi-gdb "$BUILD_DIR/nanoCLR.elf" \
        -ex "target remote localhost:3333" \
        -ex "monitor reset halt" \
        -ex "load"
    
    # Kill OpenOCD when GDB exits
    kill $OPENOCD_PID 2>/dev/null
}

function show_usage {
    echo "Usage: $0 [command]"
    echo ""
    echo "Commands:"
    echo "  setup    - Copy native driver files to nf-interpreter"
    echo "  clean    - Clean build directory"
    echo "  build    - Configure and build firmware"
    echo "  flash    - Flash firmware to board"
    echo "  debug    - Start GDB debugger"
    echo "  all      - Setup, clean, and build (default)"
    echo ""
    echo "Environment variables:"
    echo "  NF_INTERPRETER_PATH - Path to nf-interpreter (default: ~/nf-interpreter)"
    echo "  BUILD_TYPE          - Build type: Release or Debug (default: Release)"
    echo "  CHIBIOS_SOURCE      - Path to ChibiOS (default: ~/ChibiOS)"
    echo "  TOOLCHAIN_PREFIX    - ARM toolchain prefix (default: /usr)"
}

# Main script
print_info "nanoFramework DiSEqC Native Driver Build Script"
print_info "================================================"

case "${1:-all}" in
    setup)
        check_prerequisites
        setup_target
        ;;
    clean)
        clean_build
        ;;
    build)
        check_prerequisites
        configure_cmake
        build_firmware
        ;;
    flash)
        flash_firmware
        ;;
    debug)
        debug_firmware
        ;;
    all)
        check_prerequisites
        setup_target
        clean_build
        configure_cmake
        build_firmware
        ;;
    help|--help|-h)
        show_usage
        ;;
    *)
        print_error "Unknown command: $1"
        show_usage
        exit 1
        ;;
esac

print_info "Done!"
