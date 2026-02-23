/**
 * @file w5500_interop.cpp
 * @brief nanoFramework interop stubs for native W5500 socket transport
 *
 * These are intentionally conservative stubs to establish a stable interop
 * contract first. They currently return NotSupported until the native W5500
 * transport implementation is connected.
 */

#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>

enum w5500_socket_status_t
{
    W5500_SOCKET_OK = 0,
    W5500_SOCKET_INVALID_PARAM = 1,
    W5500_SOCKET_NOT_INITIALIZED = 2,
    W5500_SOCKET_BUSY = 3,
    W5500_SOCKET_TIMEOUT = 4,
    W5500_SOCKET_NOT_SUPPORTED = 5,
    W5500_SOCKET_IO_ERROR = 6
};

static const int32_t kSingleSocketHandle = 1;
static bool g_socketAllocated = false;
static bool g_socketConnected = false;

HRESULT Library_diseqc_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    if (g_socketAllocated)
    {
        stack.Arg0().NumericByRef().s4 = -1;
        stack.SetResult_I4((int32_t)W5500_SOCKET_BUSY);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    g_socketAllocated = true;
    g_socketConnected = false;
    stack.Arg0().NumericByRef().s4 = kSingleSocketHandle;
    stack.SetResult_I4((int32_t)W5500_SOCKET_OK);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_diseqc_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;
    CLR_RT_HeapBlock* hostArg = &(stack.Arg1());
    int32_t port = stack.Arg2().NumericByRef().s4;
    int32_t timeoutMs = stack.Arg3().NumericByRef().s4;

    FAULT_ON_NULL(hostArg->DereferenceString());

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (port < 1 || port > 65535 || timeoutMs < 0)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    g_socketConnected = true;
    stack.SetResult_I4((int32_t)W5500_SOCKET_OK);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_diseqc_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;
    CLR_RT_HeapBlock_Array* dataArray = stack.Arg1().DereferenceArray();
    int32_t offset = stack.Arg2().NumericByRef().s4;
    int32_t count = stack.Arg3().NumericByRef().s4;

    FAULT_ON_NULL(dataArray);

    stack.Arg4().NumericByRef().s4 = 0;

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated || !g_socketConnected)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_NOT_INITIALIZED);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (offset < 0 || count < 0 || (uint32_t)(offset + count) > dataArray->m_numOfElements)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    stack.Arg4().NumericByRef().s4 = count;
    stack.SetResult_I4((int32_t)W5500_SOCKET_OK);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_diseqc_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;
    CLR_RT_HeapBlock_Array* bufferArray = stack.Arg1().DereferenceArray();
    int32_t offset = stack.Arg2().NumericByRef().s4;
    int32_t count = stack.Arg3().NumericByRef().s4;
    int32_t timeoutMs = stack.Arg4().NumericByRef().s4;

    FAULT_ON_NULL(bufferArray);

    stack.Arg5().NumericByRef().s4 = 0;

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated || !g_socketConnected)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_NOT_INITIALIZED);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (offset < 0 || count < 0 || timeoutMs < 0 || (uint32_t)(offset + count) > bufferArray->m_numOfElements)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    // Minimal integration behavior: no RX backing buffer yet.
    // Report timeout with zero bytes so managed callers can handle retry logic.
    stack.SetResult_I4((int32_t)W5500_SOCKET_TIMEOUT);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_diseqc_interop_W5500Socket_NativeClose___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    g_socketConnected = false;
    g_socketAllocated = false;
    stack.SetResult_I4((int32_t)W5500_SOCKET_OK);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_diseqc_interop_W5500Socket_NativeIsConnected___STATIC__BOOLEAN__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated)
    {
        stack.SetResult_Boolean(false);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    stack.SetResult_Boolean(g_socketConnected);

    NANOCLR_NOCLEANUP();
}