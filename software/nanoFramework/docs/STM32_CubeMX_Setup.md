# STM32CubeMX Configuration Guide for DiSEqC Controller

## Target Hardware
- **MCU**: STM32F407VGT6
- **System Clock**: 168MHz (HSE 8MHz + PLL)
- **DiSEqC Output**: TIM4_CH1 output pin → LNBH26 DSQIN

---

## Step-by-Step CubeMX Configuration

### 1. Clock Configuration

**RCC (Reset and Clock Control):**
- HSE: Crystal/Ceramic Resonator (8MHz)
- System Clock Mux: PLLCLK
- PLL Configuration:
  - Source: HSE
  - M (Division factor): 8
  - N (Multiplication): 336
  - P (System clock): 2
  - Result: 168MHz system clock

**Clock Tree Verification:**
- HCLK: 168MHz
- APB1: 42MHz
- APB2: 84MHz
- **APB1 Timer Clocks: 84MHz** (important for TIM4 base clocking)

### 2. TIM4 Configuration

**Mode and Configuration:**
```
Pinout & Configuration → Timers → TIM4
├── Clock Source: Internal Clock
├── Channel 1: PWM Generation CH1
└── Configuration:
    ├── Prescaler: 167
    ├── Counter Mode: Up
    ├── Counter Period (ARR): 45
    ├── Internal Clock Division: No Division
    ├── Repetition Counter: 0
    ├── Auto-reload Preload: Enable
    └── PWM Generation Channel 1:
        ├── Mode: PWM mode 1
        ├── Pulse (CCR1): 0
        ├── Output Compare Preload: Enable
        ├── Fast Mode: Disable
        ├── CH Polarity: High
```

**Parameter Settings Tab:**
```
Parameter Settings
├── Counter Settings
│   ├── Prescaler: 167          → (168MHz / 168 = 1MHz = 1µs tick)
│   ├── Counter Mode: Up
│   ├── Counter Period: 45      → (~22kHz carrier)
│   └── auto-reload preload: Enable
└── PWM Generation Channel 1
    ├── Pulse: 0                → (Start with carrier OFF)
    └── Output compare preload: Enable
```

**NVIC Settings Tab:**
```
NVIC Settings
└── TIM4 global interrupt
    └── ☑ Enabled
    └── Priority: 5 (adjust as needed)
```

**GPIO Settings Tab:**
```
GPIO Settings
└── <your DiSEqC output pin>
    ├── Signal: TIM4_CH1
    ├── GPIO mode: Alternate Function Push Pull
    ├── GPIO Pull-up/Pull-down: No pull-up and no pull-down
    ├── Maximum output speed: Very High
    └── User Label: DISEQC_OUT
```

### 3. DMA Configuration (Optional - Future Enhancement)

For the current interrupt-based implementation, DMA is **not required**.

For future full DMA implementation:
```
DMA Settings → Add
├── DMA Request: TIM4_UP (if supported/mapped in your configuration)
├── Stream: DMA2 Stream 5
├── Direction: Memory To Peripheral
├── Priority: High
├── Mode: Normal (not Circular)
└── Data Width:
    ├── Peripheral: Half Word
    └── Memory: Half Word
```

### 4. GPIO Configuration (Additional Pins)

**Motor Enable Pin (from MotorEnablerManager):**
```
GPIO → Find GPIO Pin
├── Pin: PB0 (example - adjust to your schematic)
├── GPIO mode: Output Push Pull
├── GPIO Pull-up/Pull-down: No pull-up and no pull-down
├── Maximum output speed: Low
├── User Label: MOTOR_ENABLE
└── Initial Output Level: Low
```

### 5. USART/UART Configuration (for Debug/MQTT)

If using W5500 (Ethernet) for MQTT:
- Configure SPI peripheral for W5500
- Configure chip select GPIO

If using UART for debugging:
```
USART2 (example)
├── Mode: Asynchronous
├── Baud Rate: 115200
├── Word Length: 8 Bits
├── Parity: None
└── Stop Bits: 1
```

### 6. System Configuration

**SYS Configuration:**
```
System Core → SYS
├── Debug: Serial Wire
└── Timebase Source: SysTick
```

---

## Generated Code Integration

### 1. Add DiSEqC Files to Project

Copy these files to your project:
```
Inc/
└── diseqc_dma.h

Src/
├── diseqc_dma.c
└── diseqc_example.c
```

### 2. Modify main.c

**Add includes (USER CODE BEGIN Includes):**
```c
/* USER CODE BEGIN Includes */
#include "diseqc_dma.h"
/* USER CODE END Includes */
```

**Add global variables (USER CODE BEGIN PV):**
```c
/* USER CODE BEGIN PV */
DiSEqC_HandleTypeDef hdiseqc;
/* USER CODE END PV */
```

**Add initialization (USER CODE BEGIN 2):**
```c
/* USER CODE BEGIN 2 */

// Initialize DiSEqC controller
if (DiSEqC_Init(&hdiseqc, &htim4, NULL) != DISEQC_OK) {
    Error_Handler();
}

// Start PWM output
HAL_TIM_PWM_Start(&htim4, TIM_CHANNEL_1);

// Optional: Set completion callback
// DiSEqC_SetCallback(&hdiseqc, MyCallback);

/* USER CODE END 2 */
```

**Add to main loop (USER CODE BEGIN WHILE):**
```c
/* USER CODE BEGIN WHILE */
while (1)
{
    /* USER CODE END WHILE */

    /* USER CODE BEGIN 3 */
    
    // Your MQTT processing, motor control, etc.
    // Example:
    // if (mqtt_angle_received) {
    //     DiSEqC_GotoAngle(&hdiseqc, target_angle);
    //     mqtt_angle_received = false;
    // }
}
/* USER CODE END 3 */
```

### 3. Add Timer Callback

**In main.c or stm32f4xx_it.c (USER CODE BEGIN 0 in appropriate file):**
```c
/* USER CODE BEGIN 0 */
extern DiSEqC_HandleTypeDef hdiseqc;

void HAL_TIM_PeriodElapsedCallback(TIM_HandleTypeDef *htim)
{
    if (htim->Instance == TIM4) {
        DiSEqC_IRQHandler(&hdiseqc);
    }
}
/* USER CODE END 0 */
```

---

## Project Settings

### Compiler Settings

**C/C++ → Preprocessor → Defined symbols:**
```
USE_HAL_DRIVER
STM32F407xx
```

**C/C++ → Include paths:**
```
../Inc
../Src
../Drivers/STM32F4xx_HAL_Driver/Inc
../Drivers/CMSIS/Device/ST/STM32F4xx/Include
../Drivers/CMSIS/Include
```

**C/C++ → Optimization:**
- Optimization level: -O2 (or -Os for size)
- Enable: -ffunction-sections -fdata-sections
- Linker flags: -Wl,--gc-sections

---

## Verification and Testing

### 1. Build and Flash

```bash
# Build
make

# Flash using ST-Link
st-flash write build/yourproject.bin 0x08000000

# Or use CubeProgrammer
STM32_Programmer_CLI -c port=SWD -d build/yourproject.bin 0x08000000
```

### 2. Oscilloscope Verification

**Expected signals on TIM4_CH1 output pin:**
- **Idle**: Logic LOW (0V or 3.3V depending on your circuit)
- **During transmission**:
  - 22kHz carrier bursts
  - Bit '0': 1ms carrier ON, 0.5ms OFF
  - Bit '1': 0.5ms carrier ON, 1ms OFF
  - Each bit = 1.5ms total

**Test command: GotoAngle(0.0f)**
```
Byte sequence: E0 31 6E D0 00
Expected duration: ~67.5ms (5 bytes × 9 bits × 1.5ms)
```

### 3. Debug Output

Add debug UART output to verify timing:
```c
void HAL_TIM_PeriodElapsedCallback(TIM_HandleTypeDef *htim)
{
    if (htim->Instance == TIM4) {
        // Toggle debug pin for oscilloscope
        // HAL_GPIO_TogglePin(DEBUG_GPIO_Port, DEBUG_Pin);
        
        DiSEqC_IRQHandler(&hdiseqc);
    }
}
```

---

## Troubleshooting

### Issue: No output on TIM4_CH1 output pin
**Check:**
- TIM4 clock enabled (should be automatic)
- GPIO alternate function configured
- PWM started with HAL_TIM_PWM_Start()

### Issue: Wrong carrier frequency
**Check:**
- System clock actually 168MHz (verify in debugger)
- Prescaler = 167 (for 1MHz tick)
- ARR = 45 (for ~22kHz)
- Measure with oscilloscope

### Issue: Transmission doesn't complete
**Check:**
- TIM4 interrupt enabled
- HAL_TIM_PeriodElapsedCallback() called
- DiSEqC_IRQHandler() receiving calls
- segment_index incrementing

### Issue: Motor doesn't respond
**Check:**
- LNBH26 DSQIN connection correct
- Motor enable pin activated
- LNB power present (13V/18V)
- DiSEqC command bytes correct (verify with scope)

---

## Performance Metrics

- **CPU Usage**: <5% during transmission (interrupt-driven)
- **Transmission Time**: ~13.5ms per byte (9 bits × 1.5ms)
- **Precision**: ±1µs (limited by 1MHz timer tick)
- **Max Command Rate**: ~74 commands/sec (typical usage: <1/sec)

---

## Next Steps

1. **Test basic transmission** with oscilloscope
2. **Integrate MQTT client** (W5500 Ethernet or ESP-AT WiFi)
3. **Add motor enable control** (from MotorEnablerManager.cs)
4. **Implement position tracking**
5. **Add configuration storage** (EEPROM/Flash)

