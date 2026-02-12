/**
 * @file motor_enable.c
 * @brief Motor Enable Manager Implementation
 */

#include "motor_enable.h"
#include <string.h>

/* Private Functions */

/**
 * @brief Set motor enable pin state
 */
static void motor_set_state(MotorEnable_HandleTypeDef *hmotor, bool state)
{
    GPIO_PinState pin_state = state ? GPIO_PIN_SET : GPIO_PIN_RESET;
    HAL_GPIO_WritePin(hmotor->gpio_port, hmotor->gpio_pin, pin_state);
    hmotor->motor_on = state;
}

/* Public API Implementation */

/**
 * @brief Initialize motor enable manager
 */
HAL_StatusTypeDef MotorEnable_Init(MotorEnable_HandleTypeDef *hmotor,
                                   GPIO_TypeDef *gpio_port,
                                   uint16_t gpio_pin,
                                   TIM_HandleTypeDef *htim_timeout)
{
    if (hmotor == NULL || gpio_port == NULL) {
        return HAL_ERROR;
    }
    
    memset(hmotor, 0, sizeof(MotorEnable_HandleTypeDef));
    
    hmotor->gpio_port = gpio_port;
    hmotor->gpio_pin = gpio_pin;
    hmotor->htim_timeout = htim_timeout;
    hmotor->tracking_mode = false;
    hmotor->motor_on = false;
    hmotor->timeout_remaining_ms = 0;
    
    // Ensure motor is off initially
    motor_set_state(hmotor, false);
    
    return HAL_OK;
}

/**
 * @brief Start tracking mode
 */
void MotorEnable_StartTracking(MotorEnable_HandleTypeDef *hmotor)
{
    if (hmotor == NULL) return;
    
    hmotor->tracking_mode = true;
    hmotor->timeout_remaining_ms = 0;  // Cancel any timeout
    motor_set_state(hmotor, true);
}

/**
 * @brief Stop tracking mode
 */
void MotorEnable_StopTracking(MotorEnable_HandleTypeDef *hmotor)
{
    if (hmotor == NULL) return;
    
    hmotor->tracking_mode = false;
    motor_set_state(hmotor, false);
}

/**
 * @brief Turn on motor for specified duration
 */
void MotorEnable_TurnOnMotor(MotorEnable_HandleTypeDef *hmotor, 
                             uint32_t travel_time_sec)
{
    if (hmotor == NULL) return;
    
    // Don't override tracking mode
    if (hmotor->tracking_mode) {
        return;
    }
    
    // Calculate total duration: travel time + startup time
    uint32_t total_time_ms = (travel_time_sec * 1000) + MOTOR_STARTUP_TIME_MS;
    
    // Set timeout
    hmotor->timeout_remaining_ms = total_time_ms;
    
    // Turn on motor
    motor_set_state(hmotor, true);
    
    // If we have a hardware timer, configure it
    if (hmotor->htim_timeout != NULL) {
        // Configure timer to generate interrupt after total_time_ms
        // This is optional - can rely on TickHandler instead
        // Implementation depends on timer configuration
    }
    
    // Note: Startup delay handling
    // The original C# code had Thread.Sleep(StartupTimeMs) here
    // In embedded systems, we handle this differently:
    // Option 1: Blocking delay (simple but not recommended)
    // HAL_Delay(MOTOR_STARTUP_TIME_MS);
    // Option 2: Non-blocking with TickHandler (recommended)
    // The DiSEqC transmission should wait for startup time before sending
}

/**
 * @brief Check if motor is on
 */
bool MotorEnable_IsMotorOn(MotorEnable_HandleTypeDef *hmotor)
{
    if (hmotor == NULL) return false;
    return hmotor->motor_on;
}

/**
 * @brief Timer tick handler - call every 1ms
 */
void MotorEnable_TickHandler(MotorEnable_HandleTypeDef *hmotor)
{
    if (hmotor == NULL) return;
    
    // Only handle timeout in non-tracking mode
    if (hmotor->tracking_mode) {
        return;
    }
    
    // Decrement timeout if active
    if (hmotor->timeout_remaining_ms > 0) {
        hmotor->timeout_remaining_ms--;
        
        // Turn off motor when timeout expires
        if (hmotor->timeout_remaining_ms == 0) {
            motor_set_state(hmotor, false);
        }
    }
}

/**
 * @brief Force motor off immediately
 */
void MotorEnable_ForceOff(MotorEnable_HandleTypeDef *hmotor)
{
    if (hmotor == NULL) return;
    
    hmotor->tracking_mode = false;
    hmotor->timeout_remaining_ms = 0;
    motor_set_state(hmotor, false);
}
