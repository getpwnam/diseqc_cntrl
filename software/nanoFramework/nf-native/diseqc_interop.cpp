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

/* Library information */
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
    Library_diseqc_interop_MotorEnable_NativeTurnOn___STATIC__VOID__U4,
    Library_diseqc_interop_MotorEnable_NativeStartTracking___STATIC__VOID,
    Library_diseqc_interop_MotorEnable_NativeStopTracking___STATIC__VOID,
    Library_diseqc_interop_MotorEnable_NativeForceOff___STATIC__VOID,
    Library_diseqc_interop_MotorEnable_NativeIsOn___STATIC__BOOLEAN,
};

const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_DiseqC_Interop =
{
    "DiseqC.Interop",
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
    
    // Get byte array from C#
    dataArray = pArgs[0].DereferenceArray();
    FAULT_ON_NULL(dataArray);
    
    data = (uint8_t*)dataArray->GetFirstElement();
    length = dataArray->m_numOfElements;
    
    if (length > DISEQC_MAX_BYTES) {
        NANOCLR_SET_AND_LEAVE(CLR_E_OUT_OF_RANGE);
    }
    
    // Call native function
    diseqc_status_t status = diseqc_transmit(data, (uint8_t)length);
    
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

/* =============================================================================
 * Motor Enable Native Functions
 * ========================================================================== */

/**
 * @brief Native TurnOn implementation
 * C# signature: public static extern void NativeTurnOn(uint travelTimeSec);
 */
HRESULT Library_diseqc_interop_MotorEnable_NativeTurnOn___STATIC__VOID__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    uint32_t travel_time_sec = stack.Arg0().NumericByRef().u4;
    
    motor_enable_turn_on(travel_time_sec);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native StartTracking implementation
 * C# signature: public static extern void NativeStartTracking();
 */
HRESULT Library_diseqc_interop_MotorEnable_NativeStartTracking___STATIC__VOID(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    motor_enable_start_tracking();
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native StopTracking implementation
 * C# signature: public static extern void NativeStopTracking();
 */
HRESULT Library_diseqc_interop_MotorEnable_NativeStopTracking___STATIC__VOID(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    motor_enable_stop_tracking();
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native ForceOff implementation
 * C# signature: public static extern void NativeForceOff();
 */
HRESULT Library_diseqc_interop_MotorEnable_NativeForceOff___STATIC__VOID(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    motor_enable_force_off();
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native IsOn implementation
 * C# signature: public static extern bool NativeIsOn();
 */
HRESULT Library_diseqc_interop_MotorEnable_NativeIsOn___STATIC__BOOLEAN(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    bool is_on = motor_enable_is_on();
    
    stack.SetResult_Boolean(is_on);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}
