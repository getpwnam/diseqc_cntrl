#include <ch.h>
#include <hal.h>
#include <cmsis_os.h>

#include <targetHAL.h>
#include <LaunchCLR.h>

int main(void)
{
    halInit();

    // Bring up the RTOS before any delay call; osDelay() prior to scheduler
    // start can stall the booter before it ever hands off to nanoCLR.
    chSysInit();
    chThdSleepMilliseconds(20);

    // Force CLR launch unconditionally. CheckValidCLRImage() rejects valid
    // binaries on this target, so we bypass validation and launch directly.
    LaunchCLR((uint32_t)&__nanoImage_end__);

    while (true)
    {
        osDelay(1000);
    }
}
