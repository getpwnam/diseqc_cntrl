#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>
#include <string.h>

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
#include <hal.h>
#include <ch.h>
#include <usbcfg.h>
#endif

HRESULT Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_BringupStatus_NativeGetLastNativeError___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_DiagnosticsMailbox_NativeTryLatchBootProbe___STATIC__BOOLEAN__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_DiagnosticsMailbox_NativeGetBootProbe___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeConfigureNetwork___STATIC__I4__STRING__STRING__STRING__STRING(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeClose___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeIsConnected___STATIC__BOOLEAN__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeGetPhyStatus___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeGetVersion___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeGetVersionPhyStatus___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeSetPhyMode___STATIC__U4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetVoltage___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetPolarization___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetTone___STATIC__I4__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetBand___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetVoltage___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetTone___STATIC__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetPolarization___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetBand___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_StatusLed_NativeInit___STATIC__VOID(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_StatusLed_NativeSetHigh___STATIC__VOID(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_StatusLed_NativeSetLow___STATIC__VOID(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_StatusLed_NativePulse___STATIC__VOID__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_UsbCdcConsole_NativeIsEnabled___STATIC__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_UsbCdcConsole_NativeReadByte___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_UsbCdcConsole_NativeWrite___STATIC__I4__STRING(CLR_RT_StackFrame& stack);

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
    Library_cubley_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4,   // [5] W5500Socket.NativeOpen
    Library_cubley_interop_W5500Socket_NativeConfigureNetwork___STATIC__I4__STRING__STRING__STRING__STRING, // [6]
    Library_cubley_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4,                     // [7]
    Library_cubley_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4,          // [8]
    Library_cubley_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4,   // [9]
    Library_cubley_interop_W5500Socket_NativeClose___STATIC__I4__I4,                                       // [10] W5500Socket.NativeClose
    Library_cubley_interop_W5500Socket_NativeIsConnected___STATIC__BOOLEAN__I4,                            // [11]
    Library_cubley_interop_W5500Socket_NativeGetPhyStatus___STATIC__U4,                                    // [12]
    Library_cubley_interop_W5500Socket_NativeGetVersion___STATIC__U4,                                      // [13] W5500Socket.NativeGetVersion
    Library_cubley_interop_W5500Socket_NativeGetVersionPhyStatus___STATIC__U4,                             // [14] W5500Socket.NativeGetVersionPhyStatus
    Library_cubley_interop_W5500Socket_NativeSetPhyMode___STATIC__U4__I4,                                  // [15] W5500Socket.NativeSetPhyMode
    Library_cubley_interop_LNBH26_NativeSetVoltage___STATIC__I4__I4,                                       // [16] LNBH26.NativeSetVoltage
    Library_cubley_interop_LNBH26_NativeSetPolarization___STATIC__I4__I4,                                  // [17] LNBH26.NativeSetPolarization
    Library_cubley_interop_LNBH26_NativeSetTone___STATIC__I4__BOOLEAN,                                     // [18] LNBH26.NativeSetTone
    Library_cubley_interop_LNBH26_NativeSetBand___STATIC__I4__I4,                                          // [19] LNBH26.NativeSetBand
    Library_cubley_interop_LNBH26_NativeGetVoltage___STATIC__I4,                                            // [20] LNBH26.NativeGetVoltage
    Library_cubley_interop_LNBH26_NativeGetTone___STATIC__BOOLEAN,                                          // [21] LNBH26.NativeGetTone
    Library_cubley_interop_LNBH26_NativeGetPolarization___STATIC__I4,                                       // [22] LNBH26.NativeGetPolarization
    Library_cubley_interop_LNBH26_NativeGetBand___STATIC__I4,                                               // [23] LNBH26.NativeGetBand
    Library_cubley_interop_StatusLed_NativeInit___STATIC__VOID,                                             // [24] StatusLed.NativeInit
    Library_cubley_interop_StatusLed_NativeSetHigh___STATIC__VOID,                                          // [25] StatusLed.NativeSetHigh
    Library_cubley_interop_StatusLed_NativeSetLow___STATIC__VOID,                                           // [26] StatusLed.NativeSetLow
    Library_cubley_interop_StatusLed_NativePulse___STATIC__VOID__I4__I4,                                    // [27] StatusLed.NativePulse
    Library_cubley_interop_UsbCdcConsole_NativeIsEnabled___STATIC__BOOLEAN,                                 // [28] UsbCdcConsole.NativeIsEnabled
    Library_cubley_interop_UsbCdcConsole_NativeReadByte___STATIC__I4__I4,                                   // [29] UsbCdcConsole.NativeReadByte
    Library_cubley_interop_UsbCdcConsole_NativeWrite___STATIC__I4__STRING,                                  // [30] UsbCdcConsole.NativeWrite
};

extern const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_Cubley_Interop =
{
    "Cubley.Interop",
    0xBAD958CD,  // nativeMethodsChecksum from Cubley.Interop.pe (computed by MetaDataProcessor)
    method_lookup,
    { 1, 0, 0, 0 }
};

// StatusLed native implementations (PA2 register-level control)
// PA2 is the LED_STATUS pin. GPIOA clock is enabled in boardInit().
// MODER bits [5:4] control PA2 mode, ODR bit 2 controls output level.

HRESULT Library_cubley_interop_StatusLed_NativeInit___STATIC__VOID(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    (void)stack;

    // Beacon: entering NativeInit
    g_cubley_diag_current_status = 0xD5ED0001u;

    // Initialize PA2 as output (done in boardInit() but we reinit here for safety)
    palSetPadMode(GPIOA, 2, PAL_MODE_OUTPUT_PUSHPULL);
    palClearPad(GPIOA, 2);  // Start with LED OFF
    
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
    
    // Set PA2 HIGH (LED ON)
    palSetPad(GPIOA, 2);
    
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
    
    // Set PA2 LOW (LED OFF)
    palClearPad(GPIOA, 2);
    
    // Beacon: SetLow completed
    g_cubley_diag_current_status = 0xD5EF0002u;

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_StatusLed_NativePulse___STATIC__VOID__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    int count = stack.Arg0().NumericByRef().s4;
    int pulseMs = stack.Arg1().NumericByRef().s4;
    
    for (int i = 0; i < count; i++)
    {
        // LED ON
        palSetPad(GPIOA, 2);
        osalThreadSleepMilliseconds(pulseMs);
        
        // LED OFF
        palClearPad(GPIOA, 2);
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

    FAULT_ON_NULL(text);

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
