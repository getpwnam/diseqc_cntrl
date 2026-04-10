#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>

HRESULT Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeConfigureNetwork___STATIC__I4__STRING__STRING__STRING__STRING(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeClose___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_cubley_interop_W5500Socket_NativeIsConnected___STATIC__BOOLEAN__I4(CLR_RT_StackFrame& stack);

volatile uint32_t g_w5500_bringup_status = 0xD5010000;

static const CLR_RT_MethodHandler method_lookup[] =
{
    Library_cubley_interop_BringupStatus_NativeSet___STATIC__VOID__U4,       // [0] BringupStatus.NativeSet
    Library_cubley_interop_BringupStatus_NativeGet___STATIC__U4,             // [1] BringupStatus.NativeGet
    Library_cubley_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4,   // [2] W5500Socket.NativeOpen
    Library_cubley_interop_W5500Socket_NativeConfigureNetwork___STATIC__I4__STRING__STRING__STRING__STRING, // [3]
    Library_cubley_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4,                     // [4]
    Library_cubley_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4,          // [5]
    Library_cubley_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4,   // [6]
    Library_cubley_interop_W5500Socket_NativeClose___STATIC__I4__I4,                                       // [7]
    Library_cubley_interop_W5500Socket_NativeIsConnected___STATIC__BOOLEAN__I4,                            // [8]
};

extern const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_Cubley_Interop =
{
    "Cubley.Interop",
    0x7119BD66,  // nativeMethodsChecksum from Cubley.Interop.pe (computed by MetaDataProcessor)
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
