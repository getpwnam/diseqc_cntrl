/**
 * @file board_cubley.cpp
 * @brief Board-specific initialization for DiSEqC Controller
 */

#include <hal.h>
#include "board.h"

void boardInit(void)
{
	// Program RCC clocks from mcuconf.h (PLL/APB prescalers).
	stm32_clock_init();

	// Enable GPIOA clock so managed code can drive PA2 (LED_STATUS).
	RCC->AHB1ENR |= RCC_AHB1ENR_GPIOAEN;
}
