//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

#include <sys_dev_spi_native_target.h>

//////////
// SPI1 //
//////////

// pin configuration for SPI1
// port for SCK pin is: SPI1_SCLK
// port for MISO pin is: SPI1_MISO
// port for MOSI pin is: SPI1_MOSI
void ConfigPins_SPI1(const SPI_DEVICE_CONFIGURATION& spiDeviceConfig)
{
	(void)spiDeviceConfig;

	palSetLineMode(PAL_LINE(GPIOA, 5U), PAL_MODE_ALTERNATE(5));
	palSetLineMode(PAL_LINE(GPIOA, 6U), PAL_MODE_ALTERNATE(5));
	palSetLineMode(PAL_LINE(GPIOA, 7U), PAL_MODE_ALTERNATE(5));
}

//////////
// SPI2 //
//////////

// pin configuration for SPI2
// port for SCK pin is: SPI2_SCLK
// port for MISO pin is: SPI2_MISO
// port for MOSI pin is: SPI2_MOSI
void ConfigPins_SPI2(const SPI_DEVICE_CONFIGURATION& spiDeviceConfig)
{
	(void)spiDeviceConfig;

	palSetLineMode(PAL_LINE(GPIOB, 13U), PAL_MODE_ALTERNATE(5));
	palSetLineMode(PAL_LINE(GPIOB, 14U), PAL_MODE_ALTERNATE(5));
	palSetLineMode(PAL_LINE(GPIOB, 15U), PAL_MODE_ALTERNATE(5));
}
