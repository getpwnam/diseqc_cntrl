#include <ch.h>
#include <hal.h>
#include <cmsis_os.h>

#include <targetHAL.h>
#include <LaunchCLR.h>

int main(void)
{
    halInit();

    osKernelInitialize();
    osDelay(20);

    if (CheckValidCLRImage((uint32_t)&__nanoImage_end__))
    {
        LaunchCLR((uint32_t)&__nanoImage_end__);
    }

    while (true)
    {
        osDelay(1000);
    }
}
