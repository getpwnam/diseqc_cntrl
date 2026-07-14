#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

list(APPEND NANOBOOTER_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/main.c")
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/../common/usbcfg.c")
    list(APPEND NANOBOOTER_PROJECT_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../common/usbcfg.c")
endif()
set(NANOBOOTER_PROJECT_SOURCES ${NANOBOOTER_PROJECT_SOURCES} CACHE INTERNAL "make global")
