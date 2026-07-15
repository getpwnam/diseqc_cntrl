# Board Pin Ownership Audit (cubley-base)

Date: 2026-07-14
Scope: Active native profile `cubley-base` and current hardware revision in `hardware/kicad-project`.

## Where This Came From

- Firmware source of truth:
  - `software/nanoFramework/nf-native/board_cubley.h`
  - `software/nanoFramework/nf-native/target-overrides/nanoCLR/main.c`
  - `software/nanoFramework/nf-native/cubley_interop.cpp`
- Hardware source of truth:
  - `hardware/kicad-project/diseqc_cntrl.kicad_pcb`
  - `hardware/kicad-project/diseqc_cntrl_mcu.kicad_sch`

## Pin Table

| Port.Pin | Net (PCB) | Hardware Function | Firmware Owner | Mode/AF (cubley-base) | Pin Setup Code Location | Notes |
|---|---|---|---|---|---|---|
| PA1 | /MCU/REF_CLK | RMII REF_CLK | board init | AF11 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOA_*) | Ethernet RMII clock in |
| PA2 | /MCU/MDIO | RMII MDIO | board init | AF11 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOA_*) | Corrected from prior output-mode drift |
| PA7 | /MCU/CRS_DV | RMII CRS_DV | board init | AF11 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOA_*) | Ethernet RMII |
| PC1 | /MCU/MDC | RMII MDC | board init | AF11 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOC_*) | Ethernet RMII |
| PC4 | /MCU/RXD0 | RMII RXD0 | board init | AF11 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOC_*) | Ethernet RMII |
| PC5 | /MCU/RXD1 | RMII RXD1 | board init | AF11 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOC_*) | Ethernet RMII |
| PB11 | /MCU/TX_EN | RMII TX_EN | board init | AF11 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOB_*) | Ethernet RMII |
| PB12 | /MCU/TXD0 | RMII TXD0 | board init | AF11 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOB_*) | Ethernet RMII |
| PB13 | /MCU/TXD1 | RMII TXD1 | board init | AF11 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOB_*) | Ethernet RMII |
| PB0 | /MCU/LED_STATUS | Status LED drive | board init + StatusLed interop + boot marker | GPIO output push-pull | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOB_*); software/nanoFramework/nf-native/target-overrides/nanoCLR/main.c; software/nanoFramework/nf-native/cubley_interop.cpp:104 | LED net is active-high (anode on PB0, cathode via resistor to GND) |
| PB6 | /MCU/I2C1_SCL | I2C1 SCL | board init | AF4 OD + PU | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOB_*) | Shared bus infra |
| PB7 | /MCU/I2C1_SDA | I2C1 SDA | board init | AF4 OD + PU | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOB_*) | Shared bus infra |
| PA8 | /MCU/I2C3_SCL | LNBH26 SCL | board init | AF4 OD + PU | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOA_*) | LNB control bus |
| PC9 | /MCU/I2C3_SDA | LNBH26 SDA | board init | AF4 OD + PU | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOC_*) | LNB control bus |
| PC8 | /MCU/LNB_FLT | LNB fault input | board init | Input + PU | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOC_*) | LNB fault detect |
| PD8 | /MCU/USART3_TX | Wire protocol TX | board init/serial | AF7 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOD_*); software/nanoFramework/nf-native/target-overrides/nanoCLR/main.c | SWD/serial debug workflow |
| PD9 | /MCU/USART3_RX | Wire protocol RX | board init/serial | AF7 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOD_*); software/nanoFramework/nf-native/target-overrides/nanoCLR/main.c | SWD/serial debug workflow |
| PD12 | /MCU/TIM4_CH1 | DiSEqC output | board init | AF2 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOD_*) | Carrier generation path |
| PD14 | /MCU/TIM4_CH3 | Motor/PWM helper | board init | AF2 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOD_*) | Rotator helper path |
| PD15 | /MCU/TIM4_CH4 | Motor/PWM helper | board init | AF2 | software/nanoFramework/nf-native/board_cubley.h (VAL_GPIOD_*) | Rotator helper path |

## Ownership Conflicts Check

- PB0 (`LED_STATUS`)
  - Expected owners:
    - board init (`VAL_GPIOB_MODER` output)
    - early boot marker in `nanoCLR/main.c`
    - managed interop `StatusLed` in `cubley_interop.cpp`
  - No additional conflicting reconfiguration found in active `cubley-base` sources.

- PA2 (`MDIO`)
  - Expected owner: board init only (AF11).
  - Prior mismatch was fixed (was output in one stale configuration path).

## LED-Specific Evidence (Latest)

- Electrical polarity from PCB:
  - D3 anode is on `/MCU/LED_STATUS` (PB0 net).
  - D3 cathode routes through resistor to GND.
  - Therefore PB0 HIGH should turn LED ON.

- SWD live register probe result:
  - GPIOB ODR bit0 can be forced HIGH and LOW from debugger.
  - GPIOB IDR bit0 follows that state.
  - This confirms the MCU pin itself is switching correctly.

## Current Conclusion

Firmware ownership and pin mode for PB0 are now consistent in `cubley-base`.
If LED is still dark while PB0 is forced HIGH via SWD, the remaining fault is likely on the board path after MCU pad (LED orientation/value/population/solder/joint/trace segment), not in the current firmware pinmux.

Update (timing correction): the first boot-blink implementation used a busy-loop count that was too short at STM32F407 clock rates (sub-second total). The startup blink delays were increased to provide an actually visible multi-second early-boot pattern.