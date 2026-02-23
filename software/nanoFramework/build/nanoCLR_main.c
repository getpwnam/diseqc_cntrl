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

osThreadDef(ReceiverThread, osPriorityHigh, 2048, "ReceiverThread");
osThreadDef(CLRStartupThread, osPriorityNormal, 4096, "CLRStartupThread");

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

    sdStart(&SERIAL_DRIVER, NULL);

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
