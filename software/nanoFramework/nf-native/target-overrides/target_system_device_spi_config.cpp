//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

#include <sys_dev_spi_native_target.h>

// Hardware v0.5 repurposes previous SPI lines to RMII and ADC.
// Keep stubs so target builds remain deterministic while SPI stays disabled.
void ConfigPins_SPI1(const SPI_DEVICE_CONFIGURATION& spiDeviceConfig)
{
    (void)spiDeviceConfig;
}

void ConfigPins_SPI2(const SPI_DEVICE_CONFIGURATION& spiDeviceConfig)
{
    (void)spiDeviceConfig;
}
