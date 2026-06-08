#include <ch.h>
#include <hal.h>
#include <hal_nf_community.h>
#include <cmsis_os.h>

#include <serialcfg.h>
#include <CLR_Startup_Thread.h>
#include <WireProtocol_ReceiverThread.h>
#include <nanoCLR_Application.h>
#include <nanoHAL_v2.h>

static void ForceUsart3PinsOnPb10Pb11(void)
{
    RCC->AHB1ENR |= RCC_AHB1ENR_GPIOBEN;
    (void)RCC->AHB1ENR;

    GPIOB->MODER &= ~((3u << (10u * 2u)) | (3u << (11u * 2u)));
    GPIOB->MODER |=  ((2u << (10u * 2u)) | (2u << (11u * 2u)));

    GPIOB->OTYPER &= ~((1u << 10u) | (1u << 11u));
    GPIOB->OSPEEDR |= ((3u << (10u * 2u)) | (3u << (11u * 2u)));
    GPIOB->PUPDR &= ~((3u << (10u * 2u)) | (3u << (11u * 2u)));

    GPIOB->AFRH &= ~((0xFu << ((10u - 8u) * 4u)) | (0xFu << ((11u - 8u) * 4u)));
    GPIOB->AFRH |=  ((7u  << ((10u - 8u) * 4u)) | (7u  << ((11u - 8u) * 4u)));
}

osThreadDef(ReceiverThread, osPriorityHigh, 4096, "ReceiverThread");
osThreadDef(CLRStartupThread, osPriorityNormal, 4096, "CLRStartupThread");

int main(void)
{
    halInit();
    InitBootClipboard();
    osKernelInitialize();

    RCC->APB1ENR |= RCC_APB1ENR_USART3EN;
    (void)RCC->APB1ENR;

    ForceUsart3PinsOnPb10Pb11();

    static const SerialConfig usart3_cfg = {
        115200,
        0,
        USART_CR2_STOP1_BITS,
        0
    };
    sdStart(&SD3, &usart3_cfg);

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
