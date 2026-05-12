// DiSEqC interop glue for nanoFramework
#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>
#include "diseqc_native.h"

// Example: Expose DiSEqC transmit as InternalCall (expand as needed)
HRESULT Library_diseqc_interop_DiSEqC_NativeTransmit___STATIC__I4__SZARRAY_U1(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    CLR_RT_HeapBlock_Array* arr = stack.Arg0().DereferenceArray();
    if (!arr) NANOCLR_SET_AND_LEAVE(CLR_E_INVALID_PARAMETER);
    uint8_t* data = arr->GetFirstElement();
    uint8_t length = arr->m_numOfElements;
    diseqc_status_t status = diseqc_transmit(data, length);
    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}
