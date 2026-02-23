/**
 * @file diseqc_example.c
 * @brief Example usage of DiSEqC DMA controller for STM32F407VGT6
 * 
 * This file demonstrates how to integrate the DiSEqC controller
 * into a typical STM32CubeMX generated project with MQTT support.
 */

#include "main.h"
#include "diseqc_dma.h"

/* Private variables */
DiSEqC_HandleTypeDef hdiseqc;
extern TIM_HandleTypeDef htim4;
extern DMA_HandleTypeDef hdma_tim4_up;

/* Private function prototypes */
void DiSEqC_TransmitComplete(void);
void MQTT_OnAngleCommand(float angle);

/**
 * @brief Initialize DiSEqC controller
 * Call this after MX_TIM4_Init() in main.c
 */
void DiSEqC_Setup(void)
{
    DiSEqC_Status_t status;
    
    // Initialize DiSEqC controller
    status = DiSEqC_Init(&hdiseqc, &htim4, NULL);
    
    if (status != DISEQC_OK) {
        // Handle initialization error
        Error_Handler();
    }
    
    // Set completion callback (optional)
    DiSEqC_SetCallback(&hdiseqc, DiSEqC_TransmitComplete);
    
    // Start PWM output
    HAL_TIM_PWM_Start(&htim4, TIM_CHANNEL_1);
}

/**
 * @brief DiSEqC transmission complete callback
 * This is called when transmission finishes
 */
void DiSEqC_TransmitComplete(void)
{
    // Optionally publish MQTT status
    // MQTT_Publish("diseqc/status", "idle");
    
    // Or toggle LED
    // HAL_GPIO_TogglePin(LED_GPIO_Port, LED_Pin);
}

/**
 * @brief MQTT message handler for angle commands
 * Subscribe to: "diseqc/angle"
 * Payload format: "-45.5" (angle in degrees)
 */
void MQTT_OnAngleCommand(float angle)
{
    DiSEqC_Status_t status;
    
    // Check if busy
    if (DiSEqC_IsBusy(&hdiseqc)) {
        // Optionally publish error
        // MQTT_Publish("diseqc/error", "busy");
        return;
    }
    
    // Send GotoX command
    status = DiSEqC_GotoAngle(&hdiseqc, angle);
    
    if (status == DISEQC_OK) {
        // Optionally publish acknowledgment
        // char msg[32];
        // sprintf(msg, "moving to %.1f", angle);
        // MQTT_Publish("diseqc/status", msg);
    } else {
        // Handle error
        // MQTT_Publish("diseqc/error", "failed");
    }
}

/**
 * @brief HAL Timer Period Elapsed Callback
 * This is called by HAL on timer update interrupt
 * REQUIRED: Add this to stm32f4xx_it.c or your main.c
 */
void HAL_TIM_PeriodElapsedCallback(TIM_HandleTypeDef *htim)
{
    if (htim->Instance == TIM4) {
        // Handle DiSEqC timing
        DiSEqC_IRQHandler(&hdiseqc);
    }
}

/**
 * @brief Example usage in main loop
 */
void DiSEqC_Example_Usage(void)
{
    // Initialize
    DiSEqC_Setup();
    
    // Example 1: Go to specific angle
    DiSEqC_GotoAngle(&hdiseqc, 45.0f);  // Move to 45° East
    
    // Wait for completion (blocking example)
    while (DiSEqC_IsBusy(&hdiseqc)) {
        HAL_Delay(10);
    }
    
    HAL_Delay(5000);  // Wait 5 seconds for motor to reach position
    
    // Example 2: Go to negative angle
    DiSEqC_GotoAngle(&hdiseqc, -30.0f);  // Move to 30° West
    
    while (DiSEqC_IsBusy(&hdiseqc)) {
        HAL_Delay(10);
    }
    
    HAL_Delay(5000);
    
    // Example 3: Halt movement
    DiSEqC_Halt(&hdiseqc);
    
    // Example 4: Send custom command
    uint8_t custom_cmd[] = {0xE0, 0x31, 0x63};  // Limits OFF
    DiSEqC_Transmit(&hdiseqc, custom_cmd, 3);
}

/**
 * @brief Non-blocking usage with MQTT integration
 */
void DiSEqC_MQTT_Integration_Example(void)
{
    // In your MQTT message received callback:
    // 
    // if (strcmp(topic, "diseqc/angle") == 0) {
    //     float angle = atof(message);
    //     MQTT_OnAngleCommand(angle);
    // }
    // else if (strcmp(topic, "diseqc/halt") == 0) {
    //     if (!DiSEqC_IsBusy(&hdiseqc)) {
    //         DiSEqC_Halt(&hdiseqc);
    //     }
    // }
}
