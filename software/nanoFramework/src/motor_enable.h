/**
 * @file motor_enable.h
 * @brief Motor Enable Manager for DiSEqC Rotor Control
 * @note Ported from C# MotorEnablerManager.cs
 * 
 * Controls the motor power enable signal with timing management:
 * - Temporary enable for timed movements
 * - Continuous enable for tracking mode
 * - Startup delay handling
 */

#ifndef MOTOR_ENABLE_H
#define MOTOR_ENABLE_H

#include "stm32f4xx_hal.h"
#include <stdint.h>
#include <stdbool.h>

/* Configuration */
#define MOTOR_ENABLE_PIN         GPIO_PIN_1      // Adjust to match your hardware
#define MOTOR_ENABLE_PORT        GPIOB           // Adjust to match your hardware
#define MOTOR_STARTUP_TIME_MS    2000            // Motor startup time

/* Motor Enable Handle */
typedef struct {
    GPIO_TypeDef *gpio_port;
    uint16_t gpio_pin;
    
    bool tracking_mode;                 // Continuous enable for tracking
    volatile bool motor_on;             // Current motor state
    
    TIM_HandleTypeDef *htim_timeout;    // Timer for automatic shutoff
    uint32_t timeout_remaining_ms;      // Remaining timeout in ms
    
} MotorEnable_HandleTypeDef;

/* Public API */

/**
 * @brief Initialize motor enable manager
 * @param hmotor Pointer to motor enable handle
 * @param gpio_port GPIO port for enable pin
 * @param gpio_pin GPIO pin number
 * @param htim_timeout Optional timer for timeout management (can be NULL)
 * @return HAL_OK on success
 */
HAL_StatusTypeDef MotorEnable_Init(MotorEnable_HandleTypeDef *hmotor,
                                   GPIO_TypeDef *gpio_port,
                                   uint16_t gpio_pin,
                                   TIM_HandleTypeDef *htim_timeout);

/**
 * @brief Start tracking mode (continuous motor enable)
 * @param hmotor Pointer to motor enable handle
 */
void MotorEnable_StartTracking(MotorEnable_HandleTypeDef *hmotor);

/**
 * @brief Stop tracking mode (disable motor)
 * @param hmotor Pointer to motor enable handle
 */
void MotorEnable_StopTracking(MotorEnable_HandleTypeDef *hmotor);

/**
 * @brief Turn on motor for specified duration
 * @param hmotor Pointer to motor enable handle
 * @param travel_time_sec Expected travel time in seconds
 * @note Adds startup time automatically
 */
void MotorEnable_TurnOnMotor(MotorEnable_HandleTypeDef *hmotor, 
                             uint32_t travel_time_sec);

/**
 * @brief Check if motor is currently enabled
 * @param hmotor Pointer to motor enable handle
 * @return true if motor is on, false otherwise
 */
bool MotorEnable_IsMotorOn(MotorEnable_HandleTypeDef *hmotor);

/**
 * @brief Timer tick handler - call this every 1ms (e.g., from SysTick)
 * @param hmotor Pointer to motor enable handle
 */
void MotorEnable_TickHandler(MotorEnable_HandleTypeDef *hmotor);

/**
 * @brief Immediate motor shutoff (emergency)
 * @param hmotor Pointer to motor enable handle
 */
void MotorEnable_ForceOff(MotorEnable_HandleTypeDef *hmotor);

#endif /* MOTOR_ENABLE_H */
