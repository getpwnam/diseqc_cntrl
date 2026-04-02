# STM32 Hardware & nanoFramework Firmware Verification Report

## Executive Summary

✅ **Hardware and firmware are generally well-matched**, but one documentation error was identified in the native C++ code that should be corrected.

---

## 1. MCU Configuration

### Hardware (KiCAD)
- **Device**: STM32F407VGT6 (100-pin LQFP)
- **ARM Core**: Cortex M4F with FPU
- **Flash**: 1MB
- **RAM**: 192KB
- **Max Frequency**: 168MHz

### Firmware (mcuconf.h)
- **MCU Definition**: `STM32F407_MCUCONF` ✅
- **Clock Configuration**: 
  - HSE: 8MHz crystal (matches hardware) ✅
  - PLL: N=336, M=8, P=2 → sysclk = 168MHz ✅
  - APB1: /4 (42MHz)
  - APB2: /2 (84MHz)
- **HPRE**: DIV1 ✅

---

## 2. Pin Configuration Verification

### I2C1 (LNB Control - LNBH26PQR)

| Function | Hardware | Firmware (board_diseqc.h) | Status |
|----------|----------|--------------------------|--------|
| SCL | I2C1_SCL label (PB6) | PB6 (AF4, open-drain, pullup) | ✅ Match |
| SDA | I2C1_SDA label (PB7) | PB7 (AF4, open-drain, pullup) | ✅ Match |
| Address | 0x08 (7-bit) | 0x08 (I2CD1, LNB_I2C_ADDRESS) | ✅ Match |

**⚠️ ISSUE FOUND**: 
- **File**: `nf-native/lnb_control.h` (line 11)
- **Error**: Comment states `I2C Bus: I2C1 (PB8=SCL, PB9=SDA)`
- **Actual**: I2C1 uses **PB6=SCL, PB7=SDA**
- **Impact**: Documentation/code clarity issue; actual implementation is correct
- **Recommendation**: Update comment to reflect correct pin assignment

### I2C3 (FRAM Storage - FM24CL16B)

| Function | Hardware | Firmware (board_diseqc.h) | Status |
|----------|----------|--------------------------|--------|
| SCL | I2C3_SCL label (PA8) | PA8 (AF4, open-drain, pullup) | ✅ Match |
| SDA | I2C3_SDA label (PC9) | PC9 (AF4, open-drain, pullup) | ✅ Match |
| Bus | I2C3 | I2CD3 configured in mcuconf.h | ✅ Match |

**Note**: Storage schematic uses generic "SCL" and "SDA" labels but they route to PA8/PC9 (verified in MCU schematic).

### SPI1 (W5500 Ethernet)

| Function | Hardware | Firmware (board_diseqc.h) | Status |
|----------|----------|--------------------------|--------|
| SCK | SPI1_SCK (PB13) | PB13 (AF5, pushpull, high speed) | ✅ Match |
| MISO | SPI1_MISO (PB14) | PB14 (AF5, pushpull, high speed) | ✅ Match |
| MOSI | SPI1_MOSI (PB15) | PB15 (AF5, pushpull, high speed) | ✅ Match |
| CS (SCSN) | PB12 | PB12 (GPIO output, active low) | ✅ Match |
| Reset | PC6 | PC6 (GPIO output, active high) | ✅ Match |
| Interrupt | PC7 | PC7 (GPIO input, pullup) | ✅ Match |
| PHY ID | MII_LAN8742A (board header) | Configured in mcuconf.h | ✅ Match |

### USART3 (Serial Communication)

| Function | Hardware | Firmware (board_diseqc.h) | Status |
|----------|----------|--------------------------|--------|
| TX | USART3_TX (PB10) | PB10 (AF7, pushpull) | ✅ Match |
| RX | USART3_RX (PB11) | PB11 (AF7, pushpull) | ✅ Match |
| Driver | Serial Driver | SD3 (SERIAL_DRIVER) | ✅ Match |

### DiSEqC Control (PWM + GPT)

| Function | Hardware | Firmware | Status |
|----------|----------|----------|--------|
| PWM Output | PD12 (TIM4_CH1 carrier) | DISEQC_PWM_DRIVER=PWMD4, TIM4_CH1 | ✅ Match |
| GPIO Mode | Output with carrier | PIN_MODE_ALTERNATE(2), AF2 | ✅ Match |
| GPT Timer | TIM5 (bit timing) | STM32_GPT_USE_TIM5=TRUE, GPTD5 | ✅ Match |
| Frequency | 22.2kHz carrier target | 22kHz (PWM period=45@1MHz) | ✅ Match |

### LED & Status

| Function | Hardware | Firmware (board_diseqc.h) | Status |
|----------|----------|--------------------------|--------|
| LED_STATUS | PA2 (output) | PA2 (GPIO output) | ✅ Match |

### Fault Input

| Function | Hardware | Firmware | Status |
|----------|----------|----------|--------|
| LNB_FLT | PB8 (input) | Not configured (optional) | ⚠️ Note: PB8 is available for fault monitoring if needed |

---

## 3. Peripheral Configuration Summary

### Enabled in mcuconf.h ✅

| Peripheral | Config | Purpose |
|------------|--------|---------|
| I2C1 | Enabled, DMA streams assigned | LNBH26PQR control |
| I2C3 | Enabled, DMA streams assigned | FM24CL16B FRAM |
| SPI1 | Enabled, DMA streams configured | W5500 Ethernet |
| USART3 | Enabled | Serial debug/deployment |
| TIM4 (PWM) | Enabled for DiSEqC | 22kHz carrier generation |
| TIM5 (GPT) | Enabled for DiSEqC | Bit timing (1µs resolution) |
| HSE | 8MHz external crystal | ✅ Correct |

### DMA Configuration

- **I2C1 RX**: DMA1_Stream0 ✅
- **I2C1 TX**: DMA1_Stream6 ✅
- **I2C3 RX**: DMA1_Stream2 ✅
- **I2C3 TX**: DMA1_Stream4 ✅
- **SPI1 RX**: DMA2_Stream0 ✅
- **SPI1 TX**: DMA2_Stream3 ✅

---

## 4. Clock Frequency Analysis

### Clock Tree (mcuconf.h)

```
STM32F407 Clock Configuration:
├── HSE: 8MHz external oscillator
├── PLL:
│   ├── Input (PLLSRC): HSE (8MHz)
│   ├── Prescaler (PLLM): 8
│   ├── Multiplier (PLLN): 336
│   ├── Output Divider (PLLP): 2
│   └── Result: (8/8)*336/2 = 168MHz ✅
├── System Clock: PLL (168MHz)
├── AHB (HPRE): DIV1 = 168MHz ✅
├── APB1 (PPRE1): DIV4 = 42MHz ✅
├── APB2 (PPRE2): DIV2 = 84MHz ✅
└── 48MHz Clock: PLLQ=7 = 48MHz (USB, RNG) ✅
```

### Timer Clocks

- **TIM4 (PWM)**: APB1 clock = 42MHz base, prescaled for 1MHz PWM clock ✅
- **TIM5 (GPT)**: APB1 clock = 42MHz base, prescaled for 1MHz timer clock ✅
- **UART3**: APB1 clock = 42MHz ✅

---

## 5. Issues & Recommendations

### 🔴 Critical Issues
**None found** - Hardware and firmware are compatible.

### 🟡 Issues Found

#### Issue #1: Documentation Error in lnb_control.h
- **Severity**: Medium (documentation only, code is correct)
- **Location**: `nf-native/lnb_control.h`, line 11
- **Current (Wrong)**: 
  ```c
  * I2C Bus: I2C1 (PB8=SCL, PB9=SDA)
  ```
- **Should Be**:
  ```c
  * I2C Bus: I2C1 (PB6=SCL, PB7=SDA)
  ```
- **Why**: PB6/PB7 are configured as I2C1 in both mcuconf.h and board_diseqc.h. PB8 is used for LNB fault input, not I2C.
- **Action**: Recommend fixing comment to prevent future confusion

### 🟢 Recommendations (Optional Improvements)

1. **LNB Fault Monitoring**: PB8 (LNB_FLT input) is available in hardware but not configured in firmware. Consider adding if fault detection is needed.

2. **Pin Documentation**: Add pin assignment comments to MCU schematic or create a pinout reference table in the README for future maintenance.

3. **Configuration Validation**: Consider adding a compile-time check or test that verifies pin assignments match between board_diseqc.h and CMakeLists.txt/mcuconf.h.

---

## 6. Build Configuration

### CMakeLists.txt Verification ✅

- **Linker Script**: `STM32F407xG_CLR` (correct for STM32F407VGT6 with 1MB flash)
- **Native Sources**: 
  - `diseqc_native.cpp` ✅
  - `lnb_control.cpp` ✅
  - `diseqc_interop.cpp` ✅
  - `lnb_interop.cpp` ✅
- **Bootloader**: Separate `STM32F407xG_booter` linker script ✅
- **Stack Sizes**: 1KB boot stack, 2KB main stack reasonable for Cortex M4 with 192KB RAM ✅

---

## 7. Validation Checklist

| Item | Status | Notes |
|------|--------|-------|
| MCU Model Match | ✅ | STM32F407VGT6 in both HW and FW |
| HSE Frequency | ✅ | 8MHz crystal in hardware, configured in mcuconf.h |
| I2C1 Pins | ✅ | PB6/PB7, but comment error exists |
| I2C3 Pins | ✅ | PA8/PC9 correct |
| SPI1 Pins | ✅ | PB13/14/15 + PB12/PC6/PC7 control signals |
| USART3 Pins | ✅ | PB10/11 correct |
| PWM Output | ✅ | PD12/TIM4_CH1 correct |
| GPT Timer | ✅ | TIM5 correct |
| Clock Calc | ✅ | 168MHz PLL output correct |
| Flash Layout | ✅ | 1MB flash, linker scripts match |
| DMA Config | ✅ | Streams properly assigned, no conflicts |

---

## Conclusion

**The firmware and hardware configurations are compatible and ready for build.** 

The only issue identified is a **documentation error** in the LNB control header that should be corrected for code clarity. All critical hardware parameters (MCU, clock, pins, peripherals) are properly matched between the KiCAD schematic and the firmware configuration files.

### Next Steps
1. ✅ Fix the I2C pin documentation in `nf-native/lnb_control.h`
2. ✅ Proceed with firmware build
3. (Optional) Consider adding LNB fault input handling if desired
