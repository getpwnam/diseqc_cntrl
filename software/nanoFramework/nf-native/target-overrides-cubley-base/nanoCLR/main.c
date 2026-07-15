#include <ch.h>
#include <hal.h>
#include <hal_nf_community.h>
#include <cmsis_os.h>

#include <serialcfg.h>
#include <CLR_Startup_Thread.h>
#include <WireProtocol_ReceiverThread.h>
#include <nanoCLR_Application.h>
#include <nanoHAL_v2.h>

static void BusyDelay(volatile uint32_t cycles)
{
    while (cycles-- > 0)
    {
        __asm("nop");
    }
}

static void PulseStatusLedBeforeManaged(void)
{
    RCC->AHB1ENR |= RCC_AHB1ENR_GPIOBEN;
    (void)RCC->AHB1ENR;

    palSetPadMode(GPIOB, 0, PAL_MODE_OUTPUT_PUSHPULL);
    palClearPad(GPIOB, 0);

    // Early boot visibility: blink PB0 for ~5 seconds total.
    // Calibrated for this build/toolchain.
    for (int i = 0; i < 10; i++)
    {
        palSetPad(GPIOB, 0);
        BusyDelay(14000000U);
        palClearPad(GPIOB, 0);
        BusyDelay(14000000U);
    }
}

osThreadDef(ReceiverThread, osPriorityHigh, 4096, "ReceiverThread");
osThreadDef(CLRStartupThread, osPriorityNormal, 4096, "CLRStartupThread");

int main(void)
{
    halInit();
    PulseStatusLedBeforeManaged();
    InitBootClipboard();
    osKernelInitialize();

    RCC->APB1ENR |= RCC_APB1ENR_USART3EN;
    (void)RCC->APB1ENR;

    static const SerialConfig usart3_cfg = {
        921600,
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
