#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

# Ultra-minimal Cubley common sources.
#   * usbcfg.c / usbcfg.h were removed with USB CDC (HAL_USE_SERIAL_USB=FALSE).
#   * Device_BlockStorage(-DEBUG).c is required by target_BlockStorage.c and
#     provides the STM32 internal flash block map.
list(APPEND COMMON_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/Device_BlockStorage$<$<OR:$<CONFIG:Debug>,$<CONFIG:RelWithDebInfo>>:-DEBUG>.c")
list(APPEND COMMON_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/targetHAL_ConfigurationManager.cpp")

# make var global
set(COMMON_PROJECT_SOURCES ${COMMON_PROJECT_SOURCES} CACHE INTERNAL "make global")
