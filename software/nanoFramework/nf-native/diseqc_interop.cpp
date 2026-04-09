/**
 * @file diseqc_interop.cpp
 * @brief nanoFramework interop layer for DiSEqC native driver
 * 
 * This file provides the CLR interop between C# and the native C++ DiSEqC driver.
 * Functions here are called from C# via [MethodImpl(MethodImplOptions.InternalCall)]
 */

#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>
#include "diseqc_native.h"

HRESULT Library_diseqc_interop_DiseqC_NativeGotoAngle___STATIC__I4__R4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_DiseqC_NativeTransmit___STATIC__I4__SZARRAY_U1(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_DiseqC_NativeHalt___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_DiseqC_NativeDriveEast___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_DiseqC_NativeDriveWest___STATIC__I4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_DiseqC_NativeStepEast___STATIC__I4__U1(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_DiseqC_NativeStepWest___STATIC__I4__U1(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_DiseqC_NativeIsBusy___STATIC__BOOLEAN(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_DiseqC_NativeGetCurrentAngle___STATIC__R4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_DiseqC_NativeSetBringupStatus___STATIC__VOID__U4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_DiseqC_NativeGetBringupStatus___STATIC__U4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_W5500Socket_NativeConfigureNetwork___STATIC__I4__STRING__STRING__STRING__STRING(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_W5500Socket_NativeClose___STATIC__I4__I4(CLR_RT_StackFrame& stack);
HRESULT Library_diseqc_interop_W5500Socket_NativeIsConnected___STATIC__BOOLEAN__I4(CLR_RT_StackFrame& stack);

/* Library information */
volatile uint32_t g_w5500_bringup_status = 0xD5010000;

static const CLR_RT_MethodHandler method_lookup[] = 
{
    NULL,
    NULL,
    NULL,
    Library_diseqc_interop_DiseqC_NativeGotoAngle___STATIC__I4__R4,
    Library_diseqc_interop_DiseqC_NativeTransmit___STATIC__I4__SZARRAY_U1,
    Library_diseqc_interop_DiseqC_NativeHalt___STATIC__I4,
    Library_diseqc_interop_DiseqC_NativeDriveEast___STATIC__I4,
    Library_diseqc_interop_DiseqC_NativeDriveWest___STATIC__I4,
    Library_diseqc_interop_DiseqC_NativeStepEast___STATIC__I4__U1,
    Library_diseqc_interop_DiseqC_NativeStepWest___STATIC__I4__U1,
    Library_diseqc_interop_DiseqC_NativeIsBusy___STATIC__BOOLEAN,
    Library_diseqc_interop_DiseqC_NativeGetCurrentAngle___STATIC__R4,
    Library_diseqc_interop_DiseqC_NativeSetBringupStatus___STATIC__VOID__U4,
    Library_diseqc_interop_DiseqC_NativeGetBringupStatus___STATIC__U4,
    Library_diseqc_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4,
    Library_diseqc_interop_W5500Socket_NativeConfigureNetwork___STATIC__I4__STRING__STRING__STRING__STRING,
    Library_diseqc_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4,
    Library_diseqc_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4,
    Library_diseqc_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4,
    Library_diseqc_interop_W5500Socket_NativeClose___STATIC__I4__I4,
    Library_diseqc_interop_W5500Socket_NativeIsConnected___STATIC__BOOLEAN__I4,
};

extern const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_DiSEqC_Control_Interop =
{
    "DiSEqC_Control.Interop",
    0x12345678,  // TODO: Generate proper checksum
    method_lookup,
    { 1, 0, 0, 0 }  // Version 1.0.0.0
};

/* =============================================================================
 * DiSEqC Native Functions
 * ========================================================================== */

/**
 * @brief Native GotoAngle implementation
 * C# signature: public static extern int NativeGotoAngle(float angle);
 */
HRESULT Library_diseqc_interop_DiseqC_NativeGotoAngle___STATIC__I4__R4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    // Get angle parameter from C#
    float angle = stack.Arg0().NumericByRef().r4;
    
    // Call native function
    diseqc_status_t status = diseqc_goto_angle(angle);
    
    // Return status to C#
    stack.SetResult_I4((int32_t)status);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native Transmit implementation
 * C# signature: public static extern int NativeTransmit(byte[] data);
 */
HRESULT Library_diseqc_interop_DiseqC_NativeTransmit___STATIC__I4__SZARRAY_U1(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    CLR_RT_HeapBlock* pArgs = &(stack.Arg0());
    CLR_RT_HeapBlock_Array* dataArray;
    uint8_t* data;
    uint32_t length;
    diseqc_status_t status = DISEQC_OK;
    
    // Get byte array from C#
    dataArray = pArgs[0].DereferenceArray();
    FAULT_ON_NULL(dataArray);
    
    data = (uint8_t*)dataArray->GetFirstElement();
    length = dataArray->m_numOfElements;
    
    if (length > DISEQC_MAX_BYTES) {
        NANOCLR_SET_AND_LEAVE(CLR_E_OUT_OF_RANGE);
    }
    
    // Call native function
    status = diseqc_transmit(data, (uint8_t)length);
    
    // Return status to C#
    stack.SetResult_I4((int32_t)status);
    
    NANOCLR_NOCLEANUP();
}

/**
 * @brief Native Halt implementation
 * C# signature: public static extern int NativeHalt();
 */
HRESULT Library_diseqc_interop_DiseqC_NativeHalt___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    diseqc_status_t status = diseqc_halt();

    stack.SetResult_I4((int32_t)status);

    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native DriveEast implementation
 * C# signature: public static extern int NativeDriveEast();
 */
HRESULT Library_diseqc_interop_DiseqC_NativeDriveEast___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    diseqc_status_t status = diseqc_drive_east();

    stack.SetResult_I4((int32_t)status);

    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native DriveWest implementation
 * C# signature: public static extern int NativeDriveWest();
 */
HRESULT Library_diseqc_interop_DiseqC_NativeDriveWest___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    diseqc_status_t status = diseqc_drive_west();

    stack.SetResult_I4((int32_t)status);

    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native StepEast implementation
 * C# signature: public static extern int NativeStepEast(byte steps);
 */
HRESULT Library_diseqc_interop_DiseqC_NativeStepEast___STATIC__I4__U1(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    uint8_t steps = (uint8_t)stack.Arg0().NumericByRef().u1;

    diseqc_status_t status = diseqc_step_east(steps);

    stack.SetResult_I4((int32_t)status);

    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native StepWest implementation
 * C# signature: public static extern int NativeStepWest(byte steps);
 */
HRESULT Library_diseqc_interop_DiseqC_NativeStepWest___STATIC__I4__U1(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    uint8_t steps = (uint8_t)stack.Arg0().NumericByRef().u1;

    diseqc_status_t status = diseqc_step_west(steps);

    stack.SetResult_I4((int32_t)status);

    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native IsBusy implementation
 * C# signature: public static extern bool NativeIsBusy();
 */
HRESULT Library_diseqc_interop_DiseqC_NativeIsBusy___STATIC__BOOLEAN(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    bool busy = diseqc_is_busy();
    
    stack.SetResult_Boolean(busy);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native GetCurrentAngle implementation
 * C# signature: public static extern float NativeGetCurrentAngle();
 */
HRESULT Library_diseqc_interop_DiseqC_NativeGetCurrentAngle___STATIC__R4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    float angle = diseqc_get_current_angle();
    
    stack.SetResult_R4(angle);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_diseqc_interop_DiseqC_NativeSetBringupStatus___STATIC__VOID__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_w5500_bringup_status = stack.Arg0().NumericByRef().u4;

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_diseqc_interop_DiseqC_NativeGetBringupStatus___STATIC__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_U4(g_w5500_bringup_status);

    NANOCLR_NOCLEANUP_NOLABEL();
}
