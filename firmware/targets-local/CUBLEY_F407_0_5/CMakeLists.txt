#
# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.
#

include(binutils.common)
include(binutils.ChibiOS)

nf_setup_target_build(
    HAS_NANOBOOTER

    BOOTER_LINKER_FILE 
        STM32F407xG_booter

    CLR_LINKER_FILE 
        STM32F407xG_CLR
    
    BOOTER_EXTRA_LINKMAP_PROPERTIES
        ",--library-path=${CMAKE_SOURCE_DIR}/targets/ChibiOS/_common,--defsym=__main_stack_size__=0x300,--defsym=__process_stack_size__=0x300,--defsym=__crt_heap_size__=0x10000"

    CLR_EXTRA_LINKMAP_PROPERTIES
        ",--library-path=${CMAKE_SOURCE_DIR}/targets/ChibiOS/_common,--defsym=__main_stack_size__=0x400,--defsym=__process_stack_size__=0x800,--defsym=__crt_heap_size__=0x10000"
)

# Keep wire protocol on UART even when USB CDC is enabled by compiling the
# shared receiver thread with USB serial gating disabled.
set_source_files_properties(
    ${CMAKE_SOURCE_DIR}/targets/ChibiOS/_common/WireProtocol_ReceiverThread.c
    PROPERTIES
        COMPILE_DEFINITIONS "HAL_USE_SERIAL_USB=FALSE"
)
