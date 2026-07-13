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

volatile uint32_t g_cubley_diag_current_status;
volatile uint32_t g_cubley_diag_last_error = 0;
volatile uint32_t g_cubley_diag_boot_probe_status = 0;
volatile uint32_t g_cubley_diag_clr_status = 0;
extern volatile uint32_t g_w5500_diag_trace;

static HRESULT Library_smoke_w5500_interop_W5500Socket_NativeGetVersion_Traced___STATIC__U4(CLR_RT_StackFrame& stack)
{
    // Distinct breadcrumb for SmokeW5500 dispatch into the VERSIONR native call.
    g_cubley_diag_current_status = 0xD5B70031u;
    g_w5500_diag_trace = 0xE3AA5531u;
    return Library_cubley_interop_W5500Socket_NativeGetVersion___STATIC__U4(stack);
}

static const CLR_RT_MethodHandler method_lookup[] =
{
    Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4,       // [0] BringupStatus.NativeSet
    Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4,             // [1] BringupStatus.NativeGet
    Library_cubley_interop_BringupStatus_NativeGetLastNativeError___STATIC__U4, // [2] BringupStatus.NativeGetLastNativeError
    Library_cubley_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4,   // [3] W5500Socket.NativeOpen
    Library_cubley_interop_W5500Socket_NativeConfigureNetwork___STATIC__I4__STRING__STRING__STRING__STRING, // [4] W5500Socket.NativeConfigureNetwork
    Library_cubley_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4,                     // [5] W5500Socket.NativeConnect
    Library_cubley_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4,          // [6] W5500Socket.NativeSend
    Library_cubley_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4,   // [7] W5500Socket.NativeReceive
    Library_cubley_interop_W5500Socket_NativeClose___STATIC__I4__I4,                                       // [8] W5500Socket.NativeClose
    Library_cubley_interop_W5500Socket_NativeIsConnected___STATIC__BOOLEAN__I4,                            // [9] W5500Socket.NativeIsConnected
    Library_cubley_interop_W5500Socket_NativeGetPhyStatus___STATIC__U4,                                    // [10] W5500Socket.NativeGetPhyStatus
    Library_smoke_w5500_interop_W5500Socket_NativeGetVersion_Traced___STATIC__U4,                          // [11] W5500Socket.NativeGetVersion
    Library_cubley_interop_W5500Socket_NativeGetVersionPhyStatus___STATIC__U4,                             // [12] W5500Socket.NativeGetVersionPhyStatus
    Library_cubley_interop_W5500Socket_NativeSetPhyMode___STATIC__U4__I4,                                  // [13] W5500Socket.NativeSetPhyMode
};

extern const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_SmokeW5500_Interop =
{
    "Cubley.Interop",
    0xD2CF401C,
    method_lookup,
    { 1, 0, 0, 0 }
};

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