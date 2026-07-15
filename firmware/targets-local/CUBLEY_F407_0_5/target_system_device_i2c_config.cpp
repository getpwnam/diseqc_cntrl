//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

#include <sys_dev_i2c_native_target.h>

//////////
// I2C1 //
//////////

// Cubley v0.5 FRAM bus on PB6/PB7.
I2C_CONFIG_PINS(1, GPIOB, GPIOB, 6, 7, 4)
