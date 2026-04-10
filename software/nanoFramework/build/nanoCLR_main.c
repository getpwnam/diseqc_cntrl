// Minimal nanoCLR entry point for M0DMF_DISEQC_F407 (serial wire protocol, no USB)

#include <ch.h>
#include <hal.h>
#include <hal_nf_community.h>
#include <cmsis_os.h>

#include <serialcfg.h>
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
