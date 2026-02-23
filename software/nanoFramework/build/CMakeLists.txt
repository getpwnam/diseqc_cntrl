#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

include(binutils.common)
include(binutils.ChibiOS)

include_directories(${CMAKE_CURRENT_SOURCE_DIR}/common)

nf_setup_target_build(
    HAS_NANOBOOTER

    BOOTER_LINKER_FILE
        STM32F407xG_booter

    CLR_LINKER_FILE
        STM32F407xG_CLR

    CLR_EXTRA_SOURCE_FILES
        ${CMAKE_CURRENT_SOURCE_DIR}/common/diseqc_native.cpp
        ${CMAKE_CURRENT_SOURCE_DIR}/common/lnb_control.cpp
        ${CMAKE_CURRENT_SOURCE_DIR}/nanoCLR/diseqc_interop.cpp
        ${CMAKE_CURRENT_SOURCE_DIR}/nanoCLR/lnb_interop.cpp

    BOOTER_EXTRA_LINKMAP_PROPERTIES
        ",--library-path=${CMAKE_SOURCE_DIR}/targets/ChibiOS/_common,--defsym=__main_stack_size__=0x400,--defsym=__process_stack_size__=0x400,--defsym=__crt_heap_size__=0x10000"

    CLR_EXTRA_LINKMAP_PROPERTIES
        ",--library-path=${CMAKE_SOURCE_DIR}/targets/ChibiOS/_common,--defsym=__main_stack_size__=0x400,--defsym=__process_stack_size__=0x800,--defsym=__crt_heap_size__=0x10000"
)
