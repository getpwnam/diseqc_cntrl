#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>

HRESULT Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_BringupStatus_NativeGetLastNativeError___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeInit___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetEnable___STATIC__I4__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeReadStatus___STATIC__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetVoltage___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetPolarization___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetTone___STATIC__I4__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeSetBand___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetVoltage___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetTone___STATIC__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetPolarization___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_LNBH26_NativeGetBand___STATIC__I4(CLR_RT_StackFrame& stack);

volatile uint32_t g_cubley_diag_current_status;
volatile uint32_t g_cubley_diag_last_error = 0;
volatile uint32_t g_cubley_diag_boot_probe_status = 0;
volatile uint32_t g_cubley_diag_clr_status = 0;

static const CLR_RT_MethodHandler method_lookup[] =
{
    Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4,          // [0] BringupStatus.NativeSet
    Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4,                 // [1] BringupStatus.NativeGet
    Library_cubley_interop_BringupStatus_NativeGetLastNativeError___STATIC__U4,  // [2] BringupStatus.NativeGetLastNativeError
    Library_cubley_interop_LNBH26_NativeInit___STATIC__I4,                       // [3] LNBH26.NativeInit
    Library_cubley_interop_LNBH26_NativeSetEnable___STATIC__I4__BOOLEAN,         // [4] LNBH26.NativeSetEnable
    Library_cubley_interop_LNBH26_NativeReadStatus___STATIC__I4__BYREF_I4,       // [5] LNBH26.NativeReadStatus
    Library_cubley_interop_LNBH26_NativeSetVoltage___STATIC__I4__I4,             // [6] LNBH26.NativeSetVoltage
    Library_cubley_interop_LNBH26_NativeSetPolarization___STATIC__I4__I4,        // [7] LNBH26.NativeSetPolarization
    Library_cubley_interop_LNBH26_NativeSetTone___STATIC__I4__BOOLEAN,           // [8] LNBH26.NativeSetTone
    Library_cubley_interop_LNBH26_NativeSetBand___STATIC__I4__I4,                // [9] LNBH26.NativeSetBand
    Library_cubley_interop_LNBH26_NativeGetVoltage___STATIC__I4,                 // [10] LNBH26.NativeGetVoltage
    Library_cubley_interop_LNBH26_NativeGetTone___STATIC__BOOLEAN,               // [11] LNBH26.NativeGetTone
    Library_cubley_interop_LNBH26_NativeGetPolarization___STATIC__I4,            // [12] LNBH26.NativeGetPolarization
    Library_cubley_interop_LNBH26_NativeGetBand___STATIC__I4,                    // [13] LNBH26.NativeGetBand
};

extern const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_SmokeLnbh26_Interop =
{
    "Cubley.Interop",
    0x69D026C2,
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
