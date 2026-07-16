//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//
// SERIAL_DRIVER selection for CUBLEY_F407_0_5.
// USART3 (PD8 TX / PD9 RX, AF7) is the nanoFramework wire-protocol transport.
// The shared WireProtocol_HAL_Interface.c picks up this macro when
// HAL_USE_SERIAL is TRUE and HAL_USE_SERIAL_USB is FALSE (see halconf.h).

#ifndef SERIALCFG_H
#define SERIALCFG_H

#define SERIAL_DRIVER           SD3

#endif // SERIALCFG_H
