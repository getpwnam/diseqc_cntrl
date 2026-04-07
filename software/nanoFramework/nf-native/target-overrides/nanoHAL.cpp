//
// nanoHAL.cpp - STM32F407 DiSEqC Controller board-specific runtime initialization
// Patches debugger-halt flag to allow managed startup without debugger protocol blocking.
//

#include <nanoCLR_Runtime.h>
#include <nanoHAL.h>
#include <targetPAL.h>

// External CLR execution engine declarations
extern "C" {
    struct CLR_RT_ExecutionEngineState {
        uint32_t flags;
        uint32_t pad[2];
    };
    
    extern CLR_RT_ExecutionEngineState g_CLR_RT_ExecutionEngine;
    
    // Upstream nanoHAL Initialize hook
    extern void nanoHAL_Initialize_Upstream(void);
}

// Debugger execution halt flag (from CLR source)
#define CLR_EXECUTION_HALTED 0x800

//
// nanoHAL_Initialize - Custom board initialization with debugger halt clear.
//
void nanoHAL_Initialize(void) {
    // Call upstream nanoHAL initialization (clock setup, PAL, etc.)
    nanoHAL_Initialize_Upstream();
    
    // Clear the debugger-halt flag to allow normal thread execution.
    // When CONFIG_NF_FEATURE_DEBUGGER=y, ClrStartup sets this flag and enters
    // a wait loop unless debugger is attached. Since we run without debugger,
    // this prevents managed app startup. Clearing the flag allows progression
    // past ResolveAll -> PrepareForExecution -> NewThread.
    g_CLR_RT_ExecutionEngine.flags &= ~CLR_EXECUTION_HALTED;
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
