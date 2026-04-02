# Managed Code Deployment Guide

This guide covers deploying managed C# code to the DiSEqC Controller STM32F407 running nanoFramework.

## Hardware Bring-up Path → Managed Deployment

### Lessons Applied from Bringup

1. **Clock Initialization** (boardInit fix)
   - `boardInit()` now calls `stm32_clock_init()` to program RCC from mcuconf.h
   - Ensures PLL and clock tree are initialized for all profiles
   - Without this fix: RCC stays at reset values (HSI 16MHz, no PLL) → UART baud mismatch

2. **HSI PLL for HSE-less Boards**
   - If HSE crystal isn't oscillating, enable HSI_PLL profile flag
   - Bringup profiles (bringup-smoke, bringup-hardalive) have `ENABLE_HSI_PLL=1` by default
   - For production profiles (minimal, w5500-native): add `ENABLE_HSI_PLL=1` if clock hangs detected
   - Configuration: `STM32_PLLSRC=STM32_PLLSRC_HSI`, `PLLM=8`, `PLLN=168`, `PLLP=2`

3. **Stable UART for Wire Protocol**
   - USART3: PB10 (TX), PB11 (RX)
   - Configured at 115200 8N1
   - Confirmed working via bringup-smoke heartbeat output

---

## Deployment Profiles

### minimal (Recommended for Managed Apps)
- **Status**: Production-ready
- **Flash used**: ~281 KB (55% of available)
- **Features**: Full nanoFramework CLR, GPIO/I2C/SPI support, no networking
- **Build**: `./toolchain/build.sh minimal`
- **Profile config**: RTC enabled, config block enabled, HSI PLL enabled (`ENABLE_HSI_PLL=1`)

### w5500-native (Native W5500 over SPI)
- **Status**: Experimental
- **Features**: System.Device.Spi for W5500, no System.Net (lwIP), custom transport
- **Build**: `./toolchain/build.sh w5500-native`

### network (Deprecated)
- **Status**: Scheduled for removal
- **Features**: Full System.Net + lwIP (assumes STM32 internal MAC)
- **Note**: Our board uses external W5500, not internal MAC; use w5500-native instead

### bringup-smoke (Hardware + UART Proof)
- **Status**: Bring-up only
- **Purpose**: PA2 LED blink + USART3 heartbeat for electrical/clock verification
- **Build**: `./toolchain/build.sh bringup-smoke`

### bringup-hardalive (Bare-metal Execution Proof)
- **Status**: Bring-up only
- **Purpose**: direct register-level PA2/PB10 toggling without HAL/RTOS/CLR
- **Build**: `./toolchain/build.sh bringup-hardalive`

---

## Wire Protocol Deployment

nanoFramework deploys managed code via **Wire Protocol** over UART (USART3).

### Prerequisites

1. **nanoff CLI tool** (nanoFramework device deployer)
   ```bash
   dotnet tool install -g nanoff
   ```

2. **Device identification**
   - Get serial port: `ls /dev/ttyUSB* /dev/ttyACM*`
   - Baud rate: 115200

### Flash Prerequisites

1. **Build and flash nanoCLR** (from [software/nanoFramework/](.)/)
   ```bash
   ./toolchain/build.sh minimal
   st-flash write build/nanoBooter.bin 0x08000000
   st-flash write build/nanoCLR.bin 0x08004000
   ```

2. **Verify boot** via USART3 (115200 8N1)
   - Should see wire protocol ready message or clean boot without errors
   - If RTC hangs (20s+ delay): enable HSI_PLL for that profile

### Deploy Managed Application

From [DiSEqC_Control/](../DiSEqC_Control/) project directory:

1. **PowerShell (Windows)**
   ```powershell
   ..\toolchain\build-managed-cli.ps1 -Configuration Release `
       -Deploy -SerialPort COM3 -Address 0x080C0000
   ```

2. **Bash (Linux/WSL)**
   ```bash
   CONFIGURATION=Release ../toolchain/build-managed-cli.sh \
       --deploy --serialport /dev/ttyUSB0 --address 0x080C0000
   ```

3. **nanoff (standalone)**
   ```bash
   nanoff --nanodevice \
       --serialport /dev/ttyUSB0 \
       --baud 115200 \
       --deploy \
       --image DiSEqC_Control.pe \
       --address 0x080C0000 \
       --reset
   ```

**Address**: `0x080C0000` is the managed deployment region (see QUICK_START.md for layout).

---

## Troubleshooting

### UART Garbled or Silent
1. Check baud rate (115200 8N1)
2. Verify boardInit() calls stm32_clock_init() in board_diseqc.cpp
3. If RTC hangs for ~20s: enable HSI_PLL for that profile in build.sh
4. Check RCC state via GDB:
   ```gdb
   target extended-remote ...
   monitor reset run
   shell sleep 1
   monitor halt
   print/x *(unsigned int*)0x40023800  # RCC_CR
   print/x *(unsigned int*)0x40023808  # RCC_CFGR
   ```

### Deploy Fails — No Wire Protocol
1. Ensure nanoCLR booted cleanly (UART should be idle, no errors)
2. Try `nanoff` with increased timeout: `--reset` flag and 5s wait
3. Check deployment address matches memory layout: `0x080C0000` for this target
4. Confirm transport path is true bidirectional bridge (USB-UART preferred over interactive bridge tools)

### Deploy Succeeds but App Doesn't Run
1. Verify managed app (C#) compiles cleanly in Visual Studio
2. Check app entry point (Program.cs Main()) and required namespaces
3. Review nanoff log for warnings about missing assemblies

---

## Next Steps

1. **Monitor UART** during boot to confirm no hangs
2. **Deploy test app** (BlinkBringup in tests/ or custom GPIO toggle)
3. **Validate I2C/SPI** communication to LNB controller and W5500
4. **Enable W5500 driver** and test network transport

---

## Flash Layout

| Region | Address | Size | Purpose |
|--------|---------|------|---------|
| Sector 0 | 0x08000000 | 16 KB | nanoBooter (bootloader) |
| Sector 1-2 | 0x08004000 | 72 KB | nanoCLR (runtime kernel) |
| Sectors 3-6 | 0x0800C000 | 192 KB | nanoCLR continuation |
| Sectors 7-10 | 0x080C0000 | 256 KB | Managed app deployment zone |

---

## References

- [QUICK_START.md](./QUICK_START.md) — Build + flash commands
- [DOCKER_BUILD_GUIDE.md](./docs/guides/DOCKER_BUILD_GUIDE.md) — Docker build details
- [board_diseqc.cpp](./nf-native/board_diseqc.cpp) — Target board init (includes stm32_clock_init)

---

## HSI/HSE Incident Log (Resolved)

### Symptom

- Bring-up serial output was garbled or missing.
- Managed deployment failed because wire protocol was unstable or unavailable.

### Root Cause Chain

1. `boardInit()` did not call `stm32_clock_init()`, so RCC remained near reset defaults.
2. Build script profile overrides for HSI PLL were appended too early and later overwritten by a broad `mcuconf.h` copy.
3. Some profile logic depended on preprocessor-style guards that were not reliably provided as compiler defines.

### What Was Changed

1. `nf-native/board_diseqc.cpp`
   - `boardInit()` now calls `stm32_clock_init()`.
2. `toolchain/build.sh`
   - HSI PLL profile handling moved to script-level logic and re-applied after target config copies.
   - `minimal` profile now forces `ENABLE_HSI_PLL=1` for robust startup on HSE-less/unverified boards.
   - `minimal` profile enables config block so deploy tools can obtain device/deployment metadata.

### Validation Evidence

- Bring-up smoke UART showed clean `[bringup-smoke] start` and heartbeat output on USART3 @115200 8N1.
- RCC register snapshots confirmed PLL active and selected.

### Operational Guidance

- Use `bringup-smoke` when validating raw board/clock/UART behavior.
- Use `minimal` for managed deployment and test-app execution.
