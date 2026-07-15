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
#include <string.h>

#include <serialcfg.h>
#include <CLR_Startup_Thread.h>
#include <WireProtocol_ReceiverThread.h>
#include <nanoCLR_Application.h>
#include <nanoHAL_v2.h>
#include "fram_native.h"
#include "lnbh26_native.h"

static void FramSelfTestThread(void const *argument);
static void LnbSelfTestThread(void const *argument);

osThreadDef(ReceiverThread,     osPriorityHigh,   2048, "ReceiverThread");
osThreadDef(CLRStartupThread,   osPriorityNormal, 4096, "CLRStartupThread");
osThreadDef(FramSelfTestThread, osPriorityBelowNormal, 1024, "FramSelfTestThread");
osThreadDef(LnbSelfTestThread,  osPriorityBelowNormal, 1024, "LnbSelfTestThread");

volatile uint32_t g_fram_native_selftest_thread_marker = 0;
volatile uint32_t g_fram_native_selftest_status = 0;
volatile uint32_t g_fram_native_selftest_i2c = 0;

volatile uint32_t g_lnb_native_selftest_thread_marker = 0;
volatile uint32_t g_lnb_native_selftest_status = 0;
volatile uint32_t g_lnb_native_selftest_i2c = 0;
volatile uint32_t g_lnb_native_selftest_status1 = 0;

static void RunFramNativeSelfTest(void)
{
    fram_handle_t *hfram = fram_get_global_handle();
    static const uint8_t pattern[] = {0x46, 0x52, 0x41, 0x4D, 0x4E, 0x41, 0x54, 0x01};
    uint8_t readBack[sizeof(pattern)] = {0};

    // Format: 0xF4SSCCDD
    g_fram_native_selftest_status = 0xF4000001u;

    fram_status_t initStatus = fram_init(hfram, &I2CD1, 0x50);
    g_fram_native_selftest_i2c = (uint32_t)(fram_get_last_i2c_msg() & 0xFFFF);
    if (initStatus != FRAM_OK)
    {
        g_fram_native_selftest_status = 0xF4010000u | ((uint32_t)initStatus & 0xFFu);
        return;
    }

    fram_status_t writeStatus = fram_write(hfram, 0x0100, pattern, (uint16_t)sizeof(pattern));
    g_fram_native_selftest_i2c = (uint32_t)(fram_get_last_i2c_msg() & 0xFFFF);
    if (writeStatus != FRAM_OK)
    {
        g_fram_native_selftest_status = 0xF4020000u | ((uint32_t)writeStatus & 0xFFu);
        return;
    }

    fram_status_t readStatus = fram_read(hfram, 0x0100, readBack, (uint16_t)sizeof(readBack));
    g_fram_native_selftest_i2c = (uint32_t)(fram_get_last_i2c_msg() & 0xFFFF);
    if (readStatus != FRAM_OK)
    {
        g_fram_native_selftest_status = 0xF4030000u | ((uint32_t)readStatus & 0xFFu);
        return;
    }

    if (memcmp(pattern, readBack, sizeof(pattern)) != 0)
    {
        g_fram_native_selftest_status = 0xF4040001u;
        return;
    }

    static const uint8_t clearBuf[sizeof(pattern)] = {0};
    (void)fram_write(hfram, 0x0100, clearBuf, (uint16_t)sizeof(clearBuf));
    g_fram_native_selftest_i2c = (uint32_t)(fram_get_last_i2c_msg() & 0xFFFF);
    g_fram_native_selftest_status = 0xF4FF0001u;
}

static void FramSelfTestThread(void const *argument)
{
    (void)argument;
    g_fram_native_selftest_thread_marker = 0xF4AA0001u;

    chThdSleepMilliseconds(3000);
    g_fram_native_selftest_thread_marker = 0xF4AA0002u;

    RunFramNativeSelfTest();
    g_fram_native_selftest_thread_marker = 0xF4AA00FFu;

    while (true)
    {
        osDelay(1000);
    }
}

static void RunLnbNativeSelfTest(void)
{
    int32_t statusReg = 0;

    // Format: 0xB2SSCCDD
    g_lnb_native_selftest_status = 0xB2000001u;

    int32_t initStatus = lnb_native_init();
    g_lnb_native_selftest_i2c = (uint32_t)(lnb_get_last_i2c_msg() & 0xFFFF);
    if (initStatus != (int32_t)LNB_OK)
    {
        g_lnb_native_selftest_status = 0xB2010000u | ((uint32_t)initStatus & 0xFFu);
        return;
    }

    int32_t polarizationStatus = lnb_native_set_polarization((int32_t)LNB_NATIVE_POLARIZATION_VERTICAL);
    g_lnb_native_selftest_i2c = (uint32_t)(lnb_get_last_i2c_msg() & 0xFFFF);
    if (polarizationStatus != (int32_t)LNB_OK)
    {
        g_lnb_native_selftest_status = 0xB2020000u | ((uint32_t)polarizationStatus & 0xFFu);
        return;
    }

    int32_t bandStatus = lnb_native_set_band((int32_t)LNB_NATIVE_BAND_LOW);
    g_lnb_native_selftest_i2c = (uint32_t)(lnb_get_last_i2c_msg() & 0xFFFF);
    if (bandStatus != (int32_t)LNB_OK)
    {
        g_lnb_native_selftest_status = 0xB2030000u | ((uint32_t)bandStatus & 0xFFu);
        return;
    }

    int32_t enableStatus = lnb_native_set_enable(1);
    g_lnb_native_selftest_i2c = (uint32_t)(lnb_get_last_i2c_msg() & 0xFFFF);
    if (enableStatus != (int32_t)LNB_OK)
    {
        g_lnb_native_selftest_status = 0xB2040000u | ((uint32_t)enableStatus & 0xFFu);
        return;
    }

    int32_t readStatus = lnb_native_read_status(&statusReg);
    g_lnb_native_selftest_i2c = (uint32_t)(lnb_get_last_i2c_msg() & 0xFFFF);
    g_lnb_native_selftest_status1 = (uint32_t)statusReg;
    if (readStatus != (int32_t)LNB_OK)
    {
        g_lnb_native_selftest_status = 0xB2050000u | ((uint32_t)readStatus & 0xFFu);
        return;
    }

    if (lnb_native_get_polarization() != (int32_t)LNB_POL_VERTICAL)
    {
        g_lnb_native_selftest_status = 0xB2060001u;
        return;
    }

    if (lnb_native_get_band() != (int32_t)LNB_BAND_LOW)
    {
        g_lnb_native_selftest_status = 0xB2060002u;
        return;
    }

    (void)lnb_native_set_enable(0);
    g_lnb_native_selftest_i2c = (uint32_t)(lnb_get_last_i2c_msg() & 0xFFFF);
    g_lnb_native_selftest_status = 0xB2FF0001u;
}

static void LnbSelfTestThread(void const *argument)
{
    (void)argument;
    g_lnb_native_selftest_thread_marker = 0xB2AA0001u;

    chThdSleepMilliseconds(3500);
    g_lnb_native_selftest_thread_marker = 0xB2AA0002u;

    RunLnbNativeSelfTest();
    g_lnb_native_selftest_thread_marker = 0xB2AA00FFu;

    while (true)
    {
        osDelay(1000);
    }
}

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
    if (osThreadCreate(osThread(FramSelfTestThread), NULL) != NULL)
    {
        g_fram_native_selftest_thread_marker = 0xF4AA0011u;
    }
    else
    {
        g_fram_native_selftest_thread_marker = 0xF4AA00EEu;
    }

    if (osThreadCreate(osThread(LnbSelfTestThread), NULL) != NULL)
    {
        g_lnb_native_selftest_thread_marker = 0xB2AA0011u;
    }
    else
    {
        g_lnb_native_selftest_thread_marker = 0xB2AA00EEu;
    }

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
