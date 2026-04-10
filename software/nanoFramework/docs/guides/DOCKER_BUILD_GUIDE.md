# Docker Build Guide

## Purpose

Build firmware artifacts for the `M0DMF_CUBLEY_F407` target using Docker, without installing a local ARM toolchain.

## Current Profile Status

- This guide reflects the validated profile in `toolchain/build.sh`.
- `System.Net` is currently disabled in that profile.
- Upstream `nf-interpreter` is fetched during build.

---

## What's Included

This setup provides:
- ✅ **Docker-based build** - Uses a local build container from `Dockerfile.build`
- ✅ **Automated build script** - One command to build everything
- ✅ **CMake configuration** - Custom board definition
- ✅ **MCU configuration** - STM32F407 peripherals configured
- ✅ **Output firmware** - Ready-to-flash binaries

---

## Prerequisites

### Required Software
1. **Docker Desktop** (Windows/Mac) or **Docker Engine** (Linux)
   - Download: https://www.docker.com/get-started
   - Includes Docker Compose V2 (modern `docker compose` command)

2. **Git** (for cloning nf-interpreter)

3. **Linux host tools for managed build scripts** (required for `toolchain/compile-managed.sh` and `toolchain/build-chain.sh`)
    - Install Mono/MSBuild:
       - `sudo apt update && sudo apt install -y mono-complete`
    - Install `nanoff` CLI:
       - `dotnet tool install -g nanoff` (or `dotnet tool update -g nanoff`)
    - Add `xbuild` compatibility shim to `msbuild`:
       - `sudo ln -sf "$(command -v msbuild)" /usr/local/bin/xbuild`
    - Verify:
       - `msbuild --version`
       - `xbuild --version`
       - `nanoff --help`
      - Troubleshooting:
         - If you get `xbuild: command not found`, recreate the shim:
            - `sudo ln -sf "$(command -v msbuild)" /usr/local/bin/xbuild`

**Note:** This uses Docker Compose V2 (`docker compose`, not `docker-compose`)
**Note:** No Docker Hub login is required for the build container in this project.

### Hardware
- STM32F407VGT6 DiSEqC controller board
- ST-Link V2 programmer (for flashing)

---

## 🚀 Quick Start

### Step 1: Start Docker

**Windows:**
```powershell
# Make sure Docker Desktop is running
docker --version
docker compose version
```

**Linux/WSL:**
```bash
sudo systemctl start docker
docker --version
docker compose version
```

### Step 2: Build Firmware

**Option A: PowerShell (Windows/WSL)**
```powershell
# In your project directory
./toolchain/build.ps1
```

**Option B: Bash (Linux/WSL/Mac)**
```bash
# Make script executable
chmod +x toolchain/build.sh

# Run build (default minimal profile)
docker compose run --build --rm nanoframework-build /work/toolchain/build.sh

# Run build (w5500-native scaffold profile)
docker compose run --build --rm -e NF_BUILD_PROFILE=w5500-native nanoframework-build /work/toolchain/build.sh

# Run build (deprecated transitional profile)
docker compose run --build --rm -e NF_BUILD_PROFILE=network nanoframework-build /work/toolchain/build.sh
```

**Option C: Manual Docker Command**
```bash
docker compose up
docker compose run --build --rm nanoframework-build /work/toolchain/build.sh
```

### Step 3: Flash to Board

```bash
# Using st-flash (install: apt install stlink-tools)
st-flash write build/nanoBooter.bin 0x08000000
st-flash write build/nanoCLR.bin 0x08004000

# Or using OpenOCD
openocd -f interface/stlink.cfg -f target/stm32f4x.cfg \
    -c "program build/nanoBooter.bin 0x08000000 verify" \
    -c "program build/nanoCLR.bin 0x08004000 verify reset exit"
```

---

## 📊 Build Process Details

### What the Build Does

1. **Builds Docker Image Locally**
   - Uses `Dockerfile.build` in this repository
   - Installs ARM GCC compiler, CMake, Ninja, and required build tools
   
2. **Clones nf-interpreter** (first time only)
   - Official nanoFramework runtime
   - ChibiOS RTOS
   - Networking stack
   
3. **Copies Your Files**
   - `board_cubley.h` - Board configuration
   - `diseqc_native.cpp` - DiSEqC driver
   - `lnb_control.cpp` - LNB I2C control
   - `*_interop.cpp` - C# bindings
   
4. **Configures with CMake**
   - Target: `M0DMF_CUBLEY_F407`
   - Enables: GPIO, SPI, I2C (networking currently disabled in this profile)
   - DiSEqC + LNB drivers linked
   
5. **Builds with Ninja**
   - Compiles all sources
   - Links firmware
   - Generates binaries
   
6. **Outputs**
   - `build/nanoCLR.bin` - Flash binary
   - `build/nanoCLR.hex` - HEX file
   - `build/nanoCLR.elf` - Debug symbols

### Build Time
- **First build:** 10-15 minutes (downloads nf-interpreter)
- **Subsequent builds:** 2-5 minutes (cached)

---

## 📁 File Structure After Build

```
software/nanoFramework/
├── build/                      # Build artifacts
│   ├── nanoCLR.bin            # ← Flash this!
│   ├── nanoCLR.hex
│   ├── nanoCLR.elf
│   ├── CMakeLists.txt         # CMake configuration
│   └── mcuconf.h              # MCU peripheral config
│
├── docker-compose.yml         # Docker configuration
├── toolchain/
│   ├── build.sh               # Bash build script
│   └── build.ps1              # PowerShell build script
│
├── nf-native/                 # Your native code
│   ├── board_cubley.h
│   ├── diseqc_native.cpp
│   ├── lnb_control.cpp
│   └── *_interop.cpp
│
└── DiSEqC_Control/            # Your C# code
    └── Program.cs
```

---

## 🔧 Customization

### Change Build Type

Edit `toolchain/build.sh`:
```bash
BUILD_TYPE="Debug"   # For debugging
BUILD_TYPE="Release" # For production (default)
```

### Enable/Disable Features

Edit `build/CMakeLists.txt`:
```cmake
set(NF_FEATURE_DEBUGGER ON)      # Enable debug
set(API_System.Device.Gpio ON)   # Enable GPIO API
set(API_System.Net OFF)          # Networking is disabled in the current target profile
```

### Modify Peripheral Configuration

Edit `build/mcuconf.h`:
```c
#define STM32_I2C_USE_I2C1  TRUE   // Enable I2C1
#define STM32_SPI_USE_SPI1  TRUE   // Enable SPI1
```

---

## 🐛 Troubleshooting

### Issue: Docker not found

**Solution:**
```powershell
# Windows: Install Docker Desktop
https://www.docker.com/products/docker-desktop

# Linux: Install Docker Engine
sudo apt update
sudo apt install docker.io docker-compose-plugin
sudo usermod -aG docker $USER
# Log out and back in
```

### Issue: Permission denied (Linux/WSL)

**Solution:**
```bash
# Add user to docker group
sudo usermod -aG docker $USER

# Or run with sudo
sudo docker compose run --build --rm nanoframework-build /work/toolchain/build.sh
```

### Issue: Build fails - "CMake not found"

**Solution:**
The Docker container should have all tools. Try:
```bash
# Rebuild local image
docker compose build --no-cache
```

### Issue: "nanoCLR.bin not found"

**Solution:**
Check build logs for errors:
```bash
docker compose run --build --rm nanoframework-build /work/toolchain/build.sh 2>&1 | tee build.log
```

### Issue: Flash fails - "st-flash not found"

**Solution:**
```bash
# Ubuntu/Debian/WSL
sudo apt install stlink-tools

# Windows
# Download from: https://github.com/stlink-org/stlink/releases

# Mac
brew install stlink
```

---

## 📊 Build Output Example

```
========================================
nanoFramework DiSEqC Controller Build
========================================

Cloning nf-interpreter repository...
Setting up target directory...
Copying board configuration files...
Creating build directory...
Configuring with CMake...
-- Target: M0DMF_CUBLEY_F407
-- MCU: STM32F407VG
-- Features enabled:
   - DiSEqC native driver
   - LNB I2C control
   - W5500 Ethernet
   - MQTT client
Building firmware...
[152/152] Linking CXX executable nanoCLR.elf

========================================
Build SUCCESS!
========================================

Firmware files copied to: /work/build/
  - nanoBooter.bin
  - nanoBooter.hex
  - nanoBooter.elf
  - nanoCLR.bin
  - nanoCLR.hex
  - nanoCLR.elf

To flash to board:
  st-flash write build/nanoBooter.bin 0x08000000
  st-flash write build/nanoCLR.bin 0x08004000
```

---

## 🎯 Next Steps After Build

1. **Flash firmware** to board
   ```bash
   st-flash write build/nanoBooter.bin 0x08000000
   st-flash write build/nanoCLR.bin 0x08004000
   ```

2. **Deploy C# application** (from Visual Studio)
   - Open `DiSEqC_Control/DiSEqC_Control.sln`
   - Deploy to device

3. **Test** using MQTT
   ```bash
   mosquitto_pub -t diseqc/command/halt -m ''
   ```

4. **View debug output** (UART2 at 115200 baud)
   ```bash
   screen /dev/ttyUSB0 115200
   ```

---

## 🔄 Incremental Builds

After first build, you can rebuild quickly:

```bash
# Clean build (full rebuild)
rm -rf build/nanoCLR.*
./toolchain/build.ps1

# Incremental build (faster)
./toolchain/build.ps1
```

---

## 📝 Build Configuration Summary

| Setting | Value |
|---------|-------|
| MCU | STM32F407VG |
| Flash | 1MB |
| RAM | 192KB (128KB + 64KB CCM) |
| System Clock | 168MHz |
| RTOS | ChibiOS |
| Network | W5500 (SPI1) |
| Debug | UART2 @ 115200 |
| DiSEqC | TIM4 (PWM) + TIM2 (GPT) |
| LNB | I2C1 (PB8/PB9) |

---

## ✅ Verification

After build, verify files exist:

```bash
ls -lh build/
# Should show:
# - nanoCLR.bin (~400-500KB)
# - nanoCLR.hex
# - nanoCLR.elf

file build/nanoCLR.bin
# Should show: "ARM executable"
```

---

**Your Docker build environment is ready!** 🐳

To build firmware: `./toolchain/build.ps1`

For detailed testing, see: `docs/guides/TESTING_GUIDE.md`
