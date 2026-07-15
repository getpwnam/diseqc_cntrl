//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//
// CUBLEY_F407_0_5 nanoCLR entry point — ultra-minimal baseline.
//
// Responsibilities:
//   * Initialise ChibiOS HAL and clock tree.
//   * Configure USART3 (PD8 TX / PD9 RX, AF7) at 921600 as the wire-protocol
//     transport. This is the same transport nanoBooter used, so the host
//     debugger stays connected across the booter → CLR handoff.
//   * Spin up ReceiverThread (wire protocol) and CLRStartupThread.
//
// Everything else (CRC, RNG, watchdog, USB CDC, board diagnostics, LED
// heartbeat) is deliberately out of scope for Phase 0. It is added back in
// later phases as bring-up requires it.

#include <ch.h>
#include <hal.h>
#include <cmsis_os.h>

#include <serialcfg.h>
#include <CLR_Startup_Thread.h>
#include <WireProtocol_ReceiverThread.h>
#include <nanoCLR_Application.h>
#include <nanoHAL_v2.h>

osThreadDef(ReceiverThread,     osPriorityHigh,   2048, "ReceiverThread");
osThreadDef(CLRStartupThread,   osPriorityNormal, 4096, "CLRStartupThread");

// USART3 (SD3) wire-protocol configuration.
// CR2 STOP1 bits, no CR1 or CR3 overrides.
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
    Watchdog_Init();

    // Bring up USART3 pins (PD8 = TX AF7, PD9 = RX AF7) and start SD3.
    // board.h leaves these as INPUT+PULLUP for Phase 0; the wire-protocol
    // transport is the earliest consumer, so pins are switched here rather
    // than in board.c PAL init.
    palSetLineMode(PAL_LINE(GPIOD, 8U), PAL_MODE_ALTERNATE(7));
    palSetLineMode(PAL_LINE(GPIOD, 9U), PAL_MODE_ALTERNATE(7));
    sdStart(&SERIAL_DRIVER, &cubley_wp_serial_cfg);

    // Kick off wire-protocol receiver and CLR startup.
    osThreadCreate(osThread(ReceiverThread), NULL);

    CLR_SETTINGS clrSettings;
    (void)memset(&clrSettings, 0, sizeof(CLR_SETTINGS));
    clrSettings.MaxContextSwitches         = 50;
    clrSettings.WaitForDebugger            = false;
    clrSettings.EnterDebuggerLoopAfterExit = true;

    osThreadCreate(osThread(CLRStartupThread), &clrSettings);
    osKernelStart();

    while (true)
    {
        osDelay(100);
    }
}
