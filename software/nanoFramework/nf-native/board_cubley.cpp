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

	// Enable GPIO clocks for diagnostic and peripheral pins.
	RCC->AHB1ENR |= RCC_AHB1ENR_GPIOAEN;  // PA2 (LED_STATUS)
	RCC->AHB1ENR |= RCC_AHB1ENR_GPIOBEN;  // PB10, PB11 (diagnostic reference/W5500 SPI)
	RCC->AHB1ENR |= RCC_AHB1ENR_GPIOCEN;  // PC6 (W5500 reset), PC7 (W5500 INT), PC9 (RMII CRSDV)
}
