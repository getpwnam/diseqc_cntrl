// LNBH26 (LNB) interop glue for nanoFramework
#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>
#include "lnbh26_native.h"

extern volatile uint32_t g_cubley_diag_last_error;

static uint8_t lnb_try_read_reg_or_detail(int32_t reg, uint8_t fallback)
{
    int32_t value = 0;
    const int32_t rc = lnb_native_read_register(reg, &value);

    if (rc == (int32_t)LNB_OK)
    {
        return (uint8_t)(value & 0xFF);
    }

    return fallback;
}

static void lnb_set_out_i4(CLR_RT_HeapBlock& argument, int32_t value)
{
    argument.Dereference()->SetInteger((CLR_INT32)value);
}

// All LNB interop functions previously in cubley_interop.cpp

HRESULT Library_cubley_interop_LNBH26_NativeInit___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    // Entry marker for runtime binding verification.
    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC1 << 16) | ((uint32_t)0x00 << 8) | 0xAA;

    // Always re-run native init so debugger restarts re-arm I2C3 pin mux + driver.
    lnb_status_t status = (lnb_status_t)lnb_native_init();

    if (status != LNB_OK)
    {
        const uint8_t rawDetail = (uint8_t)(lnb_get_last_i2c_msg() & 0xFF);
        // 0xE3 C1 SS DD: LNB native error, stage C1(init), status enum, raw I2C detail byte.
        g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC1 << 16) | ((uint32_t)status << 8) | rawDetail;
    }

    stack.SetResult_I4((int32_t)status);

    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeSetEnable___STATIC__I4__BOOLEAN(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC2 << 16) | ((uint32_t)0x00 << 8) | 0xA1;

    bool enable = stack.Arg0().NumericByRef().u1 != 0;
    lnb_status_t status = (lnb_status_t)lnb_native_set_enable(enable ? 1 : 0);

    // 0xE3 C2 SS DD: LNB set-enable result, DD=DATA1 readback on success or low I2C detail on failure.
    uint8_t detail = (uint8_t)(lnb_get_last_i2c_msg() & 0xFF);
    if (status == LNB_OK)
    {
        detail = lnb_try_read_reg_or_detail((int32_t)LNBH26_REGISTER_DATA1, detail);
    }

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC2 << 16) | ((uint32_t)status << 8) | detail;

    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeReadStatus___STATIC__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    int32_t statusReg = 0;
    lnb_status_t status = (lnb_status_t)lnb_native_read_status(&statusReg);

    CLR_RT_HeapBlock& statusOut = stack.Arg0();
    lnb_set_out_i4(statusOut, statusReg);

    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeReadStatusPair___STATIC__I4__BYREF_I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t status1Reg = 0;
    int32_t status2Reg = 0;
    lnb_status_t status = (lnb_status_t)lnb_native_read_status_pair(&status1Reg, &status2Reg);

    CLR_RT_HeapBlock& status1Out = stack.Arg0();
    CLR_RT_HeapBlock& status2Out = stack.Arg1();
    lnb_set_out_i4(status1Out, status1Reg);
    lnb_set_out_i4(status2Out, status2Reg);

    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeSetPolarization___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC3 << 16) | ((uint32_t)0x00 << 8) | 0xA1;

    int32_t polarization = stack.Arg0().NumericByRef().s4;
    lnb_status_t status = (lnb_status_t)lnb_native_set_polarization(polarization);

    // 0xE3 C3 SS DD: LNB set-polarization result, DD=DATA1 readback on success or low I2C detail on failure.
    uint8_t detail = (uint8_t)(lnb_get_last_i2c_msg() & 0xFF);
    if (status == LNB_OK)
    {
        detail = lnb_try_read_reg_or_detail((int32_t)LNBH26_REGISTER_DATA1, detail);
    }

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC3 << 16) | ((uint32_t)status << 8) | detail;
    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeSetBand___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC4 << 16) | ((uint32_t)0x00 << 8) | 0xA1;

    int32_t band = stack.Arg0().NumericByRef().s4;
    lnb_status_t status = (lnb_status_t)lnb_native_set_band(band);

    // 0xE3 C4 SS DD: LNB set-band result, DD=DATA2 readback on success or low I2C detail on failure.
    uint8_t detail = (uint8_t)(lnb_get_last_i2c_msg() & 0xFF);
    if (status == LNB_OK)
    {
        detail = lnb_try_read_reg_or_detail((int32_t)LNBH26_REGISTER_DATA2, detail);
    }

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC4 << 16) | ((uint32_t)status << 8) | detail;
    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeGetPolarization___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_I4(lnb_native_get_polarization());
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeGetBand___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_I4(lnb_native_get_band());
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeSetPolarizationForChannel___STATIC__I4__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    const int32_t channel = stack.Arg0().NumericByRef().s4;
    const int32_t polarization = stack.Arg1().NumericByRef().s4;
    const int32_t status = lnb_native_set_polarization_for_channel(channel, polarization);

    stack.SetResult_I4(status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeSetBandForChannel___STATIC__I4__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    const int32_t channel = stack.Arg0().NumericByRef().s4;
    const int32_t band = stack.Arg1().NumericByRef().s4;
    const int32_t status = lnb_native_set_band_for_channel(channel, band);

    stack.SetResult_I4(status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeSetLowPowerForChannel___STATIC__I4__I4__BOOLEAN(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    const int32_t channel = stack.Arg0().NumericByRef().s4;
    const int32_t enable = stack.Arg1().NumericByRef().u1 != 0;
    const int32_t status = lnb_native_set_low_power_for_channel(channel, enable);

    stack.SetResult_I4(status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeSetDiseqcInputModeForChannel___STATIC__I4__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    const int32_t channel = stack.Arg0().NumericByRef().s4;
    const int32_t mode = stack.Arg1().NumericByRef().s4;
    const int32_t status = lnb_native_set_diseqc_input_mode_for_channel(channel, mode);

    stack.SetResult_I4(status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeGetPolarizationForChannel___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    const int32_t channel = stack.Arg0().NumericByRef().s4;
    stack.SetResult_I4(lnb_native_get_polarization_for_channel(channel));
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeGetBandForChannel___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    const int32_t channel = stack.Arg0().NumericByRef().s4;
    stack.SetResult_I4(lnb_native_get_band_for_channel(channel));
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeGetLastError___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_I4(lnb_native_get_last_error());
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeGetLastErrorDetail___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    stack.SetResult_I4(lnb_native_get_last_error_detail());
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26Registers_NativeReadRegister___STATIC__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC5 << 16) | ((uint32_t)0x00 << 8) | 0xA1;

    int32_t registerAddress = stack.Arg0().NumericByRef().s4;
    int32_t registerValue = 0;
    lnb_status_t status = (lnb_status_t)lnb_native_read_register(registerAddress, &registerValue);

    // 0xE3 C5 SS DD: LNB read-register result, DD=register byte on success or low I2C detail on failure.
    uint8_t detail = (uint8_t)(registerValue & 0xFF);
    if (status != LNB_OK)
    {
        detail = (uint8_t)(lnb_get_last_i2c_msg() & 0xFF);
    }

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC5 << 16) | ((uint32_t)status << 8) | detail;

    CLR_RT_HeapBlock& registerOut = stack.Arg1();
    lnb_set_out_i4(registerOut, registerValue);

    stack.SetResult_I4((int32_t)status);

    NANOCLR_NOCLEANUP_NOLABEL();
}
