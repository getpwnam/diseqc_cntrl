//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

///////////
// UART3 //
///////////

// USART3 is reserved for nanoFramework wire protocol (PD8/PD9).
// Keep System.IO.Ports from opening the same peripheral.
#define NF_SERIAL_COMM_STM32_UART_USE_USART3 FALSE
