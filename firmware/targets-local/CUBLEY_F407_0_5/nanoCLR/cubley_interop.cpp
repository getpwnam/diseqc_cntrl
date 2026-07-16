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

HRESULT Library_cubley_interop_DiagMailbox_NativeSet___STATIC__VOID__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_DiagMailbox_NativeGet___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_DiagMailbox_NativeGetLastNativeError___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeInit___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetEnable___STATIC__I4__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeReadStatus___STATIC__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeReadStatusPair___STATIC__I4__BYREF_I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetPolarizationForChannel___STATIC__I4__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetBandForChannel___STATIC__I4__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetLowPowerForChannel___STATIC__I4__I4__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetDiseqcInputModeForChannel___STATIC__I4__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetPolarizationForChannel___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetBandForChannel___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetLastError___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetLastErrorDetail___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_UsbCdcConsole_NativeIsEnabled___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_UsbCdcConsole_NativeReadByte___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_UsbCdcConsole_NativeWrite___STATIC__I4__STRING(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26Registers_NativeReadRegister___STATIC__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_Fram24C128_NativeInit___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_Fram24C128_NativeWrite___STATIC__I4__I4__SZARRAY_U1__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_Fram24C128_NativeRead___STATIC__I4__I4__SZARRAY_U1__I4__I4(CLR_RT_StackFrame& stack);

// Diagnostics words used by managed bring-up code.
//
// Semantics:
// - g_cubley_diag_current_status:
//     transient status word written by managed/native flow markers.
//     exposed through DiagMailbox.NativeSet/NativeGet.
// - g_cubley_diag_last_error:
//     latest native subsystem error/result detail word (FRAM/LNB, etc.).
//     exposed through DiagMailbox.NativeGetLastNativeError.
//
// Storage note:
// - These globals are zero-initialized static storage, so they reside in .bss.
// - 'volatile' prevents compiler elision/reordering for external observers
volatile uint32_t g_cubley_diag_current_status;
volatile uint32_t g_cubley_diag_last_error;

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
static bool CubleyUsbCdcReady()
{
    if (SDU1.state != SDU_READY || SDU1.config == nullptr || SDU1.config->usbp == nullptr)
    {
        return false;
    }

    return (SDU1.config->usbp->state == USB_ACTIVE);
}
#endif

static const CLR_RT_MethodHandler method_lookup[] =
{
    Library_cubley_interop_DiagMailbox_NativeSet___STATIC__VOID__U4,       // [0] DiagMailbox.NativeSet
    Library_cubley_interop_DiagMailbox_NativeGet___STATIC__U4,             // [1] DiagMailbox.NativeGet
    Library_cubley_interop_DiagMailbox_NativeGetLastNativeError___STATIC__U4, // [2] DiagMailbox.NativeGetLastNativeError
    Library_cubley_interop_Fram24C128_NativeInit___STATIC__I4,                                              // [3] Fram24C128.NativeInit
    Library_cubley_interop_Fram24C128_NativeWrite___STATIC__I4__I4__SZARRAY_U1__I4__I4,                    // [4] Fram24C128.NativeWrite
    Library_cubley_interop_Fram24C128_NativeRead___STATIC__I4__I4__SZARRAY_U1__I4__I4,                     // [5] Fram24C128.NativeRead
    Library_cubley_interop_LNBH26_NativeInit___STATIC__I4,                                                  // [6] LNBH26.NativeInit
    Library_cubley_interop_LNBH26_NativeSetEnable___STATIC__I4__BOOLEAN,                                    // [7] LNBH26.NativeSetEnable
    Library_cubley_interop_LNBH26_NativeReadStatus___STATIC__I4__BYREF_I4,                                  // [8] LNBH26.NativeReadStatus
    Library_cubley_interop_LNBH26_NativeReadStatusPair___STATIC__I4__BYREF_I4__BYREF_I4,                   // [9] LNBH26.NativeReadStatusPair
    Library_cubley_interop_LNBH26_NativeSetPolarizationForChannel___STATIC__I4__I4__I4,                    // [10] LNBH26.NativeSetPolarizationForChannel
    Library_cubley_interop_LNBH26_NativeSetBandForChannel___STATIC__I4__I4__I4,                            // [11] LNBH26.NativeSetBandForChannel
    Library_cubley_interop_LNBH26_NativeSetLowPowerForChannel___STATIC__I4__I4__BOOLEAN,                   // [12] LNBH26.NativeSetLowPowerForChannel
    Library_cubley_interop_LNBH26_NativeSetDiseqcInputModeForChannel___STATIC__I4__I4__I4,                 // [13] LNBH26.NativeSetDiseqcInputModeForChannel
    Library_cubley_interop_LNBH26_NativeGetPolarizationForChannel___STATIC__I4__I4,                        // [14] LNBH26.NativeGetPolarizationForChannel
    Library_cubley_interop_LNBH26_NativeGetBandForChannel___STATIC__I4__I4,                                // [15] LNBH26.NativeGetBandForChannel
    Library_cubley_interop_LNBH26_NativeGetLastError___STATIC__I4,                                          // [16] LNBH26.NativeGetLastError
    Library_cubley_interop_LNBH26_NativeGetLastErrorDetail___STATIC__I4,                                    // [17] LNBH26.NativeGetLastErrorDetail
    Library_cubley_interop_LNBH26Registers_NativeReadRegister___STATIC__I4__I4__BYREF_I4,                  // [18] LNBH26Registers.NativeReadRegister
    Library_cubley_interop_UsbCdcConsole_NativeIsEnabled___STATIC__I4,                                      // [19] UsbCdcConsole.NativeIsEnabled
    Library_cubley_interop_UsbCdcConsole_NativeReadByte___STATIC__I4__I4,                                   // [20] UsbCdcConsole.NativeReadByte
    Library_cubley_interop_UsbCdcConsole_NativeWrite___STATIC__I4__STRING,                                  // [21] UsbCdcConsole.NativeWrite
};

extern const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_CubleyNative =
{
    "CubleyNative",
    0x88B2008D,  // nativeMethodsChecksum from CubleyNative.pe (computed by MetaDataProcessor)
    method_lookup,
    { 1, 0, 0, 0 }
};

HRESULT Library_cubley_interop_DiagMailbox_NativeSet___STATIC__VOID__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_cubley_diag_current_status = stack.Arg0().NumericByRef().u4;

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_DiagMailbox_NativeGet___STATIC__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_U4(g_cubley_diag_current_status);

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_DiagMailbox_NativeGetLastNativeError___STATIC__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_U4(g_cubley_diag_last_error);

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_UsbCdcConsole_NativeIsEnabled___STATIC__I4(CLR_RT_StackFrame& stack)
{
    // Breadcrumbs for bring-up triage:
    // 0xCD010001 -> entered NativeIsEnabled
    // 0xCD010002 -> readiness evaluated
    // 0xCD010003 -> SetResult_I4 completed
    g_cubley_diag_current_status = 0xCD010001u;

    // Use SetResult_I4 (not SetResult_Boolean) to avoid the nanoFramework CLR
    // assert: SetResult_Boolean calls PushValue() which checks eval-stack
    // bounds, but extern InternalCall methods have max-stack=0 in the PE.
    // SetResult_I4 writes to slot 0 directly and is safe here.
#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
    bool ready = CubleyUsbCdcReady();
    g_cubley_diag_current_status = 0xCD010002u | (ready ? 1u : 0u);
    stack.SetResult_I4(ready ? 1 : 0);
#else
    g_cubley_diag_current_status = 0xCD010002u;
    stack.SetResult_I4(0);
#endif

    g_cubley_diag_current_status = 0xCD010003u;

    return S_OK;
}

HRESULT Library_cubley_interop_UsbCdcConsole_NativeReadByte___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    // Breadcrumbs for dispatch tracing:
    // 0xCD020001 -> entered NativeReadByte
    // 0xCD020002 -> about to call chnGetTimeout
    // 0xCD020003 -> chnGetTimeout returned
    g_cubley_diag_current_status = 0xCD020001u;

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
    if (!CubleyUsbCdcReady())
    {
        stack.SetResult_I4(-1);
        NANOCLR_NOCLEANUP_NOLABEL();
    }

    int32_t timeoutMs = stack.Arg0().NumericByRef().s4;
    if (timeoutMs < 0)
    {
        timeoutMs = 0;
    }

    systime_t timeout = (timeoutMs == 0) ? TIME_IMMEDIATE : TIME_MS2I((uint32_t)timeoutMs);
    g_cubley_diag_current_status = 0xCD020002u;
    msg_t result = chnGetTimeout((BaseChannel *)&SDU1, timeout);
    g_cubley_diag_current_status = 0xCD020003u;

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

    // Breadcrumbs for dispatch tracing:
    // 0xCD030001 -> entered NativeWrite
    g_cubley_diag_current_status = 0xCD030001u;

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
    if (!CubleyUsbCdcReady())
    {
        stack.SetResult_I4(-1);
        NANOCLR_NOCLEANUP_NOLABEL();
    }

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

    const uint8_t* cursor = (const uint8_t*)buffer;
    size_t remaining = length;
    uint8_t noProgressAttempts = 0;

    while (remaining > 0 && noProgressAttempts < 8)
    {
        size_t step = chnWriteTimeout((BaseChannel *)&SDU1, cursor, remaining, TIME_MS2I(25));

        if (step == 0)
        {
            noProgressAttempts++;
            continue;
        }

        cursor += step;
        remaining -= step;
        noProgressAttempts = 0;
    }

    written = length - remaining;
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
