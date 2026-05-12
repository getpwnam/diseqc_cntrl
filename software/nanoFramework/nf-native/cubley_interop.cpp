#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>

HRESULT Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_BringupStatus_NativeGetLastNativeError___STATIC__U4(CLR_RT_StackFrame& stack);
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

volatile uint32_t g_w5500_bringup_status = 0xD5010000;
volatile uint32_t g_w5500_last_native_error = 0;

static const CLR_RT_MethodHandler method_lookup[] =
{
    Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4,       // [0] BringupStatus.NativeSet
    Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4,             // [1] BringupStatus.NativeGet
    Library_cubley_interop_BringupStatus_NativeGetLastNativeError___STATIC__U4, // [2] BringupStatus.NativeGetLastNativeError
    Library_cubley_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4,   // [3] W5500Socket.NativeOpen
    Library_cubley_interop_W5500Socket_NativeConfigureNetwork___STATIC__I4__STRING__STRING__STRING__STRING, // [4]
    Library_cubley_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4,                     // [5]
    Library_cubley_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4,          // [6]
    Library_cubley_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4,   // [7]
    Library_cubley_interop_W5500Socket_NativeClose___STATIC__I4__I4,                                       // [8] W5500Socket.NativeClose
    Library_cubley_interop_W5500Socket_NativeIsConnected___STATIC__BOOLEAN__I4,                            // [9]
    Library_cubley_interop_W5500Socket_NativeGetPhyStatus___STATIC__U4,                                    // [10]
    Library_cubley_interop_W5500Socket_NativeGetVersion___STATIC__U4,                                      // [11] W5500Socket.NativeGetVersion
    Library_cubley_interop_W5500Socket_NativeGetVersionPhyStatus___STATIC__U4,                             // [12] W5500Socket.NativeGetVersionPhyStatus
    Library_cubley_interop_W5500Socket_NativeSetPhyMode___STATIC__U4__I4,                                  // [13] W5500Socket.NativeSetPhyMode
    Library_cubley_interop_LNBH26_NativeSetVoltage___STATIC__I4__I4,                                       // [14] LNBH26.NativeSetVoltage
    Library_cubley_interop_LNBH26_NativeSetPolarization___STATIC__I4__I4,                                  // [15] LNBH26.NativeSetPolarization
    Library_cubley_interop_LNBH26_NativeSetTone___STATIC__I4__BOOLEAN,                                     // [16] LNBH26.NativeSetTone
    Library_cubley_interop_LNBH26_NativeSetBand___STATIC__I4__I4,                                          // [17] LNBH26.NativeSetBand
    Library_cubley_interop_LNBH26_NativeGetVoltage___STATIC__I4,                                            // [18] LNBH26.NativeGetVoltage
    Library_cubley_interop_LNBH26_NativeGetTone___STATIC__BOOLEAN,                                          // [19] LNBH26.NativeGetTone
    Library_cubley_interop_LNBH26_NativeGetPolarization___STATIC__I4,                                       // [20] LNBH26.NativeGetPolarization
    Library_cubley_interop_LNBH26_NativeGetBand___STATIC__I4,                                               // [21] LNBH26.NativeGetBand
};

extern const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_Cubley_Interop =
{
    "Cubley.Interop",
    0xE741549D,  // nativeMethodsChecksum from Cubley.Interop.pe (computed by MetaDataProcessor)
    method_lookup,
    { 1, 0, 0, 0 }
};

HRESULT Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_w5500_bringup_status = stack.Arg0().NumericByRef().u4;

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_U4(g_w5500_bringup_status);

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_BringupStatus_NativeGetLastNativeError___STATIC__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_U4(g_w5500_last_native_error);

    NANOCLR_NOCLEANUP_NOLABEL();
}
