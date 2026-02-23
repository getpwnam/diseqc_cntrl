/**
 * @file diseqc_native.h
 * @brief Native DiSEqC driver for nanoFramework on STM32F407VGT6
 * 
 * This native driver uses ChibiOS (which nanoFramework is built on) to:
 * - Generate precise 22kHz DiSEqC carrier using TIM4 PWM
 * - Transmit DiSEqC 1.2 protocol commands
 * - Control motor enable with automatic timing
 * - Expose clean API to C# via nanoFramework interop
 * 
 * Hardware:
 * - PD12 (TIM4_CH1) → LNBH26 DSQIN
 * - PB1 (GPIO)     → Motor Enable
 */

#ifndef DISEQC_NATIVE_H
#define DISEQC_NATIVE_H

#include <hal.h>
#include <ch.h>
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/* DiSEqC Configuration */
#define DISEQC_CARRIER_FREQ         22000       // 22kHz
#define DISEQC_BIT0_HIGH_US         1000        // Bit 0: 1ms ON
#define DISEQC_BIT0_LOW_US          500         // Bit 0: 0.5ms OFF
#define DISEQC_BIT1_HIGH_US         500         // Bit 1: 0.5ms ON
#define DISEQC_BIT1_LOW_US          1000        // Bit 1: 1ms OFF
#define DISEQC_MAX_BYTES            6           // Max command bytes
#define DISEQC_MAX_SEGMENTS         (DISEQC_MAX_BYTES * 9 * 2)  // 9 bits × 2 segments

#define DISEQC_PWM_DRIVER           PWMD4
#define DISEQC_GPT_DRIVER           GPTD5
#define DISEQC_OUTPUT_LINE          PAL_LINE(GPIOD, 12U)

/* Motor Enable Configuration */
#define MOTOR_ENABLE_PAD            GPIOB_PIN1  // Adjust to your board
#define MOTOR_STARTUP_TIME_MS       2000        // Motor startup delay

/* DiSEqC Status Codes */
typedef enum {
    DISEQC_OK = 0,
    DISEQC_ERROR_BUSY = 1,
    DISEQC_ERROR_INVALID_PARAM = 2,
    DISEQC_ERROR_TIMEOUT = 3
} diseqc_status_t;

/* Transmission Segment */
typedef struct {
    uint16_t ccr_value;     // PWM duty (0 = OFF, >0 = carrier ON)
    uint16_t duration_us;   // Segment duration in microseconds
} diseqc_segment_t;

/* DiSEqC Driver Handle */
typedef struct {
    PWMDriver *pwm_driver;                          // ChibiOS PWM driver (TIM4)
    GPTDriver *gpt_driver;                          // ChibiOS GPT for timing
    
    diseqc_segment_t segments[DISEQC_MAX_SEGMENTS]; // Transmission buffer
    volatile uint16_t segment_count;                // Total segments
    volatile uint16_t segment_index;                // Current segment
    
    uint16_t carrier_duty;                          // PWM duty for carrier
    
    volatile bool is_transmitting;                  // Transmission in progress
    thread_t *tx_thread;                            // Transmission thread
    binary_semaphore_t tx_complete_sem;             // Completion semaphore
    
    float current_angle;                            // Last commanded angle
    float max_angle;                                // Maximum allowed angle
    
} diseqc_handle_t;

/* Motor Enable Handle */
typedef struct {
    ioline_t enable_line;                           // Motor enable GPIO line
    virtual_timer_t timeout_timer;                  // Motor timeout timer
    volatile bool tracking_mode;                    // Continuous enable mode
    volatile bool motor_on;                         // Current state
} motor_enable_handle_t;

/* Global Handles (initialized in board init) */
extern diseqc_handle_t g_diseqc;
extern motor_enable_handle_t g_motor;

/* Public API - Native Functions */

/**
 * @brief Initialize DiSEqC driver
 * @param pwm_driver Pointer to PWM driver (PWMD4 for TIM4)
 * @param gpt_driver Pointer to GPT driver for timing
 * @return DISEQC_OK on success
 */
diseqc_status_t diseqc_init(PWMDriver *pwm_driver, GPTDriver *gpt_driver);

/**
 * @brief Transmit DiSEqC command bytes
 * @param data Command bytes
 * @param length Number of bytes (1-6)
 * @return DISEQC_OK on success
 */
diseqc_status_t diseqc_transmit(const uint8_t *data, uint8_t length);

/**
 * @brief Send GotoX command
 * @param angle Target angle in degrees (-80 to +80)
 * @return DISEQC_OK on success
 */
diseqc_status_t diseqc_goto_angle(float angle);

/**
 * @brief Send halt command
 * @return DISEQC_OK on success
 */
diseqc_status_t diseqc_halt(void);

/**
 * @brief Drive motor East (continuous movement until halt)
 * @return DISEQC_OK on success
 */
diseqc_status_t diseqc_drive_east(void);

/**
 * @brief Drive motor West (continuous movement until halt)
 * @return DISEQC_OK on success
 */
diseqc_status_t diseqc_drive_west(void);

/**
 * @brief Step motor East (incremental movement)
 * @param steps Number of steps (1-128, typically 1 = ~1 degree)
 * @return DISEQC_OK on success
 */
diseqc_status_t diseqc_step_east(uint8_t steps);

/**
 * @brief Step motor West (incremental movement)
 * @param steps Number of steps (1-128, typically 1 = ~1 degree)
 * @return DISEQC_OK on success
 */
diseqc_status_t diseqc_step_west(uint8_t steps);

/**
 * @brief Check if transmission is in progress
 * @return true if busy
 */
bool diseqc_is_busy(void);

/**
 * @brief Get current angle
 * @return Current angle in degrees
 */
float diseqc_get_current_angle(void);

/**
 * @brief Initialize motor enable manager
 * @param enable_line GPIO line for motor enable
 * @return DISEQC_OK on success
 */
diseqc_status_t motor_enable_init(ioline_t enable_line);

/**
 * @brief Turn on motor for specified duration
 * @param travel_time_sec Travel time in seconds
 */
void motor_enable_turn_on(uint32_t travel_time_sec);

/**
 * @brief Start tracking mode (continuous enable)
 */
void motor_enable_start_tracking(void);

/**
 * @brief Stop tracking mode
 */
void motor_enable_stop_tracking(void);

/**
 * @brief Force motor off immediately
 */
void motor_enable_force_off(void);

/**
 * @brief Check if motor is enabled
 * @return true if motor is on
 */
bool motor_enable_is_on(void);

#ifdef __cplusplus
}
#endif

#endif /* DISEQC_NATIVE_H */
