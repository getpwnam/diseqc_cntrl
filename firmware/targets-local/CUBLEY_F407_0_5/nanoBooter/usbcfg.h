//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

#ifndef USBCFG_H
#define USBCFG_H

// Keep nanoFramework wire protocol on USART3 even when USB CDC is enabled.
#define SERIAL_DRIVER SD3

extern const USBConfig usbcfg;
extern const SerialUSBConfig serusbcfg;
extern SerialUSBDriver SDU1;

#endif // USBCFG_H
