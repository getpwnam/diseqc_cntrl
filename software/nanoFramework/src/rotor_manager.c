/**
 * @file rotor_manager.c
 * @brief High-level Rotor Manager Implementation
 */

#include "rotor_manager.h"
#include <string.h>
#include <math.h>

/**
 * @brief Initialize rotor manager
 */
HAL_StatusTypeDef RotorManager_Init(RotorManager_HandleTypeDef *hrotor,
                                    DiSEqC_HandleTypeDef *hdiseqc,
                                    MotorEnable_HandleTypeDef *hmotor,
                                    float max_angle)
{
    if (hrotor == NULL || hdiseqc == NULL || hmotor == NULL) {
        return HAL_ERROR;
    }
    
    memset(hrotor, 0, sizeof(RotorManager_HandleTypeDef));
    
    hrotor->hdiseqc = hdiseqc;
    hrotor->hmotor = hmotor;
    hrotor->current_angle = 0.0f;
    hrotor->max_angle = max_angle;
    
    return HAL_OK;
}

/**
 * @brief Move to angle with automatic motor enable/disable
 */
HAL_StatusTypeDef RotorManager_GotoAngle(RotorManager_HandleTypeDef *hrotor,
                                         float angle,
                                         uint8_t expected_travel_time_sec)
{
    if (hrotor == NULL) {
        return HAL_ERROR;
    }
    
    // Clamp angle
    if (angle > hrotor->max_angle) angle = hrotor->max_angle;
    if (angle < -hrotor->max_angle) angle = -hrotor->max_angle;
    
    // Enable motor for specified duration
    MotorEnable_TurnOnMotor(hrotor->hmotor, expected_travel_time_sec);
    
    // Wait for motor startup time (blocking - can be improved)
    // In production, use a state machine or callback
    HAL_Delay(MOTOR_STARTUP_TIME_MS);
    
    // Send DiSEqC command
    DiSEqC_Status_t status = DiSEqC_GotoAngle(hrotor->hdiseqc, angle);
    
    if (status == DISEQC_OK) {
        hrotor->current_angle = angle;
        return HAL_OK;
    }
    
    return HAL_ERROR;
}

/**
 * @brief Track and go to angle (continuous motor enable)
 */
HAL_StatusTypeDef RotorManager_TrackAndGoToAngle(RotorManager_HandleTypeDef *hrotor,
                                                  float angle)
{
    if (hrotor == NULL) {
        return HAL_ERROR;
    }
    
    // Clamp angle
    if (angle > hrotor->max_angle) angle = hrotor->max_angle;
    if (angle < -hrotor->max_angle) angle = -hrotor->max_angle;
    
    // Enable tracking mode
    MotorEnable_StartTracking(hrotor->hmotor);
    
    // Send DiSEqC command
    DiSEqC_Status_t status = DiSEqC_GotoAngle(hrotor->hdiseqc, angle);
    
    if (status == DISEQC_OK) {
        hrotor->current_angle = angle;
        return HAL_OK;
    }
    
    return HAL_ERROR;
}

/**
 * @brief Stop tracking mode
 */
void RotorManager_StopTracking(RotorManager_HandleTypeDef *hrotor)
{
    if (hrotor != NULL) {
        MotorEnable_StopTracking(hrotor->hmotor);
    }
}

/**
 * @brief Get current angle
 */
float RotorManager_GetCurrentAngle(RotorManager_HandleTypeDef *hrotor)
{
    if (hrotor == NULL) {
        return 0.0f;
    }
    return hrotor->current_angle;
}

/**
 * @brief Check if busy
 */
bool RotorManager_IsBusy(RotorManager_HandleTypeDef *hrotor)
{
    if (hrotor == NULL) {
        return false;
    }
    
    // Check if DiSEqC transmission in progress
    if (DiSEqC_IsBusy(hrotor->hdiseqc)) {
        return true;
    }
    
    // Check if motor is enabled (still moving)
    if (MotorEnable_IsMotorOn(hrotor->hmotor)) {
        return true;
    }
    
    return false;
}
