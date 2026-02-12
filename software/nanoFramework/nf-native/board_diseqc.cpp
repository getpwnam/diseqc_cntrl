/**
 * @file board_diseqc.cpp
 * @brief Board-specific initialization for DiSEqC Controller
 */

#include <hal.h>
#include "board_diseqc.h"
#include "diseqc_native.h"

/**
 * @brief Early initialization code
 * This function is invoked immediately after the system reset before the
 * main() function is called
 */
#if HAL_USE_PAL || defined(__DOXYGEN__)
/**
 * @brief   PAL setup
 * @details Digital I/O ports static configuration as defined in @p board.h.
 *          This variable is used by the HAL when initializing the PAL driver.
 */
const PALConfig pal_default_config = {
#if STM32_HAS_GPIOA
  {VAL_GPIOA_MODER, VAL_GPIOA_OTYPER, VAL_GPIOA_OSPEEDR, VAL_GPIOA_PUPDR,
   VAL_GPIOA_ODR, VAL_GPIOA_AFRL, VAL_GPIOA_AFRH},
#endif
#if STM32_HAS_GPIOB
  {VAL_GPIOB_MODER, VAL_GPIOB_OTYPER, VAL_GPIOB_OSPEEDR, VAL_GPIOB_PUPDR,
   VAL_GPIOB_ODR, VAL_GPIOB_AFRL, VAL_GPIOB_AFRH},
#endif
#if STM32_HAS_GPIOC
  {VAL_GPIOC_MODER, VAL_GPIOC_OTYPER, VAL_GPIOC_OSPEEDR, VAL_GPIOC_PUPDR,
   VAL_GPIOC_ODR, VAL_GPIOC_AFRL, VAL_GPIOC_AFRH},
#endif
};
#endif

/**
 * @brief   Board-specific initialization code
 * @note    You can add your board-specific code here
 */
void boardInit(void) {
    // Initialize DiSEqC native driver
    diseqc_init(&PWMD1, &GPTD2);
    
    // Initialize motor enable
    motor_enable_init(MOTOR_ENABLE_LINE);
}
