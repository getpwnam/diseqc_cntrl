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
    statusOut.Dereference()->SetInteger((CLR_INT32)statusReg);

    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeSetVoltage___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC3 << 16) | ((uint32_t)0x00 << 8) | 0xA1;

    // Map legacy voltage API onto the constants-only polarization API.
    int32_t voltage = stack.Arg0().NumericByRef().s4;
    int32_t polarizationConstant;

    if (voltage == 0)
    {
        polarizationConstant = (int32_t)LNB_NATIVE_POLARIZATION_VERTICAL;
    }
    else if (voltage == 1)
    {
        polarizationConstant = (int32_t)LNB_NATIVE_POLARIZATION_HORIZONTAL;
    }
    else
    {
        g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC3 << 16) | ((uint32_t)LNB_ERROR_INVALID_PARAM << 8) | 0xEF;
        stack.SetResult_I4((int32_t)LNB_ERROR_INVALID_PARAM);
        NANOCLR_NOCLEANUP_NOLABEL();
    }

    lnb_status_t status = (lnb_status_t)lnb_native_set_polarization(polarizationConstant);

    // 0xE3 C3 SS DD: LNB set-voltage result, DD=DATA1 readback on success or low I2C detail on failure.
    uint8_t detail = (uint8_t)(lnb_get_last_i2c_msg() & 0xFF);
    if (status == LNB_OK)
    {
        detail = lnb_try_read_reg_or_detail((int32_t)LNBH26_REGISTER_DATA1, detail);
    }

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC3 << 16) | ((uint32_t)status << 8) | detail;
    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeSetTone___STATIC__I4__BOOLEAN(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC4 << 16) | ((uint32_t)0x00 << 8) | 0xA1;

    // Map legacy tone API onto the constants-only band API.
    bool enable = stack.Arg0().NumericByRef().u1 != 0;
    const int32_t bandConstant = enable ? (int32_t)LNB_NATIVE_BAND_HIGH : (int32_t)LNB_NATIVE_BAND_LOW;
    lnb_status_t status = (lnb_status_t)lnb_native_set_band(bandConstant);

    // 0xE3 C4 SS DD: LNB set-tone result, DD=DATA2 readback on success or low I2C detail on failure.
    uint8_t detail = (uint8_t)(lnb_get_last_i2c_msg() & 0xFF);
    if (status == LNB_OK)
    {
        detail = lnb_try_read_reg_or_detail((int32_t)LNBH26_REGISTER_DATA2, detail);
    }

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC4 << 16) | ((uint32_t)status << 8) | detail;
    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeGetVoltage___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t polarization = lnb_native_get_polarization();
    int32_t voltage = (polarization == (int32_t)LNB_POL_HORIZONTAL) ? 1 : 0;
    stack.SetResult_I4((int32_t)voltage);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeGetTone___STATIC__BOOLEAN(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t band = lnb_native_get_band();
    stack.SetResult_Boolean(band == (int32_t)LNB_BAND_HIGH);
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
    registerOut.Dereference()->SetInteger((CLR_INT32)registerValue);

    stack.SetResult_I4((int32_t)status);

    NANOCLR_NOCLEANUP_NOLABEL();
}
