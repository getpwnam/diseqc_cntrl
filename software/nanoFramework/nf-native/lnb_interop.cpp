/**
 * @file lnb_interop.cpp
 * @brief nanoFramework interop for LNB control
 */

#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>
#include "lnb_control.h"

/**
 * @brief Native SetVoltage implementation
 * C# signature: public static extern int NativeSetVoltage(int voltage);
 */
HRESULT Library_lnb_interop_LNB_NativeSetVoltage___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    int32_t voltage = stack.Arg0().NumericByRef().s4;
    
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_status_t status = lnb_set_voltage(hlnb, (lnb_voltage_t)voltage);
    
    stack.SetResult_I4((int32_t)status);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native SetPolarization implementation
 * C# signature: public static extern int NativeSetPolarization(int polarization);
 */
HRESULT Library_lnb_interop_LNB_NativeSetPolarization___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    int32_t polarization = stack.Arg0().NumericByRef().s4;
    
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_status_t status = lnb_set_polarization(hlnb, (lnb_polarization_t)polarization);
    
    stack.SetResult_I4((int32_t)status);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native SetTone implementation
 * C# signature: public static extern int NativeSetTone(bool enable);
 */
HRESULT Library_lnb_interop_LNB_NativeSetTone___STATIC__I4__BOOLEAN(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    bool enable = stack.Arg0().NumericByRef().u1 != 0;
    
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_status_t status = lnb_set_tone(hlnb, enable);
    
    stack.SetResult_I4((int32_t)status);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native SetBand implementation
 * C# signature: public static extern int NativeSetBand(int band);
 */
HRESULT Library_lnb_interop_LNB_NativeSetBand___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    int32_t band = stack.Arg0().NumericByRef().s4;
    
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_status_t status = lnb_set_band(hlnb, (lnb_band_t)band);
    
    stack.SetResult_I4((int32_t)status);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native GetVoltage implementation
 * C# signature: public static extern int NativeGetVoltage();
 */
HRESULT Library_lnb_interop_LNB_NativeGetVoltage___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_voltage_t voltage = lnb_get_voltage(hlnb);
    
    stack.SetResult_I4((int32_t)voltage);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native GetTone implementation
 * C# signature: public static extern bool NativeGetTone();
 */
HRESULT Library_lnb_interop_LNB_NativeGetTone___STATIC__BOOLEAN(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    lnb_handle_t* hlnb = lnb_get_global_handle();
    bool tone = lnb_get_tone(hlnb);
    
    stack.SetResult_Boolean(tone);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native GetPolarization implementation
 * C# signature: public static extern int NativeGetPolarization();
 */
HRESULT Library_lnb_interop_LNB_NativeGetPolarization___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_polarization_t polarization = lnb_get_polarization(hlnb);
    
    stack.SetResult_I4((int32_t)polarization);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}

/**
 * @brief Native GetBand implementation
 * C# signature: public static extern int NativeGetBand();
 */
HRESULT Library_lnb_interop_LNB_NativeGetBand___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_band_t band = lnb_get_band(hlnb);
    
    stack.SetResult_I4((int32_t)band);
    
    NANOCLR_NOCLEANUP_NOLABEL();
}
