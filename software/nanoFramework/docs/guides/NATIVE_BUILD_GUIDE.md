# Native Build Guide - DiSEqC Controller

## ⚠️ CORRECTION: Docker Image Doesn't Exist

The `nanoframework/dev-container` Docker image **does not exist**. 

For **custom boards with custom native code**, you need to build nf-interpreter **natively**.

---

## ✅ **Correct Build Process (Native)**

### Prerequisites

**Install Build Tools (WSL/Linux):**
```sh
sudo apt update
sudo apt install -y \
    gcc-arm-none-eabi \
    cmake \
    ninja-build \
    git \
    python3 \
    libnewlib-arm-none-eabi
```

**Verify Installation:**
```sh
arm-none-eabi-gcc --version  # Should show GCC 10.x or newer
cmake --version               # Should show 3.15+
ninja --version              # Should show 1.x
```

---

## 🏗️ **Step-by-Step Build**

### Step 1: Clone nf-interpreter

```sh
cd ~
git clone --recursive https://github.com/nanoframework/nf-interpreter.git
cd nf-interpreter

# This is ~500MB and may take 10-15 minutes
```

### Step 2: Integrate Your Board

```sh
# Go back to your project
cd ~/Dev/diseqc_cntrl/software/nanoFramework

# Make integration script executable
chmod +x integrate-board.sh

# Run integration (copies your files into nf-interpreter)
./integrate-board.sh
```

This copies:
- `board_diseqc.h` → Board configuration
- `mcuconf.h` → MCU peripherals
- `diseqc_native.cpp/h` → DiSEqC driver
- `lnb_control.cpp/h` → LNB driver
- `*_interop.cpp` → C# bindings

### Step 3: Build Firmware

```sh
cd ~/nf-interpreter

# Create build directory
mkdir build && cd build

# Configure with CMake
cmake -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DTARGET_SERIES=STM32F4xx \
    -DRTOS=ChibiOS \
    -DTARGET_BOARD=DISEQC_STM32F407 \
    -DNF_FEATURE_DEBUGGER=ON \
    -DNF_FEATURE_RTC=ON \
    -DAPI_System.Device.Gpio=ON \
    -DAPI_System.Device.I2c=ON \
    -DAPI_System.Device.Spi=ON \
    -DAPI_nanoFramework.System.Net=ON \
    ..

# Build (takes 5-10 minutes first time)
ninja

# Check output
ls -lh nanoCLR.bin
```

**Output:** `~/nf-interpreter/build/nanoCLR.bin`

### Step 4: Copy Firmware Back

```sh
# Copy to your project for flashing
cp ~/nf-interpreter/build/nanoCLR.bin \
   ~/Dev/diseqc_cntrl/software/nanoFramework/build/

# Verify
ls -lh ~/Dev/diseqc_cntrl/software/nanoFramework/build/nanoCLR.bin
```

---

## 🚀 **Quick Build Script**

I'll create a simplified build script for you:

```sh
# File: native-build.sh
#!/bin/bash
set -e

NF_DIR="$HOME/nf-interpreter"

# Integrate board (if needed)
./integrate-board.sh

# Build
cd "$NF_DIR"
mkdir -p build && cd build

cmake -G Ninja \
    -DTARGET_SERIES=STM32F4xx \
    -DRTOS=CHIBIOS \
    -DTARGET_BOARD=DISEQC_STM32F407 \
    -DCMAKE_BUILD_TYPE=Release \
    ..

ninja

# Copy output
cp nanoCLR.bin ~/Dev/diseqc_cntrl/software/nanoFramework/build/

echo "✓ Build complete!"
echo "Firmware: ~/Dev/diseqc_cntrl/software/nanoFramework/build/nanoCLR.bin"
```

---

## ⏱️ **Build Times**

- **First build:** 10-15 minutes (compiles entire nanoFramework)
- **Incremental builds:** 1-3 minutes (only your changes)
- **Clean rebuild:** 5-8 minutes

---

## 🔧 **Troubleshooting**

### Issue: "arm-none-eabi-gcc: command not found"

```sh
# Install ARM GCC toolchain
sudo apt install gcc-arm-none-eabi libnewlib-arm-none-eabi
```

### Issue: CMake can't find target

Make sure you ran `integrate-board.sh` first:
```sh
ls ~/nf-interpreter/targets/CHIBIOS/DISEQC_STM32F407/
# Should show your board files
```

### Issue: Build fails with missing includes

```sh
# Update submodules
cd ~/nf-interpreter
git submodule update --init --recursive
```

### Issue: Ninja not found

```sh
sudo apt install ninja-build
```

---

## 📊 **Build Configuration Summary**

| Setting | Value |
|---------|-------|
| Build Tool | Ninja |
| Compiler | ARM GCC (arm-none-eabi-gcc) |
| Target | STM32F407VG |
| RTOS | ChibiOS |
| Build Type | Release (optimized) |
| Output | nanoCLR.bin (~400-500KB) |

---

## 🎯 **Complete Workflow**

```sh
# 1. Install tools (one time)
sudo apt install gcc-arm-none-eabi cmake ninja-build git

# 2. Clone nf-interpreter (one time)
cd ~ && git clone --recursive https://github.com/nanoframework/nf-interpreter.git

# 3. Integrate your board
cd ~/Dev/diseqc_cntrl/software/nanoFramework
./integrate-board.sh

# 4. Build firmware
cd ~/nf-interpreter/build
cmake -G Ninja -DTARGET_SERIES=STM32F4xx -DRTOS=CHIBIOS -DTARGET_BOARD=DISEQC_STM32F407 ..
ninja

# 5. Copy output
cp nanoCLR.bin ~/Dev/diseqc_cntrl/software/nanoFramework/build/

# 6. Flash to board (when PCB arrives)
st-flash write ~/Dev/diseqc_cntrl/software/nanoFramework/build/nanoCLR.bin 0x08000000
```

---

## ✅ **Incremental Builds**

After initial setup, rebuilding is fast:

```sh
# 1. Modify your code
nano ~/Dev/diseqc_cntrl/software/nanoFramework/nf-native/diseqc_native.cpp

# 2. Re-integrate (copies modified files)
cd ~/Dev/diseqc_cntrl/software/nanoFramework
./integrate-board.sh

# 3. Rebuild (only recompiles changed files)
cd ~/nf-interpreter/build
ninja

# 4. Copy & flash
cp nanoCLR.bin ~/Dev/diseqc_cntrl/software/nanoFramework/build/
st-flash write nanoCLR.bin 0x08000000
```

---

**Sorry for the Docker confusion - native build is the correct approach for custom boards!** 🛠️

