// LNBH26 (LNB) interop glue for nanoFramework
#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>
#include "lnbh26_native.h"
#include "board_cubley.h"

extern volatile uint32_t g_cubley_diag_last_error;

static uint8_t lnb_try_read_reg_or_detail(lnb_handle_t *hlnb, uint8_t reg, uint8_t fallback)
{
    uint8_t value = 0;
    const lnb_status_t rc = lnb_read_register_byte(hlnb, reg, &value);

    if (rc == LNB_OK)
    {
        return value;
    }

    return fallback;
}

// All LNB interop functions previously in cubley_interop.cpp

HRESULT Library_cubley_interop_LNBH26_NativeInit___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    // Entry marker for runtime binding verification.
    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC1 << 16) | ((uint32_t)0x00 << 8) | 0xAA;

    // Always re-run native init so debugger restarts re-arm I2C3 pin mux + driver
    // without requiring a physical MCU reset.
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_status_t status = lnb_init(hlnb, &LNB_I2C_DRIVER, LNB_I2C_ADDRESS);

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
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_status_t status = lnb_set_enable(hlnb, enable);

    // 0xE3 C2 SS DD: LNB set-enable result, DD=DATA1 readback on success or low I2C detail on failure.
    uint8_t detail = (uint8_t)(lnb_get_last_i2c_msg() & 0xFF);
    if (status == LNB_OK)
    {
        detail = lnb_try_read_reg_or_detail(hlnb, (uint8_t)LNBH26_REGISTER_DATA1, detail);
    }

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC2 << 16) | ((uint32_t)status << 8) | detail;

    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeReadStatus___STATIC__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    lnb_handle_t* hlnb = lnb_get_global_handle();
    uint8_t statusReg = 0;
    lnb_status_t status = lnb_read_status(hlnb, &statusReg);

    CLR_RT_HeapBlock& statusOut = stack.Arg0();
    statusOut.Dereference()->SetInteger((CLR_INT32)statusReg);

    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeSetVoltage___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC3 << 16) | ((uint32_t)0x00 << 8) | 0xA1;

    int32_t voltage = stack.Arg0().NumericByRef().s4;
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_status_t status = lnb_set_voltage(hlnb, (lnb_voltage_t)voltage);

    // 0xE3 C3 SS DD: LNB set-voltage result, DD=DATA1 readback on success or low I2C detail on failure.
    uint8_t detail = (uint8_t)(lnb_get_last_i2c_msg() & 0xFF);
    if (status == LNB_OK)
    {
        detail = lnb_try_read_reg_or_detail(hlnb, (uint8_t)LNBH26_REGISTER_DATA1, detail);
    }

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC3 << 16) | ((uint32_t)status << 8) | detail;
    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeSetTone___STATIC__I4__BOOLEAN(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC4 << 16) | ((uint32_t)0x00 << 8) | 0xA1;

    bool enable = stack.Arg0().NumericByRef().u1 != 0;
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_status_t status = lnb_set_tone(hlnb, enable);

    // 0xE3 C4 SS DD: LNB set-tone result, DD=DATA2 readback on success or low I2C detail on failure.
    uint8_t detail = (uint8_t)(lnb_get_last_i2c_msg() & 0xFF);
    if (status == LNB_OK)
    {
        detail = lnb_try_read_reg_or_detail(hlnb, (uint8_t)LNBH26_REGISTER_DATA2, detail);
    }

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC4 << 16) | ((uint32_t)status << 8) | detail;
    stack.SetResult_I4((int32_t)status);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeGetVoltage___STATIC__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    lnb_handle_t* hlnb = lnb_get_global_handle();
    lnb_voltage_t voltage = lnb_get_voltage(hlnb);
    stack.SetResult_I4((int32_t)voltage);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26_NativeGetTone___STATIC__BOOLEAN(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();
    lnb_handle_t* hlnb = lnb_get_global_handle();
    bool tone = lnb_get_tone(hlnb);
    stack.SetResult_Boolean(tone);
    NANOCLR_NOCLEANUP_NOLABEL();
}

HRESULT Library_cubley_interop_LNBH26Registers_NativeReadRegister___STATIC__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC5 << 16) | ((uint32_t)0x00 << 8) | 0xA1;

    int32_t registerAddress = stack.Arg0().NumericByRef().s4;
    lnb_handle_t* hlnb = lnb_get_global_handle();
    uint8_t registerValue = 0;
    lnb_status_t status = lnb_read_register_byte(hlnb, (uint8_t)registerAddress, &registerValue);

    // 0xE3 C5 SS DD: LNB read-register result, DD=register byte on success or low I2C detail on failure.
    uint8_t detail = registerValue;
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
