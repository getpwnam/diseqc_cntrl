/**
 * @file diseqc_dma.h
 * @brief DMA-based DiSEqC 1.2 Controller for STM32F407VGT6
 * @author Auto-generated for DiSEqC Motor Control Board
 * 
 * Hardware Configuration:
 * - MCU: STM32F407VGT6 @ 168MHz
 * - Timer: TIM1 Channel 1 (PA8) → LNBH26 DSQIN
 * - DMA: DMA2 Stream 5, Channel 6 (TIM1_CH1/TIM1_UP)
 * - System Clock: 168MHz, APB2 Timer Clock: 168MHz
 * 
 * Features:
 * - Non-blocking DMA-driven transmission
 * - Precise 22kHz carrier generation
 * - DiSEqC 1.2 GotoX command support
 * - Callback notification on completion
 */

#ifndef DISEQC_DMA_H
#define DISEQC_DMA_H

#include "stm32f4xx_hal.h"
#include <stdint.h>
#include <stdbool.h>

/* Configuration Constants */
#define DISEQC_CARRIER_FREQ         22000   // 22kHz carrier frequency
#define DISEQC_BIT0_HIGH_US         1000    // Bit 0: 1000µs carrier ON
#define DISEQC_BIT0_LOW_US          500     // Bit 0: 500µs carrier OFF
#define DISEQC_BIT1_HIGH_US         500     // Bit 1: 500µs carrier ON
#define DISEQC_BIT1_LOW_US          1000    // Bit 1: 1000µs carrier OFF

#define DISEQC_MAX_BYTES            6       // Maximum command bytes
#define DISEQC_MAX_BITS             (DISEQC_MAX_BYTES * 9)  // 8 data + 1 parity
#define DISEQC_MAX_SEGMENTS         (DISEQC_MAX_BITS * 2)   // 2 segments per bit

/* DiSEqC Command Bytes */
#define DISEQC_CMD_MASTER_NOREPLY   0xE0    // Command from master, no reply
#define DISEQC_ADDR_ANY_POSITIONER  0x31    // Address: Any positioner
#define DISEQC_CMD_GOTOX            0x6E    // GotoX command
#define DISEQC_CMD_HALT             0x60    // Halt positioner movement
#define DISEQC_CMD_LIMITS_OFF       0x63    // Disable limits
#define DISEQC_CMD_STORE_POS        0x6A    // Store position

/* Error Codes */
typedef enum {
    DISEQC_OK = 0,
    DISEQC_ERROR_BUSY,
    DISEQC_ERROR_INVALID_PARAM,
    DISEQC_ERROR_DMA_FAILED,
    DISEQC_ERROR_TIMEOUT
} DiSEqC_Status_t;

/* Transmission Segment (one pulse duration) */
typedef struct {
    uint16_t ccr_value;     // CCR1 value (carrier ON: duty cycle, OFF: 0)
    uint16_t arr_value;     // ARR value (duration in µs at 1MHz tick)
} DiSEqC_Segment_t;

/* DiSEqC Handle Structure */
typedef struct {
    TIM_HandleTypeDef *htim;            // Timer handle (TIM1)
    DMA_HandleTypeDef *hdma_update;     // DMA handle for timer update
    
    DiSEqC_Segment_t segments[DISEQC_MAX_SEGMENTS];  // Transmission segments
    uint16_t segment_count;             // Number of segments in buffer
    volatile uint16_t segment_index;    // Current segment being transmitted
    
    uint16_t carrier_period;            // ARR value for 22kHz carrier
    uint16_t carrier_duty;              // CCR value for 50% duty cycle
    
    volatile bool is_transmitting;      // Transmission in progress flag
    void (*tx_complete_callback)(void); // Completion callback (optional)
    
} DiSEqC_HandleTypeDef;

/* Public API Functions */

/**
 * @brief Initialize DiSEqC controller with DMA
 * @param hdiseqc Pointer to DiSEqC handle
 * @param htim Pointer to TIM1 handle (must be configured)
 * @param hdma_update Pointer to DMA handle for TIM1_UP
 * @return DISEQC_OK on success
 */
DiSEqC_Status_t DiSEqC_Init(DiSEqC_HandleTypeDef *hdiseqc, 
                            TIM_HandleTypeDef *htim,
                            DMA_HandleTypeDef *hdma_update);

/**
 * @brief Send DiSEqC GotoX command to position rotor
 * @param hdiseqc Pointer to DiSEqC handle
 * @param angle Target angle in degrees (-80.0 to +80.0)
 * @return DISEQC_OK on success, error code otherwise
 */
DiSEqC_Status_t DiSEqC_GotoAngle(DiSEqC_HandleTypeDef *hdiseqc, float angle);

/**
 * @brief Transmit raw DiSEqC command bytes
 * @param hdiseqc Pointer to DiSEqC handle
 * @param data Pointer to command bytes
 * @param length Number of bytes (1-6)
 * @return DISEQC_OK on success, error code otherwise
 */
DiSEqC_Status_t DiSEqC_Transmit(DiSEqC_HandleTypeDef *hdiseqc, 
                                const uint8_t *data, 
                                uint8_t length);

/**
 * @brief Send halt command to stop rotor movement
 * @param hdiseqc Pointer to DiSEqC handle
 * @return DISEQC_OK on success
 */
DiSEqC_Status_t DiSEqC_Halt(DiSEqC_HandleTypeDef *hdiseqc);

/**
 * @brief Check if transmission is in progress
 * @param hdiseqc Pointer to DiSEqC handle
 * @return true if transmitting, false otherwise
 */
bool DiSEqC_IsBusy(DiSEqC_HandleTypeDef *hdiseqc);

/**
 * @brief Set transmission complete callback
 * @param hdiseqc Pointer to DiSEqC handle
 * @param callback Function to call when transmission completes
 */
void DiSEqC_SetCallback(DiSEqC_HandleTypeDef *hdiseqc, 
                        void (*callback)(void));

/**
 * @brief Timer update interrupt handler (call from HAL_TIM_PeriodElapsedCallback)
 * @param hdiseqc Pointer to DiSEqC handle
 */
void DiSEqC_IRQHandler(DiSEqC_HandleTypeDef *hdiseqc);

/**
 * @brief DMA transfer complete interrupt handler
 * @param hdiseqc Pointer to DiSEqC handle
 */
void DiSEqC_DMA_CompleteCallback(DiSEqC_HandleTypeDef *hdiseqc);

#endif /* DISEQC_DMA_H */
