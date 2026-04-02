/**
 * @file board_diseqc.cpp
 * @brief Board-specific initialization for DiSEqC Controller
 */

#include <hal.h>
#include "board.h"

void boardInit(void)
{
	// Program RCC clocks from mcuconf.h (PLL/APB prescalers).
	stm32_clock_init();
}
