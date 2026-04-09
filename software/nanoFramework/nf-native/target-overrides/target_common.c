//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

#include "target_common.h"
#include "target_board.h"
#include <nanoHAL_v2.h>
#include <platform_target_capabilities.h>

#ifndef ConvertCOM_DebugHandle
#define ConvertCOM_DebugHandle(port) (port)
#endif

// Board-specific system config.
// Wire protocol debug channel is mapped to COM3 (USART3 -> PB10/PB11).
HAL_SYSTEM_CONFIG HalSystemConfig = {
    {true}, // HAL_DRIVER_CONFIG_HEADER Header

    ConvertCOM_DebugHandle(3), // COM3 (USART3 on PB10/PB11)
    0,      // Messaging channel disabled
    115200, // Wire protocol serial bitrate
    0,      // STDIO channel disabled

    {RAM1_MEMORY_StartAddress, RAM1_MEMORY_Size},
    {FLASH1_MEMORY_StartAddress, FLASH1_MEMORY_Size}};

HAL_TARGET_CONFIGURATION g_TargetConfiguration;

// This target can use both JTAG and DFU for updates.
inline GET_TARGET_CAPABILITIES(TargetCapabilities_JtagUpdate |
                               TargetCapabilities_DfuUpdate);
