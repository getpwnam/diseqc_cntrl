#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>
#include <string.h>
#include "fram_native.h"

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
#include <hal.h>
#include <ch.h>
#include "../common/usbcfg.h"
#endif

HRESULT Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_BringupStatus_NativeGetLastNativeError___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_DiagnosticsMailbox_NativeTryLatchBootProbe___STATIC__BOOLEAN__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_DiagnosticsMailbox_NativeGetBootProbe___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeInit___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetEnable___STATIC__I4__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeReadStatus___STATIC__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetVoltage___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetTone___STATIC__I4__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetVoltage___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetTone___STATIC__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_StatusLed_NativeInit___STATIC__VOID(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_StatusLed_NativeSetHigh___STATIC__VOID(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_StatusLed_NativeSetLow___STATIC__VOID(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_StatusLed_NativePulse___STATIC__VOID__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_UsbCdcConsole_NativeIsEnabled___STATIC__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_UsbCdcConsole_NativeReadByte___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_UsbCdcConsole_NativeWrite___STATIC__I4__STRING(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26Registers_NativeReadRegister___STATIC__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_Fram24C128_NativeInit___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_Fram24C128_NativeWrite___STATIC__I4__I4__SZARRAY_U1__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_Fram24C128_NativeRead___STATIC__I4__I4__SZARRAY_U1__I4__I4(CLR_RT_StackFrame& stack);

// Diagnostics mailboxes. Keep the transient current status in .bss so the linker
// places it after g_CLR_InteropAssembliesNativeData in .data, which the CLR may
// overwrite by one slot on assembly load.
volatile uint32_t g_cubley_diag_current_status;
volatile uint32_t g_cubley_diag_last_error = 0;
volatile uint32_t g_cubley_diag_boot_probe_status = 0;
volatile uint32_t g_cubley_diag_clr_status = 0;

static const CLR_RT_MethodHandler method_lookup[] =
{
    Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4,       // [0] BringupStatus.NativeSet
    Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4,             // [1] BringupStatus.NativeGet
    Library_cubley_interop_BringupStatus_NativeGetLastNativeError___STATIC__U4, // [2] BringupStatus.NativeGetLastNativeError
    Library_cubley_interop_DiagnosticsMailbox_NativeTryLatchBootProbe___STATIC__BOOLEAN__U4, // [3] DiagnosticsMailbox.NativeTryLatchBootProbe
    Library_cubley_interop_DiagnosticsMailbox_NativeGetBootProbe___STATIC__U4, // [4] DiagnosticsMailbox.NativeGetBootProbe
    Library_cubley_interop_Fram24C128_NativeInit___STATIC__I4,                                              // [5] Fram24C128.NativeInit
    Library_cubley_interop_Fram24C128_NativeWrite___STATIC__I4__I4__SZARRAY_U1__I4__I4,                    // [6] Fram24C128.NativeWrite
    Library_cubley_interop_Fram24C128_NativeRead___STATIC__I4__I4__SZARRAY_U1__I4__I4,                     // [7] Fram24C128.NativeRead
    Library_cubley_interop_LNBH26_NativeInit___STATIC__I4,                                                  // [8] LNBH26.NativeInit
    Library_cubley_interop_LNBH26_NativeSetEnable___STATIC__I4__BOOLEAN,                                    // [9] LNBH26.NativeSetEnable
    Library_cubley_interop_LNBH26_NativeReadStatus___STATIC__I4__BYREF_I4,                                  // [10] LNBH26.NativeReadStatus
    Library_cubley_interop_LNBH26_NativeSetVoltage___STATIC__I4__I4,                                        // [11] LNBH26.NativeSetVoltage
    Library_cubley_interop_LNBH26_NativeSetTone___STATIC__I4__BOOLEAN,                                      // [12] LNBH26.NativeSetTone
    Library_cubley_interop_LNBH26_NativeGetVoltage___STATIC__I4,                                            // [13] LNBH26.NativeGetVoltage
    Library_cubley_interop_LNBH26_NativeGetTone___STATIC__BOOLEAN,                                          // [14] LNBH26.NativeGetTone
    Library_cubley_interop_LNBH26Registers_NativeReadRegister___STATIC__I4__I4__BYREF_I4,                  // [15] LNBH26Registers.NativeReadRegister
    Library_cubley_interop_StatusLed_NativeInit___STATIC__VOID,                                             // [16] StatusLed.NativeInit
    Library_cubley_interop_StatusLed_NativeSetHigh___STATIC__VOID,                                          // [17] StatusLed.NativeSetHigh
    Library_cubley_interop_StatusLed_NativeSetLow___STATIC__VOID,                                           // [18] StatusLed.NativeSetLow
    Library_cubley_interop_StatusLed_NativePulse___STATIC__VOID__I4__I4,                                    // [19] StatusLed.NativePulse
    Library_cubley_interop_UsbCdcConsole_NativeIsEnabled___STATIC__BOOLEAN,                                 // [20] UsbCdcConsole.NativeIsEnabled
    Library_cubley_interop_UsbCdcConsole_NativeReadByte___STATIC__I4__I4,                                   // [21] UsbCdcConsole.NativeReadByte
    Library_cubley_interop_UsbCdcConsole_NativeWrite___STATIC__I4__STRING,                                  // [22] UsbCdcConsole.NativeWrite
};

extern const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_CubleyNative =
{
    "CubleyNative",
    0x615A93C4,  // nativeMethodsChecksum from CubleyNative.pe (computed by MetaDataProcessor)
    method_lookup,
    { 1, 0, 0, 0 }
};

// StatusLed native implementations.
// Current PCB routes LED_STATUS to PB0. PA2 is used for RMII MDIO.

static inline void status_led_init_pins(void)
{
    palSetPadMode(GPIOB, 0, PAL_MODE_OUTPUT_PUSHPULL);
    palClearPad(GPIOB, 0);
}

static inline void status_led_set(bool on)
{
    if (on)
    {
        palSetPad(GPIOB, 0);
    }
    else
    {
        palClearPad(GPIOB, 0);
    }
}

HRESULT Library_cubley_interop_StatusLed_NativeInit___STATIC__VOID(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    (void)stack;

    // Beacon: entering NativeInit
    g_cubley_diag_current_status = 0xD5ED0001u;

    status_led_init_pins();
    
    // Beacon: initialization complete
    g_cubley_diag_current_status = 0xD5ED0003u;

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_StatusLed_NativeSetHigh___STATIC__VOID(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    (void)stack;
    
    // Beacon: SetHigh called
    g_cubley_diag_current_status = 0xD5EE0001u;
    
    status_led_set(true);
    
    // Beacon: SetHigh completed
    g_cubley_diag_current_status = 0xD5EE0002u;

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_StatusLed_NativeSetLow___STATIC__VOID(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    (void)stack;
    
    // Beacon: SetLow called
    g_cubley_diag_current_status = 0xD5EF0001u;
    
    status_led_set(false);
    
    // Beacon: SetLow completed
    g_cubley_diag_current_status = 0xD5EF0002u;

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_StatusLed_NativePulse___STATIC__VOID__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    int count = stack.Arg0().NumericByRef().s4;
    int pulseMs = stack.Arg1().NumericByRef().s4;

    status_led_init_pins();
    
    for (int i = 0; i < count; i++)
    {
        status_led_set(true);
        osalThreadSleepMilliseconds(pulseMs);
        
        status_led_set(false);
        osalThreadSleepMilliseconds(pulseMs);
    }

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_cubley_diag_current_status = stack.Arg0().NumericByRef().u4;

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_U4(g_cubley_diag_current_status);

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_BringupStatus_NativeGetLastNativeError___STATIC__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_U4(g_cubley_diag_last_error);

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_DiagnosticsMailbox_NativeTryLatchBootProbe___STATIC__BOOLEAN__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    const uint32_t statusWord = stack.Arg0().NumericByRef().u4;
    const bool latched = (g_cubley_diag_boot_probe_status == 0);

    if (latched)
    {
        g_cubley_diag_boot_probe_status = statusWord;
    }

    stack.SetResult_Boolean(latched);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_DiagnosticsMailbox_NativeGetBootProbe___STATIC__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_U4(g_cubley_diag_boot_probe_status);

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_UsbCdcConsole_NativeIsEnabled___STATIC__BOOLEAN(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
    stack.SetResult_Boolean(true);
#else
    stack.SetResult_Boolean(false);
#endif

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_UsbCdcConsole_NativeReadByte___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
    int32_t timeoutMs = stack.Arg0().NumericByRef().s4;
    if (timeoutMs < 0)
    {
        timeoutMs = 0;
    }

    systime_t timeout = (timeoutMs == 0) ? TIME_IMMEDIATE : TIME_MS2I((uint32_t)timeoutMs);
    msg_t result = chnGetTimeout((BaseChannel *)&SDU1, timeout);

    if (result < MSG_OK)
    {
        stack.SetResult_I4(-1);
    }
    else
    {
        stack.SetResult_I4((int32_t)((uint8_t)result));
    }
#else
    (void)stack;
    stack.SetResult_I4(-1);
#endif

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_UsbCdcConsole_NativeWrite___STATIC__I4__STRING(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
    CLR_RT_HeapBlock_String *text = stack.Arg0().DereferenceString();
    const char *buffer;
    size_t length = 0;
    size_t written = 0;

    if (text == nullptr)
    {
        stack.SetResult_I4(-1);
        NANOCLR_NOCLEANUP_NOLABEL();
    }

    buffer = text->StringText();

    while (buffer[length] != '\0')
    {
        length++;
    }

    if (length == 0)
    {
        stack.SetResult_I4(0);
        NANOCLR_NOCLEANUP_NOLABEL();
    }

    written = chnWriteTimeout((BaseChannel *)&SDU1, (const uint8_t *)buffer, length, TIME_MS2I(50));
    stack.SetResult_I4((int32_t)written);
#else
    (void)stack;
    stack.SetResult_I4(-1);
#endif

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_Fram24C128_NativeInit___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    fram_handle_t* hfram = fram_get_global_handle();
    fram_status_t status = fram_init(hfram, &I2CD1, 0x50);

    if (status != FRAM_OK)
    {
        const uint8_t rawDetail = (uint8_t)(fram_get_last_i2c_msg() & 0xFF);
        g_cubley_diag_last_error = ((uint32_t)0xE4 << 24) | ((uint32_t)0xD1 << 16) | ((uint32_t)status << 8) | rawDetail;
    }

    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_Fram24C128_NativeWrite___STATIC__I4__I4__SZARRAY_U1__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t address = stack.Arg0().NumericByRef().s4;
    CLR_RT_HeapBlock_Array* buffer = stack.Arg1().DereferenceArray();
    int32_t offset = stack.Arg2().NumericByRef().s4;
    int32_t count = stack.Arg3().NumericByRef().s4;

    if (buffer == NULL || address < 0 || address > 0xFFFF || offset < 0 || count <= 0 || (offset + count) > (int32_t)buffer->m_numOfElements)
    {
        stack.SetResult_I4((int32_t)FRAM_ERROR_INVALID_PARAM);
        NANOCLR_NOCLEANUP_NOLABEL();
    }

    fram_handle_t* hfram = fram_get_global_handle();
    uint8_t* src = (uint8_t*)buffer->GetFirstElement();
    src += offset;
    fram_status_t status = fram_write(hfram, (uint16_t)address, src, (uint16_t)count);

    if (status == FRAM_OK)
    {
        // 0xE4 D2 00 XX: FRAM write success, first payload byte observed by native wrapper.
        g_cubley_diag_last_error = ((uint32_t)0xE4 << 24) | ((uint32_t)0xD2 << 16) | ((uint32_t)0x00 << 8) | (uint32_t)src[0];
    }
    else
    {
        const uint8_t rawDetail = (uint8_t)(fram_get_last_i2c_msg() & 0xFF);
        g_cubley_diag_last_error = ((uint32_t)0xE4 << 24) | ((uint32_t)0xD2 << 16) | ((uint32_t)status << 8) | rawDetail;
    }

    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_Fram24C128_NativeRead___STATIC__I4__I4__SZARRAY_U1__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t address = stack.Arg0().NumericByRef().s4;
    CLR_RT_HeapBlock_Array* buffer = stack.Arg1().DereferenceArray();
    int32_t offset = stack.Arg2().NumericByRef().s4;
    int32_t count = stack.Arg3().NumericByRef().s4;

    if (buffer == NULL || address < 0 || address > 0xFFFF || offset < 0 || count <= 0 || (offset + count) > (int32_t)buffer->m_numOfElements)
    {
        stack.SetResult_I4((int32_t)FRAM_ERROR_INVALID_PARAM);
        NANOCLR_NOCLEANUP_NOLABEL();
    }

    fram_handle_t* hfram = fram_get_global_handle();
    uint8_t* dst = (uint8_t*)buffer->GetFirstElement();
    dst += offset;
    fram_status_t status = fram_read(hfram, (uint16_t)address, dst, (uint16_t)count);

    if (status == FRAM_OK)
    {
        // 0xE4 D3 00 XX: FRAM read success, first payload byte returned by native driver.
        g_cubley_diag_last_error = ((uint32_t)0xE4 << 24) | ((uint32_t)0xD3 << 16) | ((uint32_t)0x00 << 8) | (uint32_t)dst[0];
    }
    else
    {
        const uint8_t rawDetail = (uint8_t)(fram_get_last_i2c_msg() & 0xFF);
        g_cubley_diag_last_error = ((uint32_t)0xE4 << 24) | ((uint32_t)0xD3 << 16) | ((uint32_t)status << 8) | rawDetail;
    }

    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}
