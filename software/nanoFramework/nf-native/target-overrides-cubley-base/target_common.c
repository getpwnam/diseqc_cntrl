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

// Wire protocol debug channel is COM3 (USART3 on PB10/PB11).
HAL_SYSTEM_CONFIG HalSystemConfig = {
    {true},

    ConvertCOM_DebugHandle(3),
    0,
    115200,
    0,

    {RAM1_MEMORY_StartAddress, RAM1_MEMORY_Size},
    {FLASH1_MEMORY_StartAddress, FLASH1_MEMORY_Size}};

HAL_TARGET_CONFIGURATION g_TargetConfiguration;

inline GET_TARGET_CAPABILITIES(TargetCapabilities_JtagUpdate |
                               TargetCapabilities_DfuUpdate);
