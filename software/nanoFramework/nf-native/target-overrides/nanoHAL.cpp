//
// nanoHAL.cpp - STM32F407 DiSEqC Controller board-specific runtime initialization
//

#include <nanoCLR_Runtime.h>
#include <nanoHAL.h>
#include <targetPAL.h>

// Upstream nanoHAL Initialize hook
extern "C" void nanoHAL_Initialize_Upstream(void);

//
// nanoHAL_Initialize - Custom board initialization hook.
//
void nanoHAL_Initialize(void) {
    // Call upstream nanoHAL initialization (clock setup, PAL, etc.)
    nanoHAL_Initialize_Upstream();
}

// Empty stubs for platform-specific functionality that may be called by CLR
void nanoHAL_Uninitialize(bool isReboot) {
    // No platform-specific cleanup needed for minimal profile
    (void)isReboot;
}

void nanoHAL_Power_Sleep(CLR_UINT32 Flags) {
    // No power management for minimal profile
    (void)Flags;
    while (TRUE)
        ;
}

void nanoHAL_EnterBootloader(void) {
    // No bootloader reentry for minimal profile
    __BKPT(0);
    while (TRUE)
        ;
}

void nanoHAL_Reboot(void) {
    // System reset via NVIC
    NVIC_SystemReset();
}
