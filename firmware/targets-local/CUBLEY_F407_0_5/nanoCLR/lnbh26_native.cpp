#include "lnbh26_native.h"

#include <string.h>

static lnb_handle_t g_lnb;
static bool g_lnb_initialized = false;
static int32_t g_lnb_last_i2c_msg = 0;
static lnb_last_error_t g_lnb_last_error = {0, 0};

// Static SRAM buffers avoid CCM/DMA access faults on STM32F4.
static uint8_t g_lnb_i2c_tx_buf[5];
static uint8_t g_lnb_i2c_reg_addr = 0;
static uint8_t g_lnb_i2c_rx_byte = 0;

static const I2CConfig g_lnb_i2c_config = {
    OPMODE_I2C,
    100000,
    STD_DUTY_CYCLE,
};

static void lnb_set_last_error(int32_t status, int32_t detail)
{
    g_lnb_last_error.status = status;
    g_lnb_last_error.detail = detail;
}

static bool lnb_is_valid_reg(uint8_t reg)
{
    return reg <= (uint8_t)LNBH26_REGISTER_DATA4;
}

static void lnb_configure_i2c3_pins(void)
{
    palSetLineMode(PAL_LINE(GPIOA, 8U), PAL_MODE_ALTERNATE(4) | PAL_STM32_OTYPE_OPENDRAIN | PAL_STM32_PUPDR_PULLUP);
    palSetLineMode(PAL_LINE(GPIOC, 9U), PAL_MODE_ALTERNATE(4) | PAL_STM32_OTYPE_OPENDRAIN | PAL_STM32_PUPDR_PULLUP);

    // LNB fault pin is read-only status input on this board.
    palSetLineMode(PAL_LINE(GPIOC, 8U), PAL_MODE_INPUT_PULLUP);
}

static void lnb_prepare_i2c_bus(I2CDriver *i2c_driver)
{
    if (i2c_driver == &I2CD3)
    {
        lnb_configure_i2c3_pins();
    }

    i2cStart(i2c_driver, &g_lnb_i2c_config);
}

static void lnb_refresh_shadow_registers(lnb_handle_t *hlnb)
{
    uint8_t channelAData1 = LNBH26_DATA1_A_DISABLED;

    if (hlnb->enabled)
    {
        channelAData1 = (hlnb->voltage == LNB_VOLTAGE_18V) ?
            LNBH26_DATA1_A_18V :
            LNBH26_DATA1_A_13V;
    }

    hlnb->data1_reg &= (uint8_t)~LNBH26_DATA1_VSEL_A_MASK;
    hlnb->data1_reg |= channelAData1;

    hlnb->data2_reg &= (uint8_t)~(LNBH26_DATA2_LPM_A | LNBH26_DATA2_EXTM_A);
    if (hlnb->tone_enabled)
    {
        hlnb->data2_reg |= LNBH26_DATA2_TEN_A;
    }
    else
    {
        hlnb->data2_reg &= (uint8_t)~LNBH26_DATA2_TEN_A;
    }
}

static lnb_status_t lnb_write_data_registers(lnb_handle_t *hlnb)
{
    g_lnb_i2c_tx_buf[0] = (uint8_t)LNBH26_REGISTER_DATA1;
    g_lnb_i2c_tx_buf[1] = hlnb->data1_reg;
    g_lnb_i2c_tx_buf[2] = hlnb->data2_reg;
    g_lnb_i2c_tx_buf[3] = hlnb->data3_reg;
    g_lnb_i2c_tx_buf[4] = hlnb->data4_reg;

    msg_t status = i2cMasterTransmitTimeout(
        hlnb->i2c_driver,
        hlnb->i2c_addr,
        g_lnb_i2c_tx_buf,
        5,
        NULL,
        0,
        TIME_MS2I(LNBH26_I2C_TIMEOUT_MS));

    g_lnb_last_i2c_msg = (int32_t)status;

    if (status != MSG_OK)
    {
        lnb_set_last_error(LNB_ERROR_I2C, g_lnb_last_i2c_msg);
        return LNB_ERROR_I2C;
    }

    lnb_set_last_error(LNB_OK, 0);
    return LNB_OK;
}

static lnb_status_t lnb_read_register(lnb_handle_t *hlnb, uint8_t reg, uint8_t *value)
{
    g_lnb_i2c_reg_addr = reg;

    msg_t status = i2cMasterTransmitTimeout(
        hlnb->i2c_driver,
        hlnb->i2c_addr,
        &g_lnb_i2c_reg_addr,
        1,
        &g_lnb_i2c_rx_byte,
        1,
        TIME_MS2I(LNBH26_I2C_TIMEOUT_MS));

    g_lnb_last_i2c_msg = (int32_t)status;

    if (status != MSG_OK)
    {
        lnb_set_last_error(LNB_ERROR_I2C, g_lnb_last_i2c_msg);
        return LNB_ERROR_I2C;
    }

    *value = g_lnb_i2c_rx_byte;
    lnb_set_last_error(LNB_OK, 0);
    return LNB_OK;
}

lnb_status_t lnb_init(lnb_handle_t *hlnb, I2CDriver *i2c_driver, uint8_t i2c_addr)
{
    if (hlnb == NULL || i2c_driver == NULL)
    {
        g_lnb_last_i2c_msg = -127;
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, -127);
        return LNB_ERROR_INVALID_PARAM;
    }

    // Firmware bring-up path is intentionally locked to I2C3 + LNBH26 address.
    if (i2c_driver != &I2CD3 || i2c_addr != LNBH26_I2C_ADDR)
    {
        g_lnb_last_i2c_msg = -126;
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, -126);
        return LNB_ERROR_INVALID_PARAM;
    }

    memset(hlnb, 0, sizeof(lnb_handle_t));

    hlnb->i2c_driver = i2c_driver;
    hlnb->i2c_addr = i2c_addr;
    hlnb->voltage = LNB_VOLTAGE_13V;
    hlnb->tone_enabled = false;
    hlnb->enabled = false;

    lnb_prepare_i2c_bus(i2c_driver);
    lnb_refresh_shadow_registers(hlnb);

    lnb_status_t status = lnb_write_data_registers(hlnb);
    if (status != LNB_OK)
    {
        return status;
    }

    g_lnb = *hlnb;
    g_lnb_initialized = true;
    lnb_set_last_error(LNB_OK, 0);
    return LNB_OK;
}

lnb_status_t lnb_set_enable(lnb_handle_t *hlnb, bool enable)
{
    if (hlnb == NULL || !g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    lnb_handle_t previous = *hlnb;
    hlnb->enabled = enable;
    lnb_refresh_shadow_registers(hlnb);

    lnb_status_t status = lnb_write_data_registers(hlnb);
    if (status != LNB_OK)
    {
        *hlnb = previous;
        return status;
    }

    g_lnb = *hlnb;
    return LNB_OK;
}

lnb_status_t lnb_read_status(lnb_handle_t *hlnb, uint8_t *status)
{
    if (hlnb == NULL || status == NULL)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, -125);
        return LNB_ERROR_INVALID_PARAM;
    }

    if (!g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    return lnb_read_register(hlnb, (uint8_t)LNBH26_REGISTER_STATUS1, status);
}

static lnb_status_t lnb_set_voltage_internal(lnb_handle_t *hlnb, lnb_voltage_t voltage)
{
    if (hlnb == NULL || !g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    if (voltage != LNB_VOLTAGE_13V && voltage != LNB_VOLTAGE_18V)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, (int32_t)voltage);
        return LNB_ERROR_INVALID_PARAM;
    }

    lnb_handle_t previous = *hlnb;
    hlnb->voltage = voltage;
    lnb_refresh_shadow_registers(hlnb);

    lnb_status_t status = lnb_write_data_registers(hlnb);
    if (status != LNB_OK)
    {
        *hlnb = previous;
        return status;
    }

    g_lnb = *hlnb;
    return LNB_OK;
}

lnb_status_t lnb_set_polarization(lnb_handle_t *hlnb, lnb_polarization_t polarization)
{
    if (polarization != LNB_POL_VERTICAL && polarization != LNB_POL_HORIZONTAL)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, (int32_t)polarization);
        return LNB_ERROR_INVALID_PARAM;
    }

    return lnb_set_voltage_internal(
        hlnb,
        (polarization == LNB_POL_VERTICAL) ? LNB_VOLTAGE_13V : LNB_VOLTAGE_18V);
}

static lnb_status_t lnb_set_tone_internal(lnb_handle_t *hlnb, bool enable)
{
    if (hlnb == NULL || !g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    lnb_handle_t previous = *hlnb;
    hlnb->tone_enabled = enable;
    lnb_refresh_shadow_registers(hlnb);

    lnb_status_t status = lnb_write_data_registers(hlnb);
    if (status != LNB_OK)
    {
        *hlnb = previous;
        return status;
    }

    g_lnb = *hlnb;
    return LNB_OK;
}

lnb_status_t lnb_set_band(lnb_handle_t *hlnb, lnb_band_t band)
{
    if (band != LNB_BAND_LOW && band != LNB_BAND_HIGH)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, (int32_t)band);
        return LNB_ERROR_INVALID_PARAM;
    }

    return lnb_set_tone_internal(hlnb, band == LNB_BAND_HIGH);
}

lnb_polarization_t lnb_get_polarization(lnb_handle_t *hlnb)
{
    if (hlnb == NULL || !g_lnb_initialized)
    {
        return LNB_POL_VERTICAL;
    }

    lnb_voltage_t voltage = hlnb->voltage;
    return (voltage == LNB_VOLTAGE_13V) ? LNB_POL_VERTICAL : LNB_POL_HORIZONTAL;
}

lnb_band_t lnb_get_band(lnb_handle_t *hlnb)
{
    if (hlnb == NULL || !g_lnb_initialized)
    {
        return LNB_BAND_LOW;
    }

    bool tone = hlnb->tone_enabled;
    return tone ? LNB_BAND_HIGH : LNB_BAND_LOW;
}

lnb_status_t lnb_read_register_byte(lnb_handle_t *hlnb, uint8_t reg, uint8_t *value)
{
    if (hlnb == NULL || value == NULL)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, -124);
        return LNB_ERROR_INVALID_PARAM;
    }

    if (!g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    if (!lnb_is_valid_reg(reg))
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, (int32_t)reg);
        return LNB_ERROR_INVALID_PARAM;
    }

    return lnb_read_register(hlnb, reg, value);
}

lnb_handle_t *lnb_get_global_handle(void)
{
    return &g_lnb;
}

int32_t lnb_get_last_i2c_msg(void)
{
    return g_lnb_last_i2c_msg;
}

lnb_last_error_t lnb_get_last_error(void)
{
    return g_lnb_last_error;
}

int32_t lnb_native_init(void)
{
    return (int32_t)lnb_init(lnb_get_global_handle(), &I2CD3, LNBH26_I2C_ADDR);
}

int32_t lnb_native_set_enable(int32_t enable)
{
    return (int32_t)lnb_set_enable(lnb_get_global_handle(), enable != 0);
}

int32_t lnb_native_read_status(int32_t *statusRegister)
{
    if (statusRegister == NULL)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, -123);
        return (int32_t)LNB_ERROR_INVALID_PARAM;
    }

    uint8_t statusValue = 0;
    lnb_status_t status = lnb_read_status(lnb_get_global_handle(), &statusValue);
    *statusRegister = (int32_t)statusValue;
    return (int32_t)status;
}

int32_t lnb_native_set_polarization(int32_t polarizationConstant)
{
    if ((uint32_t)polarizationConstant == LNB_NATIVE_POLARIZATION_VERTICAL)
    {
        return (int32_t)lnb_set_polarization(lnb_get_global_handle(), LNB_POL_VERTICAL);
    }

    if ((uint32_t)polarizationConstant == LNB_NATIVE_POLARIZATION_HORIZONTAL)
    {
        return (int32_t)lnb_set_polarization(lnb_get_global_handle(), LNB_POL_HORIZONTAL);
    }

    lnb_set_last_error(LNB_ERROR_INVALID_PARAM, polarizationConstant);
    return (int32_t)LNB_ERROR_INVALID_PARAM;
}

int32_t lnb_native_set_band(int32_t bandConstant)
{
    if ((uint32_t)bandConstant == LNB_NATIVE_BAND_LOW)
    {
        return (int32_t)lnb_set_band(lnb_get_global_handle(), LNB_BAND_LOW);
    }

    if ((uint32_t)bandConstant == LNB_NATIVE_BAND_HIGH)
    {
        return (int32_t)lnb_set_band(lnb_get_global_handle(), LNB_BAND_HIGH);
    }

    lnb_set_last_error(LNB_ERROR_INVALID_PARAM, bandConstant);
    return (int32_t)LNB_ERROR_INVALID_PARAM;
}

int32_t lnb_native_get_polarization(void)
{
    return (int32_t)lnb_get_polarization(lnb_get_global_handle());
}

int32_t lnb_native_get_band(void)
{
    return (int32_t)lnb_get_band(lnb_get_global_handle());
}

int32_t lnb_native_read_register(int32_t registerAddress, int32_t *registerValue)
{
    if (registerAddress < 0 || registerAddress > 0xFF || registerValue == NULL)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, -122);
        return (int32_t)LNB_ERROR_INVALID_PARAM;
    }

    uint8_t rawValue = 0;
    lnb_status_t status = lnb_read_register_byte(lnb_get_global_handle(), (uint8_t)registerAddress, &rawValue);
    *registerValue = (int32_t)rawValue;
    return (int32_t)status;
}

int32_t lnb_native_get_last_error(void)
{
    return g_lnb_last_error.status;
}
