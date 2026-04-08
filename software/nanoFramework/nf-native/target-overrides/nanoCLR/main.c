// Minimal nanoCLR entry point for M0DMF_DISEQC_F407 (serial wire protocol, no USB)

#include <ch.h>
#include <hal.h>
#include <hal_nf_community.h>
#include <cmsis_os.h>

#if (HAL_USE_SERIAL_USB == TRUE)
#include <usbcfg.h>
#else
#include <serialcfg.h>
#endif
#include <swo.h>
#include <CLR_Startup_Thread.h>
#include <WireProtocol_ReceiverThread.h>
#include <nanoCLR_Application.h>
#include <nanoHAL_v2.h>

#ifndef SWO_OUTPUT
#define SWO_OUTPUT 0
#endif

osThreadDef(ReceiverThread, osPriorityHigh, 2048, "ReceiverThread");
osThreadDef(CLRStartupThread, osPriorityNormal, 4096, "CLRStartupThread");

#if (HAL_USE_SERIAL_USB != TRUE)
static void ForceUsart3PinsOnPb10Pb11(void)
{
    RCC->AHB1ENR |= RCC_AHB1ENR_GPIOBEN;
    (void)RCC->AHB1ENR;

    GPIOB->MODER &= ~((3u << (10u * 2u)) | (3u << (11u * 2u)));
    GPIOB->MODER |= ((2u << (10u * 2u)) | (2u << (11u * 2u)));

    GPIOB->OTYPER &= ~((1u << 10u) | (1u << 11u));
    GPIOB->OSPEEDR |= ((3u << (10u * 2u)) | (3u << (11u * 2u)));
    GPIOB->PUPDR &= ~((3u << (10u * 2u)) | (3u << (11u * 2u)));

    GPIOB->AFRH &= ~((0xFu << ((10u - 8u) * 4u)) | (0xFu << ((11u - 8u) * 4u)));
    GPIOB->AFRH |= ((7u << ((10u - 8u) * 4u)) | (7u << ((11u - 8u) * 4u)));
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

#if (HAL_USE_SERIAL_USB == TRUE)
    sduObjectInit(&SERIAL_DRIVER);
    sduStart(&SERIAL_DRIVER, &serusbcfg);

#if defined(NF_USB_NO_VBUS_SENSE) && (NF_USB_NO_VBUS_SENSE == 1)
    USB_OTG_FS->GCCFG &= ~(USB_OTG_GCCFG_VBUSASEN | USB_OTG_GCCFG_VBUSBSEN);
    USB_OTG_FS->GCCFG |= USB_OTG_GCCFG_NOVBUSSENS;
#endif

    usbDisconnectBus(serusbcfg.usbp);
    chThdSleepMilliseconds(100);
    usbStart(serusbcfg.usbp, &usbcfg);
    usbConnectBus(serusbcfg.usbp);
#else
    static const SerialConfig usart3_cfg = {
        115200,
        0,
        USART_CR2_STOP1_BITS,
        0
    };

    palSetLineMode(PAL_LINE(GPIOB, 10U), PAL_MODE_ALTERNATE(7));
    palSetLineMode(PAL_LINE(GPIOB, 11U), PAL_MODE_ALTERNATE(7));
    ForceUsart3PinsOnPb10Pb11();

    sdStart(&SERIAL_DRIVER, &usart3_cfg);
#endif

    osThreadCreate(osThread(ReceiverThread), NULL);

    CLR_SETTINGS clrSettings;
    (void)memset(&clrSettings, 0, sizeof(CLR_SETTINGS));

    clrSettings.MaxContextSwitches = 50;
    clrSettings.WaitForDebugger = false;
    clrSettings.EnterDebuggerLoopAfterExit = true;

    osThreadCreate(osThread(CLRStartupThread), &clrSettings);

    osKernelStart();

    while (true)
    {
        osDelay(100);
    }
}