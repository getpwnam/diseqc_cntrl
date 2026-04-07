//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

#include "target_system_io_ports_config.h"
#include <sys_io_ser_native_target.h>

///////////
// UART1 //
///////////

// pin configuration for UART1
// port for TX pin is: GPIOA
// port for RX pin is: GPIOA
// TX pin: is GPIOA_9
// RX pin: is GPIOA_10
// GPIO alternate pin function is 7 (STM32F407 alternate function mapping)
UART_CONFIG_PINS(1, GPIOA, GPIOA, 9, 10, 7)

// initialization for UART1
UART_INIT(1)

// un-initialization for UART1
UART_UNINIT(1)

///////////
// UART3 //
///////////

// pin configuration for UART3 (board_diseqc.h uses PB10/PB11)
// port for TX pin is: GPIOB
// port for RX pin is: GPIOB
// TX pin: is GPIOB_10
// RX pin: is GPIOB_11
// GPIO alternate pin function is 7 (STM32F407 alternate function mapping)
UART_CONFIG_PINS(3, GPIOB, GPIOB, 10, 11, 7)

// initialization for UART3
UART_INIT(3)

// un-initialization for UART3
UART_UNINIT(3)
