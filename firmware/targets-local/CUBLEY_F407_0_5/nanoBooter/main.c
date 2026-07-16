//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//
// CUBLEY_F407_0_5 nanoBooter entry point — ultra-minimal baseline.
//
// Responsibilities:
//   * Initialise ChibiOS HAL and clock tree.
//   * If a valid nanoCLR image is present in flash, jump to it immediately.
//   * Otherwise stay resident so the host can flash a new CLR image via
//     the wire protocol on USART3 (PD8/PD9 @ 921600).
//   * Blink the status LED (PB0) at 1 Hz so an operator can see the board
//     is stuck in the booter.
//
// Phase 0 has no user-button "hold to stay in booter" gate — Cubley v0.5
// does not have a user button. A future revision may add a UART "hold key"
// or mailbox flag if we ever need one.

#include <ch.h>
#include <hal.h>
#include <cmsis_os.h>

#include <serialcfg.h>
#include <usbcfg.h>
#include <targetHAL.h>
#include <WireProtocol_ReceiverThread.h>
#include <LaunchCLR.h>

osThreadDef(ReceiverThread, osPriorityHigh, 2048, "ReceiverThread");

static THD_WORKING_AREA(waUsbCdcInitThread, 768);
static THD_FUNCTION(UsbCdcInitThread, arg)
{
    (void)arg;
    chRegSetThreadName("USB_CDC_Init");

    sduObjectInit(&SDU1);
    sduStart(&SDU1, &serusbcfg);

    usbDisconnectBus(serusbcfg.usbp);
    chThdSleepMilliseconds(100);
    usbStart(serusbcfg.usbp, &usbcfg);
    usbConnectBus(serusbcfg.usbp);

    chThdExit(MSG_OK);
}

static bool CubleyHasValidClrImageAt(uint32_t imageBase)
{
    const uint32_t initialSp = *((uint32_t *)imageBase);
    const uint32_t resetVector = *((uint32_t *)(imageBase + 4U));
    const uint32_t resetAddress = resetVector & ~1U;

    if (initialSp == 0U || initialSp == 0xFFFFFFFFU)
    {
        return false;
    }

    if ((resetVector & 1U) == 0U)
    {
        return false;
    }

    if (resetAddress < FLASH1_MEMORY_StartAddress ||
        resetAddress >= (FLASH1_MEMORY_StartAddress + FLASH1_MEMORY_Size))
    {
        return false;
    }

    return true;
}

static const SerialConfig cubley_wp_serial_cfg = {
    921600,
    0,
    USART_CR2_STOP1_BITS,
    0
};

int main(void)
{
    halInit();
    InitBootClipboard();
    osKernelInitialize();
    osDelay(20); // let clocks settle before probing flash

    // If flash contains a valid CLR image, launch it.
    if (CubleyHasValidClrImageAt((uint32_t)&__nanoImage_end__))
    {
        LaunchCLR((uint32_t)&__nanoImage_end__);
    }

    // No valid CLR — stay in booter and accept updates over USART3.
    palSetLineMode(PAL_LINE(GPIOD, 8U), PAL_MODE_ALTERNATE(7));
    palSetLineMode(PAL_LINE(GPIOD, 9U), PAL_MODE_ALTERNATE(7));
    sdStart(&SERIAL_DRIVER, &cubley_wp_serial_cfg);

    // Bring up USB CDC as an independent console channel. Wire protocol
    // remains bound to SERIAL_DRIVER (SD3 / USART3) via serialcfg.h.
    chThdCreateStatic(
        waUsbCdcInitThread,
        sizeof(waUsbCdcInitThread),
        NORMALPRIO + 2,
        UsbCdcInitThread,
        NULL);

    osThreadCreate(osThread(ReceiverThread), NULL);
    osKernelStart();

    // Keep booter alive without toggling any GPIO heartbeat.
    while (true)
    {
        osDelay(1000);
    }
}
