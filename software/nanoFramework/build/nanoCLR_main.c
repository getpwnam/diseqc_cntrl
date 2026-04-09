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

extern volatile uint32_t g_w5500_bringup_status;

static inline void SetStartupMailbox(uint8_t stage, uint8_t result, uint8_t detail)
{
    g_w5500_bringup_status = ((uint32_t)0xD5 << 24) | ((uint32_t)stage << 16) | ((uint32_t)result << 8) | (uint32_t)detail;
}

static void CLRStartupThreadWrapper(void const* argument);

osThreadDef(ReceiverThread, osPriorityHigh, 2048, "ReceiverThread");
osThreadDef(CLRStartupThreadWrapper, osPriorityNormal, 4096, "CLRStartupThread");

#if (HAL_USE_SERIAL_USB != TRUE)
static void ForceUsart3PinsOnPb10Pb11(void)
{
    // Ensure GPIOB clock is enabled before pin mux writes.
    RCC->AHB1ENR |= RCC_AHB1ENR_GPIOBEN;
    (void)RCC->AHB1ENR;

    // PB10/PB11 -> alternate function mode.
    GPIOB->MODER &= ~((3u << (10u * 2u)) | (3u << (11u * 2u)));
    GPIOB->MODER |=  ((2u << (10u * 2u)) | (2u << (11u * 2u)));

    // Push-pull, high speed, floating.
    GPIOB->OTYPER &= ~((1u << 10u) | (1u << 11u));
    GPIOB->OSPEEDR |= ((3u << (10u * 2u)) | (3u << (11u * 2u)));
    GPIOB->PUPDR &= ~((3u << (10u * 2u)) | (3u << (11u * 2u)));

    // AF7 = USART3 on PB10/PB11.
    GPIOB->AFRH &= ~((0xFu << ((10u - 8u) * 4u)) | (0xFu << ((11u - 8u) * 4u)));
    GPIOB->AFRH |=  ((7u << ((10u - 8u) * 4u)) | (7u << ((11u - 8u) * 4u)));
}
#endif

static void CLRStartupThreadWrapper(void const* argument)
{
    SetStartupMailbox(0x35, 0, 0);
    CLRStartupThread(argument);
    SetStartupMailbox(0x36, 14, 1);

    while (true)
    {
        osDelay(1000);
    }
}

int main(void)
{
    SetStartupMailbox(0x30, 0, 0);

    halInit();

    InitBootClipboard();

#if (SWO_OUTPUT == TRUE)
    SwoInit();
#endif

    osKernelInitialize();
    SetStartupMailbox(0x31, 0, 0);

#if (HAL_NF_USE_STM32_CRC == TRUE)
    crcStart(NULL);
#endif

#if (HAL_USE_SERIAL_USB == TRUE)
    sduObjectInit(&SERIAL_DRIVER);
    sduStart(&SERIAL_DRIVER, &serusbcfg);

    usbDisconnectBus(serusbcfg.usbp);
    chThdSleepMilliseconds(100);
    usbStart(serusbcfg.usbp, &usbcfg);
    usbConnectBus(serusbcfg.usbp);
#else
    // Explicit 115200 8N1 config — do not pass NULL, ChibiOS default may differ.
    static const SerialConfig usart3_cfg = {
        115200,
        0,
        USART_CR2_STOP1_BITS,
        0
    };

    // Keep the PAL call path, then force direct register mux as a safety net.
    palSetLineMode(PAL_LINE(GPIOB, 10U), PAL_MODE_ALTERNATE(7));  // PB10 = USART3_TX
    palSetLineMode(PAL_LINE(GPIOB, 11U), PAL_MODE_ALTERNATE(7));  // PB11 = USART3_RX
    ForceUsart3PinsOnPb10Pb11();

    sdStart(&SERIAL_DRIVER, &usart3_cfg);
#endif

    osThreadId receiverThread = osThreadCreate(osThread(ReceiverThread), NULL);
    SetStartupMailbox(0x32, receiverThread != NULL ? 1 : 14, receiverThread != NULL ? 0 : 1);

    CLR_SETTINGS clrSettings;
    (void)memset(&clrSettings, 0, sizeof(CLR_SETTINGS));

    clrSettings.MaxContextSwitches = 50;
    clrSettings.WaitForDebugger = false;
    clrSettings.EnterDebuggerLoopAfterExit = true;

    osThreadId clrThread = osThreadCreate(osThread(CLRStartupThreadWrapper), &clrSettings);
    SetStartupMailbox(0x33, clrThread != NULL ? 1 : 14, clrThread != NULL ? 0 : 2);

    osStatus kernelStart = osKernelStart();
    SetStartupMailbox(0x34, kernelStart == osOK ? 1 : 14, (uint8_t)kernelStart);

    while (true)
    {
        osDelay(100);
    }
}
