// Minimal nanoCLR entry point for M0DMF_CUBLEY_F407 (serial wire protocol, no USB)

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

#if defined(CUBLEY_W5500_EARLY_INIT) && (CUBLEY_W5500_EARLY_INIT == TRUE)
extern int cubley_w5500_early_init(void);
extern volatile uint32_t g_w5500_bringup_status;
#endif

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

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_W5500_EARLY_INIT) && (CUBLEY_W5500_EARLY_INIT == TRUE))
static void PreOsDelayMs(int ms)
{
    for (int t = 0; t < ms; t++)
    {
        for (volatile int i = 0; i < 7000; i++)
        {
            __asm__ volatile ("nop");
        }
    }
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
    PreOsDelayMs(100);
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

#if defined(CUBLEY_W5500_EARLY_INIT) && (CUBLEY_W5500_EARLY_INIT == TRUE)
    // Visible heartbeat pulse and early W5500 reset release before CLR startup.
    palSetLineMode(PAL_LINE(GPIOA, 2U), PAL_MODE_OUTPUT_PUSHPULL);
    palSetLine(PAL_LINE(GPIOA, 2U));
    PreOsDelayMs(100);
    palClearLine(PAL_LINE(GPIOA, 2U));

    int earlyInitStatus = cubley_w5500_early_init();

    if (earlyInitStatus == 0)
    {
        // Two short pulses = early W5500 init success.
        palSetLine(PAL_LINE(GPIOA, 2U));
        PreOsDelayMs(80);
        palClearLine(PAL_LINE(GPIOA, 2U));
        PreOsDelayMs(80);
        palSetLine(PAL_LINE(GPIOA, 2U));
        PreOsDelayMs(80);
        palClearLine(PAL_LINE(GPIOA, 2U));

        g_w5500_bringup_status = ((uint32_t)0xD5 << 24) | ((uint32_t)0x90 << 16) | ((uint32_t)1 << 8);
    }
    else
    {
        // Four short pulses = early W5500 init failure.
        for (int i = 0; i < 4; i++)
        {
            palSetLine(PAL_LINE(GPIOA, 2U));
            PreOsDelayMs(80);
            palClearLine(PAL_LINE(GPIOA, 2U));
            PreOsDelayMs(80);
        }

        // Follow-up diagnostic code: pulse low nibble of status (1..15).
        int diagPulses = earlyInitStatus & 0x0F;
        if (diagPulses <= 0)
        {
            diagPulses = 1;
        }

        PreOsDelayMs(250);
        for (int i = 0; i < diagPulses; i++)
        {
            palSetLine(PAL_LINE(GPIOA, 2U));
            PreOsDelayMs(60);
            palClearLine(PAL_LINE(GPIOA, 2U));
            PreOsDelayMs(120);
        }

        g_w5500_bringup_status = ((uint32_t)0xD5 << 24) | ((uint32_t)0x90 << 16) | ((uint32_t)14 << 8) | ((uint32_t)earlyInitStatus & 0xFFU);
    }
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