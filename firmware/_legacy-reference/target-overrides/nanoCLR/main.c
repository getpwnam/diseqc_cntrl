// nanoCLR entry point for CUBLEY_F407_0_5.
// Baseline startup with wire protocol on USART3 @ 115200.

#include <ch.h>
#include <hal.h>
#include <hal_nf_community.h>
#include <cmsis_os.h>

#include <serialcfg.h>
#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
#include <usbcfg.h>
#endif
#include <swo.h>
#include <CLR_Startup_Thread.h>
#include <WireProtocol_ReceiverThread.h>
#include <nanoCLR_Application.h>
#include <nanoHAL_v2.h>

#ifndef SWO_OUTPUT
#define SWO_OUTPUT 0
#endif

#if !defined(CUBLEY_WIRE_PROTOCOL_USB)
#define CUBLEY_WIRE_PROTOCOL_USB HAL_USE_SERIAL_USB
#endif

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
#define CUBLEY_USB_CDC_ACTIVE TRUE
#else
#define CUBLEY_USB_CDC_ACTIVE FALSE
#endif

// ChibiOS halt path expects this hook when linked with Cubley overlays.
void CubleySystemHaltHook(const char *reason)
{
    (void)reason;
}

osThreadDef(ReceiverThread, osPriorityHigh, 4096, "ReceiverThread");
osThreadDef(CLRStartupThread, osPriorityNormal, 4096, "CLRStartupThread");

#if (CUBLEY_USB_CDC_ACTIVE == TRUE)
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
#endif

int main(void)
{
    halInit();
    InitBootClipboard();

#if (SWO_OUTPUT == TRUE)
    SwoInit();
#endif

    osKernelInitialize();

#if (HAL_NF_USE_STM32_CRC == TRUE)
    crcStart(NULL);
#endif

#if (CUBLEY_WIRE_PROTOCOL_USB != TRUE)
    // Ensure USART3 clocks/pinmux are active for wire protocol.
    RCC->APB1ENR |= RCC_APB1ENR_USART3EN;
    (void)RCC->APB1ENR;

    RCC->AHB1ENR |= RCC_AHB1ENR_GPIODEN;
    (void)RCC->AHB1ENR;

    palSetLineMode(PAL_LINE(GPIOD, 8U), PAL_MODE_ALTERNATE(7));
    palSetLineMode(PAL_LINE(GPIOD, 9U), PAL_MODE_ALTERNATE(7));

    static const SerialConfig usart3_cfg = {
        115200, 0, USART_CR2_STOP1_BITS, 0};
    sdStart(&SD3, &usart3_cfg);
#endif

#if (CUBLEY_USB_CDC_ACTIVE == TRUE)
    chThdCreateStatic(
        waUsbCdcInitThread,
        sizeof(waUsbCdcInitThread),
        NORMALPRIO + 2,
        UsbCdcInitThread,
        NULL);
#endif

    osThreadCreate(osThread(ReceiverThread), NULL);

    CLR_SETTINGS clrSettings;
    (void)memset(&clrSettings, 0, sizeof(CLR_SETTINGS));
    clrSettings.MaxContextSwitches = 50;
    clrSettings.WaitForDebugger = false;
    clrSettings.EnterDebuggerLoopAfterExit = false;

    osThreadCreate(osThread(CLRStartupThread), &clrSettings);
    osKernelStart();

    while (true)
    {
        osDelay(100);
    }
}
