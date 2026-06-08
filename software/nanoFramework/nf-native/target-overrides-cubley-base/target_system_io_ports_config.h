//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

#ifndef TARGET_SYSTEM_IO_PORTS_CONFIG_H
#define TARGET_SYSTEM_IO_PORTS_CONFIG_H

// UART3 is reserved for nanoFramework wire protocol (SD3 via SERIAL driver).
// Do not expose USART3 to managed System.IO.Ports.
#define NF_SERIAL_COMM_STM32_UART_USE_USART1 FALSE
#define NF_SERIAL_COMM_STM32_UART_USE_USART2 FALSE
#define NF_SERIAL_COMM_STM32_UART_USE_USART3 FALSE
#define NF_SERIAL_COMM_STM32_UART_USE_UART4  FALSE
#define NF_SERIAL_COMM_STM32_UART_USE_UART5  FALSE
#define NF_SERIAL_COMM_STM32_UART_USE_USART6 FALSE

#endif // TARGET_SYSTEM_IO_PORTS_CONFIG_H
