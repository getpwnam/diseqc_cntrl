//
// Copyright (c) .NET Foundation and Contributors
// Portions Copyright (c) 2006..2015 Giovanni Di Sirio.  All rights reserved.
// See LICENSE file in the project root for full license information.
//

#ifndef USBCFG_H
#define USBCFG_H

// Wire-protocol driver selection. usbcfg.h is only included by code paths
// that build with HAL_USE_SERIAL_USB == TRUE (e.g. nanoCLR main.c).
// Override any prior SERIAL_DRIVER definition (e.g. from serialcfg.h) so
// the same translation unit can include both headers cleanly.
#undef  SERIAL_DRIVER
#define SERIAL_DRIVER SDU1

extern const USBConfig usbcfg;
extern const SerialUSBConfig serusbcfg;
extern SerialUSBDriver SDU1;

#endif // USBCFG_H
