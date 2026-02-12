/**
 * @file rotor_manager.h
 * @brief High-level Rotor Manager - Combines DiSEqC and Motor Enable
 * @note Ported from C# RotorManager.cs
 * 
 * This manager provides a unified interface similar to the original
 * ESP32/nanoFramework implementation.
 */

#ifndef ROTOR_MANAGER_H
#define ROTOR_MANAGER_H

#include "diseqc_dma.h"
#include "motor_enable.h"

/* Rotor Manager Handle */
typedef struct {
    DiSEqC_HandleTypeDef *hdiseqc;
    MotorEnable_HandleTypeDef *hmotor;
    
    float current_angle;
    float max_angle;
    
} RotorManager_HandleTypeDef;

/* Public API */

/**
 * @brief Initialize rotor manager
 * @param hrotor Pointer to rotor manager handle
 * @param hdiseqc Pointer to DiSEqC handle (must be initialized)
 * @param hmotor Pointer to motor enable handle (must be initialized)
 * @param max_angle Maximum allowed angle (typically 80.0)
 * @return HAL_OK on success
 */
HAL_StatusTypeDef RotorManager_Init(RotorManager_HandleTypeDef *hrotor,
                                    DiSEqC_HandleTypeDef *hdiseqc,
                                    MotorEnable_HandleTypeDef *hmotor,
                                    float max_angle);

/**
 * @brief Move rotor to specific angle
 * @param hrotor Pointer to rotor manager handle
 * @param angle Target angle in degrees (-max_angle to +max_angle)
 * @param expected_travel_time_sec Expected time to reach position (seconds)
 * @return HAL_OK on success
 */
HAL_StatusTypeDef RotorManager_GotoAngle(RotorManager_HandleTypeDef *hrotor,
                                         float angle,
                                         uint8_t expected_travel_time_sec);

/**
 * @brief Start tracking mode and move to angle
 * @param hrotor Pointer to rotor manager handle
 * @param angle Target angle in degrees
 * @return HAL_OK on success
 */
HAL_StatusTypeDef RotorManager_TrackAndGoToAngle(RotorManager_HandleTypeDef *hrotor,
                                                  float angle);

/**
 * @brief Stop tracking mode
 * @param hrotor Pointer to rotor manager handle
 */
void RotorManager_StopTracking(RotorManager_HandleTypeDef *hrotor);

/**
 * @brief Get current rotor angle
 * @param hrotor Pointer to rotor manager handle
 * @return Current angle in degrees
 */
float RotorManager_GetCurrentAngle(RotorManager_HandleTypeDef *hrotor);

/**
 * @brief Check if rotor is busy (moving or transmitting)
 * @param hrotor Pointer to rotor manager handle
 * @return true if busy, false otherwise
 */
bool RotorManager_IsBusy(RotorManager_HandleTypeDef *hrotor);

#endif /* ROTOR_MANAGER_H */
