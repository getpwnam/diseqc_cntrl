# Build and Flash Helper Script for Windows/WSL

## Prerequisites Check
```powershell
# Check for required tools
where arm-none-eabi-gcc
where st-flash
where openocd
```

## Building the Project

### Using Makefile (STM32CubeMX Generated)
```bash
# Clean build
make clean

# Build
make -j8

# Output files
ls -lh build/diseqc_cntrl.elf
ls -lh build/diseqc_cntrl.bin
ls -lh build/diseqc_cntrl.hex
```

### Using STM32CubeIDE
```
Project → Build Project (Ctrl+B)
```

## Flashing the MCU

### Method 1: ST-Link Utility (Windows)
```powershell
# Flash binary
st-flash write build\diseqc_cntrl.bin 0x08000000

# Verify
st-flash read verify.bin 0x08000000 0x100000

# Reset MCU
st-flash reset
```

### Method 2: STM32CubeProgrammer CLI
```bash
# Connect and flash
STM32_Programmer_CLI -c port=SWD freq=4000 -w build/diseqc_cntrl.bin 0x08000000 -v -rst

# Erase full chip
STM32_Programmer_CLI -c port=SWD -e all

# Read flash
STM32_Programmer_CLI -c port=SWD -r backup.bin 0x08000000 0x100000
```

### Method 3: OpenOCD
```bash
# Start OpenOCD server
openocd -f interface/stlink.cfg -f target/stm32f4x.cfg

# In another terminal, connect with telnet
telnet localhost 4444

# Flash commands
> reset halt
> flash write_image erase build/diseqc_cntrl.bin 0x08000000
> verify_image build/diseqc_cntrl.bin 0x08000000
> reset run
> exit
```

### Method 4: GDB + OpenOCD
```bash
# Terminal 1: Start OpenOCD
openocd -f interface/stlink.cfg -f target/stm32f4x.cfg

# Terminal 2: GDB
arm-none-eabi-gdb build/diseqc_cntrl.elf
(gdb) target remote localhost:3333
(gdb) monitor reset halt
(gdb) load
(gdb) monitor reset run
(gdb) continue
```

## Debugging

### Serial Debug Output (UART2)
```bash
# Linux/WSL
minicom -D /dev/ttyUSB0 -b 115200

# Windows (PowerShell)
# Use PuTTY or TeraTerm
putty -serial COM3 -sercfg 115200,8,n,1,N
```

### Live Variables Watch
```bash
# Using GDB
arm-none-eabi-gdb build/diseqc_cntrl.elf
(gdb) target remote localhost:3333
(gdb) monitor reset halt
(gdb) break main
(gdb) continue
(gdb) print hdiseqc
(gdb) watch hdiseqc.is_transmitting
(gdb) info locals
```

### SWO (Serial Wire Output) Trace
```bash
# Configure in STM32CubeIDE:
# Run → Debug Configurations → Debugger Tab
# Enable Serial Wire Viewer (SWV)
# ITM Stimulus Port: 0

# In code:
ITM_SendChar('X');  // Send character to SWO
```

## Testing DiSEqC Signal

### Oscilloscope Setup
```
Channel 1: PA8 (DiSEqC output)
Channel 2: PB1 (Motor enable)
Trigger: Rising edge on PA8
Timebase: 2ms/div
Voltage: 1V/div (or 5V/div if using line driver)

Expected:
- 22kHz carrier during ON periods
- Bit duration: 1.5ms total
- Command duration: ~67.5ms for 5-byte GotoX
```

### Logic Analyzer
```
Sample Rate: 10MHz minimum (prefer 50MHz+)
Channels:
- D0: PA8 (DiSEqC)
- D1: PB1 (Motor enable)

Decode:
- UART decoder (not standard, but can help visualize bits)
- Custom protocol decoder for DiSEqC
```

## Size Optimization

### Compiler Flags
```makefile
# In Makefile, modify CFLAGS:
CFLAGS += -Os              # Optimize for size
CFLAGS += -flto            # Link-time optimization
CFLAGS += -ffunction-sections -fdata-sections
LDFLAGS += -Wl,--gc-sections  # Remove unused sections
```

### Check Section Sizes
```bash
arm-none-eabi-size build/diseqc_cntrl.elf

# Detailed section analysis
arm-none-eabi-nm -S -C --size-sort build/diseqc_cntrl.elf | tail -20

# Disassembly
arm-none-eabi-objdump -d build/diseqc_cntrl.elf > disasm.txt
```

## Common Build Issues

### Issue: Undefined reference to HAL functions
```bash
# Check HAL drivers included in Makefile:
grep "stm32f4xx_hal_tim.c" Makefile
grep "stm32f4xx_hal_gpio.c" Makefile

# Add if missing:
C_SOURCES += Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_tim.c
```

### Issue: Stack overflow
```bash
# Increase stack size in linker script (.ld file)
_Min_Stack_Size = 0x800;  /* Change to 0x1000 or higher */
```

### Issue: Heap too small
```bash
# Increase heap size in linker script
_Min_Heap_Size = 0x400;  /* Change to 0x800 or higher */
```

## Memory Usage Analysis

```bash
# Generate map file (should be automatic)
# Check Makefile has: LDFLAGS += -Wl,-Map=$(BUILD_DIR)/$(TARGET).map

# Analyze map file
grep -A 20 "Memory Configuration" build/diseqc_cntrl.map
grep -A 50 "Linker script and memory map" build/diseqc_cntrl.map
```

## Performance Profiling

### Measure Execution Time
```c
// Add to code:
uint32_t start = DWT->CYCCNT;
DiSEqC_GotoAngle(&hdiseqc, 45.0f);
uint32_t end = DWT->CYCCNT;
uint32_t cycles = end - start;
float us = cycles / 168.0f;  // For 168MHz clock
printf("Execution time: %.2f us\n", us);
```

### Enable DWT Cycle Counter
```c
// In main, before measurements:
CoreDebug->DEMCR |= CoreDebug_DEMCR_TRCENA_Msk;
DWT->CTRL |= DWT_CTRL_CYCCNTENA_Msk;
```

## Production Programming

### Mass Production Script
```bash
#!/bin/bash
# flash_production.sh

echo "Insert board and press Enter..."
read

# Flash bootloader (if separate)
STM32_Programmer_CLI -c port=SWD -w bootloader.bin 0x08000000

# Flash application
STM32_Programmer_CLI -c port=SWD -w build/diseqc_cntrl.bin 0x08004000

# Write unique ID (example)
# STM32_Programmer_CLI -c port=SWD -w unique_id.bin 0x0801FC00

# Verify
STM32_Programmer_CLI -c port=SWD -v

# Lock flash (optional)
# STM32_Programmer_CLI -c port=SWD -ob RDP=1

echo "Programming complete!"
```

## Continuous Integration

### GitHub Actions Example
```yaml
name: Build STM32

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      
      - name: Install ARM GCC
        run: |
          sudo apt-get update
          sudo apt-get install gcc-arm-none-eabi
      
      - name: Build
        run: make
      
      - name: Upload artifacts
        uses: actions/upload-artifact@v2
        with:
          name: firmware
          path: build/*.bin
```

---

**Happy Building!** 🛠️
