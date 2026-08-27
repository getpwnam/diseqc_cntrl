# CUBLEY_F407_0_5 Firmware Reference

This document summarizes the current Cubley firmware board configuration for:

- pin config and ownership
- active runtime transport mode
- CubleyNative interop surface

It is intended as a quick reference for bring-up and firmware changes.

## Quick Start
```sh
./bootstrap.sh
./build-flash-cubley.sh build
```

## Scope and Source of Truth

Primary code sources:

- `firmware/targets-local/CUBLEY_F407_0_5/board.h`
- `firmware/targets-local/CUBLEY_F407_0_5/target_common.c`
- `firmware/targets-local/CUBLEY_F407_0_5/common/serialcfg.h`
- `firmware/targets-local/CUBLEY_F407_0_5/CMakeLists.txt`
- `firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/cubley_interop.cpp`
- `firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/lnbh26_interop.cpp`

Supporting docs used for pin audit history and baseline context:

- `docs/debug/BOARD_PIN_OWNERSHIP_AUDIT.md`
- `docs/debug/PHASE_A_BASELINE.md`
- `docs/software/INTEROP_CONTRACT_V1.md`

## Runtime Mode (Current)

### Wire protocol transport

- Wire protocol transport: USART3 on PD8 (TX) and PD9 (RX)
- nanoFramework DebuggerPort: COM3
- Wire protocol baud: 921600
- DebugTextPort: disabled (prevents `hal_printf()` traffic from interleaving with wire protocol)

Source:

- `target_common.c` (`HAL_SYSTEM_CONFIG`)
- `common/serialcfg.h` (`SERIAL_DRIVER SD3`)

### USB CDC mode

USB CDC console support can be compiled in (`UsbCdcConsole` interop exists), but wire protocol remains on UART by design.

- Build forces `WireProtocol_ReceiverThread.c` with `HAL_USE_SERIAL_USB=FALSE`
- Result: USB CDC does not become the wire protocol transport

Source:

- `CUBLEY_F407_0_5/CMakeLists.txt`
- `nanoCLR/cubley_interop.cpp`

## Pin Config and Ownership

`board.h` uses a staged model:

- Phase 0: board-owned peripheral pins default to input + pull-up (safe state)
- Later phases: pins are switched to required AF/output modes as subsystems come up

### Active ownership table

| Pin | Net | Function | Owner | Mode/AF |
|---|---|---|---|---|
| PA1 | /MCU/REF_CLK | RMII REF_CLK | board init | AF11 |
| PA2 | /MCU/MDIO | RMII MDIO | board init | AF11 |
| PA7 | /MCU/CRS_DV | RMII CRS_DV | board init | AF11 |
| PC1 | /MCU/MDC | RMII MDC | board init | AF11 |
| PC4 | /MCU/RXD0 | RMII RXD0 | board init | AF11 |
| PC5 | /MCU/RXD1 | RMII RXD1 | board init | AF11 |
| PB11 | /MCU/TX_EN | RMII TX_EN | board init | AF11 |
| PB12 | /MCU/TXD0 | RMII TXD0 | board init | AF11 |
| PB13 | /MCU/TXD1 | RMII TXD1 | board init | AF11 |
| PB0 | /MCU/LED_STATUS | status LED | board init + interop | GPIO output push-pull |
| PB6 | /MCU/I2C1_SCL | I2C1 SCL (FRAM bus) | board init | AF4 open-drain + pull-up |
| PB7 | /MCU/I2C1_SDA | I2C1 SDA (FRAM bus) | board init | AF4 open-drain + pull-up |
| PA8 | /MCU/I2C3_SCL | I2C3 SCL (LNB bus) | board init | AF4 open-drain + pull-up |
| PC9 | /MCU/I2C3_SDA | I2C3 SDA (LNB bus) | board init | AF4 open-drain + pull-up |
| PC8 | /MCU/LNB_FLT | LNB fault input | board init | input + pull-up |
| PD8 | /MCU/USART3_TX | wire protocol TX | serial transport | AF7 |
| PD9 | /MCU/USART3_RX | wire protocol RX | serial transport | AF7 |
| PD12 | /MCU/TIM4_CH1 | DiSEqC output | board init | AF2 |
| PD14 | /MCU/TIM4_CH3 | motor/PWM helper | board init | AF2 |
| PD15 | /MCU/TIM4_CH4 | motor/PWM helper | board init | AF2 |

Notes:

- PB8/PB9 are no-connect on this board revision.
- FRAM device is on I2C1 (PB6/PB7).
- SWD is on PA13/PA14; SWO is PB3.

## CubleyNative Interop Surface (Current Native Table)

Native assembly export in `nanoCLR/cubley_interop.cpp`:

- Assembly: `CubleyNative`
- Native checksum: `0x55A991DA`
- Version tuple: `{ 1, 0, 0, 0 }`

Method slot map (native `method_lookup`):

| Slot | Native API |
|---:|---|
| 0 | `DiagMailbox.NativeSet(uint)` |
| 1 | `DiagMailbox.NativeGet()` |
| 2 | `DiagMailbox.NativeGetLastNativeError()` |
| 3 | `Fram24C128.NativeInit()` |
| 4 | `Fram24C128.NativeWrite(int, byte[], int, int)` |
| 5 | `Fram24C128.NativeRead(int, byte[], int, int)` |
| 6 | `LNBH26.NativeInit()` |
| 7 | `LNBH26.NativeSetEnable(int, bool)` |
| 8 | `LNBH26.NativeReadStatus(out int)` |
| 9 | `LNBH26.NativeReadStatusPair(out int, out int)` |
| 10 | `LNBH26.NativeSetPolarizationForChannel(int, int)` |
| 11 | `LNBH26.NativeSetBandForChannel(int, int)` |
| 12 | `LNBH26.NativeSetLowPowerForChannel(int, bool)` |
| 13 | `LNBH26.NativeSetDiseqcInputModeForChannel(int, int)` |
| 14 | `LNBH26.NativeGetPolarizationForChannel(int)` |
| 15 | `LNBH26.NativeGetBandForChannel(int)` |
| 16 | `LNBH26.NativeGetLastError()` |
| 17 | `LNBH26.NativeGetLastErrorDetail()` |
| 18 | `LNBH26Registers.NativeReadRegister(int, out int)` |
| 19 | `LNBH26Tweaks.NativeSetIsetLowForChannel(int, bool)` |
| 20 | `LNBH26Tweaks.NativeSetIswLowForChannel(int, bool)` |
| 21 | `LNBH26Tweaks.NativeGetIsetLowForChannel(int)` |
| 22 | `LNBH26Tweaks.NativeGetIswLowForChannel(int)` |
| 23 | `UsbCdcConsole.NativeIsEnabled()` |
| 24 | `UsbCdcConsole.NativeReadByte(int)` |
| 25 | `UsbCdcConsole.NativeWrite(string)` |

### Interop ownership and update rules

- Keep emitted PE method order and native `method_lookup` order aligned.
- Do not reorder or repurpose existing slots.
- Add a method only when the emitted PE order preserves the frozen 26-slot prefix.
- Recompute and validate checksum after interop changes.

See `docs/software/INTEROP_CONTRACT_V1.md` for policy and process.

## Diagnostics Words

Current native diagnostics globals (in `cubley_interop.cpp`):

- `g_cubley_diag_current_status`: transient status word
- `g_cubley_diag_last_error`: latest native error/detail word

`lnbh26_interop.cpp` and FRAM wrappers update `g_cubley_diag_last_error` with subsystem-specific encoded values for bring-up debugging.
