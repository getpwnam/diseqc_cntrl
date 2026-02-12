# STM32F407VGT6 DiSEqC Implementation Guide

## Hardware Configuration
- **MCU**: STM32F407VGT6 @ 168MHz
- **Timer**: TIM1 Channel 1 (PA8)
- **Output**: Connected to LNBH26 DSQIN pin
- **System Clock**: 168MHz (PLL from 8MHz HSE)

## Implementation Strategy

### Single Timer + DMA Approach (Recommended)

Use **TIM1** with DMA to generate:
1. Continuous 22kHz carrier (50% duty PWM)
2. OOK modulation by switching CCR between ON (50%) and OFF (0%)

## Complete C Implementation

```c
/* diseqc.h */
#ifndef DISEQC_H
#define DISEQC_H

#include "stm32f4xx_hal.h"
#include <stdint.h>
#include <stdbool.h>

#define DISEQC_CARRIER_FREQ     22000   // 22kHz
#define DISEQC_BIT0_HIGH_US     1000    // Bit 0: 1000µs high
#define DISEQC_BIT0_LOW_US      500     // Bit 0: 500µs low
#define DISEQC_BIT1_HIGH_US     500     // Bit 1: 500µs high
#define DISEQC_BIT1_LOW_US      1000    // Bit 1: 1000µs low

#define DISEQC_MAX_BYTES        6
#define DISEQC_MAX_BITS         (DISEQC_MAX_BYTES * 9)  // 8 data + 1 parity per byte
#define DISEQC_MAX_PULSES       (DISEQC_MAX_BITS * 2)   // 2 pulses per bit (high + low)

typedef struct {
    TIM_HandleTypeDef *htim;
    DMA_HandleTypeDef *hdma_ccr;
    DMA_HandleTypeDef *hdma_arr;
    uint16_t timer_period;      // ARR value for 22kHz
    uint16_t carrier_duty;      // CCR value for 50% duty
    volatile bool transmitting;
} DiSEqC_Handle_t;

void DiSEqC_Init(DiSEqC_Handle_t *hdiseqc, TIM_HandleTypeDef *htim);
void DiSEqC_GotoAngle(DiSEqC_Handle_t *hdiseqc, float angle, uint8_t speed);
void DiSEqC_TransmitBytes(DiSEqC_Handle_t *hdiseqc, const uint8_t *data, uint8_t len);

#endif

/* diseqc.c */
#include "diseqc.h"
#include <math.h>

// DMA buffers for pulse timings
static uint16_t dma_ccr_buffer[DISEQC_MAX_PULSES];
static uint16_t dma_arr_buffer[DISEQC_MAX_PULSES];
static uint16_t dma_pulse_count = 0;

// Parity calculation
static uint8_t calculate_parity(uint8_t byte) {
    uint8_t parity = 0;
    for (int i = 0; i < 8; i++) {
        parity ^= (byte >> i) & 1;
    }
    return parity;  // 0 = even, 1 = odd
}

// Add bit to DMA buffers
static void add_bit(DiSEqC_Handle_t *hdiseqc, bool bit_value) {
    uint16_t high_time, low_time;
    
    if (bit_value) {
        // Bit '1': 500µs ON, 1000µs OFF
        high_time = DISEQC_BIT1_HIGH_US;
        low_time = DISEQC_BIT1_LOW_US;
    } else {
        // Bit '0': 1000µs ON, 500µs OFF
        high_time = DISEQC_BIT0_HIGH_US;
        low_time = DISEQC_BIT0_LOW_US;
    }
    
    // ON period: CCR = 50% duty, ARR = high_time
    dma_ccr_buffer[dma_pulse_count] = hdiseqc->carrier_duty;
    dma_arr_buffer[dma_pulse_count] = high_time;
    dma_pulse_count++;
    
    // OFF period: CCR = 0% duty, ARR = low_time
    dma_ccr_buffer[dma_pulse_count] = 0;
    dma_arr_buffer[dma_pulse_count] = low_time;
    dma_pulse_count++;
}

// Add byte with parity to DMA buffers
static void add_byte_with_parity(DiSEqC_Handle_t *hdiseqc, uint8_t byte) {
    // Add 8 data bits (MSB first)
    for (int i = 7; i >= 0; i--) {
        add_bit(hdiseqc, (byte >> i) & 1);
    }
    
    // Add parity bit (DiSEqC uses odd parity = transmit '1' if even parity)
    uint8_t parity = calculate_parity(byte);
    add_bit(hdiseqc, parity == 0);  // Even parity → send '1', Odd parity → send '0'
}

/**
 * @brief Initialize DiSEqC controller
 * @param hdiseqc DiSEqC handle
 * @param htim Timer handle (TIM1 configured for PWM on channel 1)
 */
void DiSEqC_Init(DiSEqC_Handle_t *hdiseqc, TIM_HandleTypeDef *htim) {
    hdiseqc->htim = htim;
    hdiseqc->transmitting = false;
    
    // Calculate timer values for 22kHz carrier
    // Timer clock = 168MHz (or APB2 timer clock)
    // For 22kHz: Period = 168MHz / 22kHz = 7636.36 ≈ 7636
    // With prescaler = 0: ARR = 7636, CCR = 3818 (50% duty)
    
    // However, we need 1µs resolution for pulse timing
    // Use prescaler to get 1MHz tick rate: PSC = 168 - 1 = 167
    // At 1MHz: 22kHz period = 1MHz / 22kHz = 45.45 ≈ 45 ticks
    
    uint32_t timer_clock = HAL_RCC_GetPCLK2Freq() * 2;  // TIM1 on APB2
    uint16_t prescaler = (timer_clock / 1000000) - 1;    // 1µs tick rate
    
    hdiseqc->timer_period = 45;         // ~22kHz at 1MHz tick
    hdiseqc->carrier_duty = 22;         // 50% duty cycle
    
    // Configure timer
    htim->Instance->PSC = prescaler;
    htim->Instance->ARR = hdiseqc->timer_period;
    htim->Instance->CCR1 = 0;           // Start with carrier OFF
    htim->Instance->EGR = TIM_EGR_UG;   // Update registers
}

/**
 * @brief Transmit DiSEqC command bytes
 * @param hdiseqc DiSEqC handle
 * @param data Pointer to command bytes
 * @param len Number of bytes to transmit
 */
void DiSEqC_TransmitBytes(DiSEqC_Handle_t *hdiseqc, const uint8_t *data, uint8_t len) {
    if (hdiseqc->transmitting || len > DISEQC_MAX_BYTES) {
        return;
    }
    
    hdiseqc->transmitting = true;
    dma_pulse_count = 0;
    
    // Build pulse sequence
    for (uint8_t i = 0; i < len; i++) {
        add_byte_with_parity(hdiseqc, data[i]);
    }
    
    // Configure DMA for CCR (duty cycle control)
    // Note: You need to configure DMA channels in CubeMX:
    // - DMA2_Stream1 or DMA2_Stream5 for TIM1_CH1 (CCR1)
    // - DMA2_Stream5 for TIM1_UP (ARR)
    
    // Method 1: Update both CCR and ARR via DMA
    // This requires two DMA channels working in sync
    
    // Method 2: Use two-timer approach (see alternative below)
    
    // For simplicity, we'll use software timing here
    // In production, use DMA + timer update interrupts
    
    TIM_HandleTypeDef *tim = hdiseqc->htim;
    
    for (uint16_t i = 0; i < dma_pulse_count; i++) {
        tim->Instance->CCR1 = dma_ccr_buffer[i];
        tim->Instance->ARR = dma_arr_buffer[i];
        tim->Instance->CNT = 0;
        tim->Instance->CR1 |= TIM_CR1_CEN;  // Start timer
        
        // Wait for period to complete (blocking - use DMA in production)
        while ((tim->Instance->SR & TIM_SR_UIF) == 0);
        tim->Instance->SR &= ~TIM_SR_UIF;
    }
    
    tim->Instance->CR1 &= ~TIM_CR1_CEN;  // Stop timer
    tim->Instance->CCR1 = 0;              // Ensure carrier is off
    
    hdiseqc->transmitting = false;
}

/**
 * @brief Send DiSEqC GotoX command to position rotor
 * @param hdiseqc DiSEqC handle
 * @param angle Angle in degrees (-80.0 to +80.0)
 * @param speed Movement speed (not used in standard GotoX)
 */
void DiSEqC_GotoAngle(DiSEqC_Handle_t *hdiseqc, float angle, uint8_t speed) {
    uint8_t cmd[5];
    
    // Clamp angle
    if (angle > 80.0f) angle = 80.0f;
    if (angle < -80.0f) angle = -80.0f;
    
    // DiSEqC 1.2 GotoX command
    cmd[0] = 0xE0;  // Command from master, no reply expected
    cmd[1] = 0x31;  // Address: Any positioner
    cmd[2] = 0x6E;  // Command: GotoX (Drive Motor to Angular Position)
    
    // Calculate position value (16 * angle)
    uint8_t direction = (angle < 0) ? 0xE0 : 0xD0;
    int16_t angle_16 = (int16_t)(16.0f * fabsf(angle) + 0.5f);
    
    cmd[3] = direction | ((angle_16 >> 8) & 0x0F);
    cmd[4] = angle_16 & 0xFF;
    
    DiSEqC_TransmitBytes(hdiseqc, cmd, 5);
}
```

## Hardware-Optimized DMA Implementation

For non-blocking operation, use DMA to update timer registers:

```c
/**
 * @brief Initialize DMA for DiSEqC transmission
 * Configure in CubeMX:
 * - DMA2 Stream 5, Channel 6: TIM1_UP → Memory-to-Peripheral, Half-word
 * - Link to TIM1 Update Event
 */
void DiSEqC_Init_DMA(DiSEqC_Handle_t *hdiseqc) {
    // Enable DMA request on timer update
    __HAL_TIM_ENABLE_DMA(hdiseqc->htim, TIM_DMA_UPDATE);
    
    // Configure DMA to update both CCR1 and ARR
    // This requires custom DMA configuration to alternate between registers
    // See STM32 Reference Manual for advanced DMA techniques
}

/**
 * @brief DMA Transfer Complete Callback
 */
void HAL_TIM_PWM_PulseFinishedCallback(TIM_HandleTypeDef *htim) {
    if (htim->Instance == TIM1) {
        // Transmission complete
        htim->Instance->CCR1 = 0;  // Carrier OFF
        // Signal completion (semaphore, flag, etc.)
    }
}
```

## Two-Timer Alternative (More Complex but Cleaner)

```c
/**
 * TIM1: Generate 22kHz carrier (continuous PWM)
 * TIM2: Control timing (trigger TIM1 gate via TRGO)
 * 
 * TIM1 configured in "Gated Mode" triggered by TIM2
 * DMA updates TIM2 ARR to control ON/OFF durations
 */

void DiSEqC_Init_TwoTimer(void) {
    // TIM1: 22kHz carrier
    TIM1->PSC = 167;      // 1MHz tick
    TIM1->ARR = 45;       // ~22kHz
    TIM1->CCR1 = 22;      // 50% duty
    TIM1->CCMR1 = TIM_CCMR1_OC1M_1 | TIM_CCMR1_OC1M_2;  // PWM mode 1
    TIM1->CCER = TIM_CCER_CC1E;
    
    // Configure TIM1 as slave, gated by TIM2
    TIM1->SMCR = (1 << TIM_SMCR_SMS_Pos) |   // Gated mode
                 (0 << TIM_SMCR_TS_Pos);      // ITR0 = TIM2 TRGO
    
    // TIM2: Gate control
    TIM2->PSC = 167;      // 1MHz tick
    TIM2->CR2 = TIM_CR2_MMS_2;  // OC1REF as TRGO
    TIM2->DIER = TIM_DIER_UDE;  // DMA on update
    
    // DMA updates TIM2->ARR with pulse durations
}
```

## STM32CubeMX Configuration

### TIM1 Settings:
- **Clock Source**: Internal Clock
- **Channel 1**: PWM Generation CH1
- **Prescaler**: 167 (for 168MHz → 1MHz tick)
- **Counter Period (ARR)**: 45 (for ~22kHz)
- **Pulse (CCR1)**: 22 (50% duty)

### GPIO Settings:
- **PA8**: TIM1_CH1, Alternate Function Push-Pull, High Speed

### DMA Settings (Optional for advanced implementation):
- **DMA2 Stream 5**: TIM1_UP, Memory-to-Peripheral, Half-word

## Usage Example

```c
DiSEqC_Handle_t hdiseqc;
TIM_HandleTypeDef htim1;

int main(void) {
    HAL_Init();
    SystemClock_Config();
    
    // Initialize peripherals (via CubeMX generated code)
    MX_TIM1_Init();
    
    // Initialize DiSEqC
    DiSEqC_Init(&hdiseqc, &htim1);
    
    // Start PWM
    HAL_TIM_PWM_Start(&htim1, TIM_CHANNEL_1);
    
    while (1) {
        // Command via MQTT, etc.
        DiSEqC_GotoAngle(&hdiseqc, 45.0f, 5);
        HAL_Delay(5000);
        
        DiSEqC_GotoAngle(&hdiseqc, -30.0f, 5);
        HAL_Delay(5000);
    }
}
```

## Performance Notes

- **Blocking Implementation**: Simple but CPU-intensive
- **DMA Implementation**: Best performance, requires careful setup
- **Two-Timer Method**: Hardware-only solution, no CPU intervention

For production, use **DMA + interrupts** for non-blocking operation with MQTT integration.
