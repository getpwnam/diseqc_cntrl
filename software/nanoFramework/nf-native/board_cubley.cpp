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
	RCC->AHB1ENR |= RCC_AHB1ENR_GPIOAEN;  // PA2 (LED_STATUS), PA11/PA12 (USB OTG_FS D-/D+)
	RCC->AHB1ENR |= RCC_AHB1ENR_GPIOBEN;  // PB10, PB11 (diagnostic reference/W5500 SPI)
	RCC->AHB1ENR |= RCC_AHB1ENR_GPIOCEN;  // PC6 (W5500 reset), PC7 (W5500 INT), PC9 (RMII CRSDV)
	(void)RCC->AHB1ENR;

	// Configure PA11 and PA12 for USB OTG_FS:
	//   MODER:  AF mode (10b)
	//   OTYPER: push-pull (0)
	//   OSPEEDR: very high speed (11b) - required for full-speed USB
	//   PUPDR:  no pull (00b) - the bus has external biasing
	//   AFRH:   alternate function 10 (OTG_FS)
	//
	// The ChibiOS hal_usb_lld.c does NOT touch GPIO mux/AF: it expects the
	// board init to have done it before usb_lld_start() is called. Without
	// this block, PA11/PA12 stay at reset (input mode), the OTG_FS PHY
	// never sees D+/D-, and the host reports Code 43.
	GPIOA->MODER   = (GPIOA->MODER   & ~((3u << (11u*2u)) | (3u << (12u*2u))))
	               | ((2u << (11u*2u)) | (2u << (12u*2u)));
	GPIOA->OTYPER &= ~((1u << 11u) | (1u << 12u));
	GPIOA->OSPEEDR = (GPIOA->OSPEEDR & ~((3u << (11u*2u)) | (3u << (12u*2u))))
	               | ((3u << (11u*2u)) | (3u << (12u*2u)));
	GPIOA->PUPDR  &= ~((3u << (11u*2u)) | (3u << (12u*2u)));
	GPIOA->AFRH    = (GPIOA->AFRH    & ~((0xFu << ((11u-8u)*4u)) | (0xFu << ((12u-8u)*4u))))
	               | ((10u << ((11u-8u)*4u)) | (10u << ((12u-8u)*4u)));
}
