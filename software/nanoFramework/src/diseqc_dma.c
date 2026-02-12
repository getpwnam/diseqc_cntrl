/**
 * @file diseqc_dma.c
 * @brief DMA-based DiSEqC 1.2 Controller Implementation
 */

#include "diseqc_dma.h"
#include <math.h>
#include <string.h>

/* Private Helper Functions */

/**
 * @brief Calculate even parity bit
 * @param byte Data byte
 * @return 0 if even parity, 1 if odd parity
 */
static uint8_t calculate_parity(uint8_t byte)
{
    uint8_t parity = 0;
    for (int i = 0; i < 8; i++) {
        parity ^= (byte >> i) & 1;
    }
    return parity;
}

/**
 * @brief Add a single bit to the transmission buffer
 * @param hdiseqc Pointer to DiSEqC handle
 * @param bit_value 0 or 1
 */
static void add_bit(DiSEqC_HandleTypeDef *hdiseqc, uint8_t bit_value)
{
    uint16_t high_duration, low_duration;
    
    if (bit_value) {
        // Bit '1': 500µs carrier ON, 1000µs carrier OFF
        high_duration = DISEQC_BIT1_HIGH_US;
        low_duration = DISEQC_BIT1_LOW_US;
    } else {
        // Bit '0': 1000µs carrier ON, 500µs carrier OFF
        high_duration = DISEQC_BIT0_HIGH_US;
        low_duration = DISEQC_BIT0_LOW_US;
    }
    
    // Segment 1: Carrier ON period
    hdiseqc->segments[hdiseqc->segment_count].ccr_value = hdiseqc->carrier_duty;
    hdiseqc->segments[hdiseqc->segment_count].arr_value = high_duration - 1;
    hdiseqc->segment_count++;
    
    // Segment 2: Carrier OFF period
    hdiseqc->segments[hdiseqc->segment_count].ccr_value = 0;
    hdiseqc->segments[hdiseqc->segment_count].arr_value = low_duration - 1;
    hdiseqc->segment_count++;
}

/**
 * @brief Add a byte with parity to transmission buffer
 * @param hdiseqc Pointer to DiSEqC handle
 * @param byte Data byte
 */
static void add_byte_with_parity(DiSEqC_HandleTypeDef *hdiseqc, uint8_t byte)
{
    // Add 8 data bits (MSB first)
    for (int i = 7; i >= 0; i--) {
        add_bit(hdiseqc, (byte >> i) & 1);
    }
    
    // Add parity bit (DiSEqC uses odd parity transmission)
    // If data has even parity (parity=0), transmit '1'
    // If data has odd parity (parity=1), transmit '0'
    uint8_t parity = calculate_parity(byte);
    add_bit(hdiseqc, parity == 0 ? 1 : 0);
}

/**
 * @brief Start DMA transmission of next segment
 * @param hdiseqc Pointer to DiSEqC handle
 */
static void start_next_segment(DiSEqC_HandleTypeDef *hdiseqc)
{
    if (hdiseqc->segment_index >= hdiseqc->segment_count) {
        // Transmission complete
        hdiseqc->htim->Instance->CCR1 = 0;  // Carrier OFF
        hdiseqc->htim->Instance->CR1 &= ~TIM_CR1_CEN;  // Stop timer
        hdiseqc->is_transmitting = false;
        
        // Call completion callback if registered
        if (hdiseqc->tx_complete_callback != NULL) {
            hdiseqc->tx_complete_callback();
        }
        return;
    }
    
    // Load next segment
    DiSEqC_Segment_t *seg = &hdiseqc->segments[hdiseqc->segment_index];
    
    // Update CCR1 (duty cycle - carrier ON/OFF)
    hdiseqc->htim->Instance->CCR1 = seg->ccr_value;
    
    // Update ARR (segment duration)
    hdiseqc->htim->Instance->ARR = seg->arr_value;
    
    // Reset counter and generate update event
    hdiseqc->htim->Instance->CNT = 0;
    hdiseqc->htim->Instance->EGR = TIM_EGR_UG;
    
    hdiseqc->segment_index++;
}

/* Public API Implementation */

/**
 * @brief Initialize DiSEqC controller
 */
DiSEqC_Status_t DiSEqC_Init(DiSEqC_HandleTypeDef *hdiseqc, 
                            TIM_HandleTypeDef *htim,
                            DMA_HandleTypeDef *hdma_update)
{
    if (hdiseqc == NULL || htim == NULL) {
        return DISEQC_ERROR_INVALID_PARAM;
    }
    
    // Clear handle structure
    memset(hdiseqc, 0, sizeof(DiSEqC_HandleTypeDef));
    
    hdiseqc->htim = htim;
    hdiseqc->hdma_update = hdma_update;
    hdiseqc->is_transmitting = false;
    hdiseqc->tx_complete_callback = NULL;
    
    // Calculate timer values for 22kHz carrier at 1MHz tick rate
    // Timer clock = 168MHz (APB2)
    // Prescaler = 168 - 1 = 167 → 1MHz tick rate (1µs per tick)
    // At 1MHz: 22kHz period = 1000000 / 22000 = 45.45 ticks ≈ 45
    // For 50% duty: CCR1 = 22 (or 23 for better approximation)
    
    uint32_t timer_clock = HAL_RCC_GetPCLK2Freq();
    if (htim->Instance == TIM1 || htim->Instance == TIM8) {
        timer_clock *= 2;  // APB2 timers run at 2x when APB2 prescaler > 1
    }
    
    uint16_t prescaler = (timer_clock / 1000000) - 1;  // 1MHz = 1µs tick
    
    hdiseqc->carrier_period = 45;   // ~22.2kHz at 1MHz tick
    hdiseqc->carrier_duty = 22;     // ~49% duty cycle
    
    // Configure timer
    htim->Instance->PSC = prescaler;
    htim->Instance->ARR = hdiseqc->carrier_period;
    htim->Instance->CCR1 = 0;  // Start with carrier OFF
    htim->Instance->CNT = 0;
    
    // Configure PWM mode 1 on channel 1
    htim->Instance->CCMR1 &= ~TIM_CCMR1_OC1M;
    htim->Instance->CCMR1 |= TIM_CCMR1_OC1M_1 | TIM_CCMR1_OC1M_2;  // PWM mode 1
    htim->Instance->CCMR1 |= TIM_CCMR1_OC1PE;  // Preload enable
    
    // Enable channel 1 output
    htim->Instance->CCER |= TIM_CCER_CC1E;
    
    // Enable auto-reload preload
    htim->Instance->CR1 |= TIM_CR1_ARPE;
    
    // Enable update interrupt
    htim->Instance->DIER |= TIM_DIER_UIE;
    
    // Generate update event to load all registers
    htim->Instance->EGR = TIM_EGR_UG;
    
    // Enable main output (required for TIM1)
    if (htim->Instance == TIM1 || htim->Instance == TIM8) {
        htim->Instance->BDTR |= TIM_BDTR_MOE;
    }
    
    return DISEQC_OK;
}

/**
 * @brief Transmit DiSEqC command bytes
 */
DiSEqC_Status_t DiSEqC_Transmit(DiSEqC_HandleTypeDef *hdiseqc, 
                                const uint8_t *data, 
                                uint8_t length)
{
    if (hdiseqc == NULL || data == NULL) {
        return DISEQC_ERROR_INVALID_PARAM;
    }
    
    if (length == 0 || length > DISEQC_MAX_BYTES) {
        return DISEQC_ERROR_INVALID_PARAM;
    }
    
    if (hdiseqc->is_transmitting) {
        return DISEQC_ERROR_BUSY;
    }
    
    // Build transmission segment buffer
    hdiseqc->segment_count = 0;
    hdiseqc->segment_index = 0;
    
    for (uint8_t i = 0; i < length; i++) {
        add_byte_with_parity(hdiseqc, data[i]);
    }
    
    if (hdiseqc->segment_count == 0) {
        return DISEQC_ERROR_INVALID_PARAM;
    }
    
    // Start transmission
    hdiseqc->is_transmitting = true;
    
    // Load first segment and start timer
    start_next_segment(hdiseqc);
    
    // Enable timer
    hdiseqc->htim->Instance->CR1 |= TIM_CR1_CEN;
    
    return DISEQC_OK;
}

/**
 * @brief Send GotoX command
 */
DiSEqC_Status_t DiSEqC_GotoAngle(DiSEqC_HandleTypeDef *hdiseqc, float angle)
{
    if (hdiseqc == NULL) {
        return DISEQC_ERROR_INVALID_PARAM;
    }
    
    // Clamp angle to valid range
    if (angle > 80.0f) angle = 80.0f;
    if (angle < -80.0f) angle = -80.0f;
    
    // Build DiSEqC 1.2 GotoX command
    uint8_t cmd[5];
    
    cmd[0] = DISEQC_CMD_MASTER_NOREPLY;  // 0xE0
    cmd[1] = DISEQC_ADDR_ANY_POSITIONER;  // 0x31
    cmd[2] = DISEQC_CMD_GOTOX;            // 0x6E
    
    // Calculate position value (angle * 16)
    // Direction nibble: 0xD = East (positive), 0xE = West (negative)
    uint8_t direction = (angle < 0) ? 0xE0 : 0xD0;
    int16_t angle_16 = (int16_t)(16.0f * fabsf(angle) + 0.5f);
    
    cmd[3] = direction | ((angle_16 >> 8) & 0x0F);
    cmd[4] = angle_16 & 0xFF;
    
    return DiSEqC_Transmit(hdiseqc, cmd, 5);
}

/**
 * @brief Send halt command
 */
DiSEqC_Status_t DiSEqC_Halt(DiSEqC_HandleTypeDef *hdiseqc)
{
    uint8_t cmd[3];
    
    cmd[0] = DISEQC_CMD_MASTER_NOREPLY;
    cmd[1] = DISEQC_ADDR_ANY_POSITIONER;
    cmd[2] = DISEQC_CMD_HALT;
    
    return DiSEqC_Transmit(hdiseqc, cmd, 3);
}

/**
 * @brief Check if transmission is in progress
 */
bool DiSEqC_IsBusy(DiSEqC_HandleTypeDef *hdiseqc)
{
    if (hdiseqc == NULL) {
        return false;
    }
    return hdiseqc->is_transmitting;
}

/**
 * @brief Set completion callback
 */
void DiSEqC_SetCallback(DiSEqC_HandleTypeDef *hdiseqc, void (*callback)(void))
{
    if (hdiseqc != NULL) {
        hdiseqc->tx_complete_callback = callback;
    }
}

/**
 * @brief Timer interrupt handler - call this from HAL_TIM_PeriodElapsedCallback
 */
void DiSEqC_IRQHandler(DiSEqC_HandleTypeDef *hdiseqc)
{
    if (hdiseqc == NULL || !hdiseqc->is_transmitting) {
        return;
    }
    
    // Load next segment
    start_next_segment(hdiseqc);
}

/**
 * @brief DMA transfer complete callback (future enhancement)
 */
void DiSEqC_DMA_CompleteCallback(DiSEqC_HandleTypeDef *hdiseqc)
{
    // For future DMA-driven implementation
    // Currently using interrupt-driven approach
    (void)hdiseqc;
}
