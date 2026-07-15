//
// Copyright (c) .NET Foundation and Contributors
// Portions Copyright (c) Microsoft Corporation.  All rights reserved.
// See LICENSE file in the project root for full license information.
//

#include "target_common.h"
#include "target_board.h"
#include <nanoHAL_v2.h>
#include <platform_target_capabilities.h>

// Board-specific system config for CUBLEY_F407_0_5.
// USART3 (PD8/PD9) is the sole wire-protocol transport (COM3, 921600 baud).
// DebugTextPort is disabled so hal_printf() output cannot interleave with
// wire protocol packets on the same UART.
HAL_SYSTEM_CONFIG HalSystemConfig = {
    {true},  // HAL_DRIVER_CONFIG_HEADER Header

    3,       // DebuggerPort  -> COM3 (USART3 = nanoFramework wire protocol)
    0,       // DebugTextPort -> disabled
    921600,  // Wire protocol bitrate
    0,       // STDIO channel disabled

    {RAM1_MEMORY_StartAddress, RAM1_MEMORY_Size},
    {FLASH1_MEMORY_StartAddress, FLASH1_MEMORY_Size}};

HAL_TARGET_CONFIGURATION g_TargetConfiguration;

// Cubley v0.5 supports JTAG/SWD firmware updates only (no DFU bootrom pin).
inline GET_TARGET_CAPABILITIES(TargetCapabilities_JtagUpdate);

