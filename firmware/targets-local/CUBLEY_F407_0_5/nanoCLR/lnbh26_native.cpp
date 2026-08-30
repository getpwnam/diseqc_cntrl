#include "lnbh26_native.h"
#include "diseqc_transmitter.h"

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

static int32_t lnb_get_i2c_error_detail(I2CDriver *i2c_driver, msg_t status)
{
    if (status == MSG_RESET)
    {
        return (int32_t)i2cGetErrors(i2c_driver);
    }

    return (int32_t)status;
}

static bool lnb_is_valid_reg(uint8_t reg)
{
    return reg <= (uint8_t)LNBH26_REGISTER_DATA4;
}

static bool lnb_is_valid_channel(lnb_channel_t channel)
{
    return channel == LNB_CHANNEL_A || channel == LNB_CHANNEL_B;
}

static int lnb_channel_to_index(lnb_channel_t channel)
{
    return (channel == LNB_CHANNEL_B) ? 1 : 0;
}

static bool lnb_try_validate_channel(lnb_channel_t channel)
{
    if (lnb_is_valid_channel(channel))
    {
        return true;
    }

    lnb_set_last_error(LNB_ERROR_INVALID_PARAM, (int32_t)channel);
    return false;
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
    uint8_t channelBData1 = LNBH26_DATA1_B_DISABLED;

    if (hlnb->enabled[0])
    {
        channelAData1 = (hlnb->voltage[0] == LNB_VOLTAGE_18V) ? LNBH26_DATA1_A_18V : LNBH26_DATA1_A_13V;
    }

    if (hlnb->enabled[1])
    {
        channelBData1 = (hlnb->voltage[1] == LNB_VOLTAGE_18V) ? LNBH26_DATA1_B_18V : LNBH26_DATA1_B_13V;
    }

    hlnb->data1_reg &= (uint8_t)~(LNBH26_DATA1_VSEL_A_MASK | LNBH26_DATA1_VSEL_B_MASK);
    hlnb->data1_reg |= (uint8_t)(channelAData1 | channelBData1);

    hlnb->data2_reg &= (uint8_t)~(LNBH26_DATA2_TEN_A | LNBH26_DATA2_LPM_A | LNBH26_DATA2_EXTM_A |
        LNBH26_DATA2_TEN_B | LNBH26_DATA2_LPM_B | LNBH26_DATA2_EXTM_B);

    if (hlnb->tone_enabled[0])
    {
        hlnb->data2_reg |= LNBH26_DATA2_TEN_A;
    }

    if (hlnb->low_power_enabled[0])
    {
        hlnb->data2_reg |= LNBH26_DATA2_LPM_A;
    }

    if (hlnb->diseqc_input_mode[0] == LNB_DISEQC_INPUT_ENABLED)
    {
        hlnb->data2_reg |= LNBH26_DATA2_EXTM_A;
    }

    if (hlnb->tone_enabled[1])
    {
        hlnb->data2_reg |= LNBH26_DATA2_TEN_B;
    }

    if (hlnb->low_power_enabled[1])
    {
        hlnb->data2_reg |= LNBH26_DATA2_LPM_B;
    }

    if (hlnb->diseqc_input_mode[1] == LNB_DISEQC_INPUT_ENABLED)
    {
        hlnb->data2_reg |= LNBH26_DATA2_EXTM_B;
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
        lnb_set_last_error(LNB_ERROR_I2C, lnb_get_i2c_error_detail(hlnb->i2c_driver, status));
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
        lnb_set_last_error(LNB_ERROR_I2C, lnb_get_i2c_error_detail(hlnb->i2c_driver, status));
        return LNB_ERROR_I2C;
    }

    *value = g_lnb_i2c_rx_byte;
    lnb_set_last_error(LNB_OK, 0);
    return LNB_OK;
}

lnb_status_t lnb_init(lnb_handle_t *hlnb, I2CDriver *i2c_driver, uint8_t i2c_addr)
{
    int index;

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

    for (index = 0; index < 2; index++)
    {
        hlnb->voltage[index] = LNB_VOLTAGE_13V;
        hlnb->tone_enabled[index] = false;
        hlnb->low_power_enabled[index] = false;
        hlnb->diseqc_input_mode[index] = LNB_DISEQC_INPUT_DISABLED;
        hlnb->enabled[index] = false;
    }

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

lnb_status_t lnb_set_enable_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, bool enable)
{
    if (hlnb == NULL || !g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    if (!lnb_try_validate_channel(channel))
    {
        return LNB_ERROR_INVALID_PARAM;
    }

    lnb_handle_t previous = *hlnb;
    hlnb->enabled[lnb_channel_to_index(channel)] = enable;
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

lnb_status_t lnb_set_enable(lnb_handle_t *hlnb, bool enable)
{
    return lnb_set_enable_for_channel(hlnb, LNB_CHANNEL_A, enable);
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

lnb_status_t lnb_read_status_pair(lnb_handle_t *hlnb, uint8_t *status1, uint8_t *status2)
{
    lnb_status_t status;

    if (hlnb == NULL || status1 == NULL || status2 == NULL)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, -121);
        return LNB_ERROR_INVALID_PARAM;
    }

    if (!g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    status = lnb_read_register(hlnb, (uint8_t)LNBH26_REGISTER_STATUS1, status1);
    if (status != LNB_OK)
    {
        return status;
    }

    return lnb_read_register(hlnb, (uint8_t)LNBH26_REGISTER_STATUS2, status2);
}

static lnb_status_t lnb_set_voltage_for_channel_internal(lnb_handle_t *hlnb, lnb_channel_t channel, lnb_voltage_t voltage)
{
    if (hlnb == NULL || !g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    if (!lnb_try_validate_channel(channel))
    {
        return LNB_ERROR_INVALID_PARAM;
    }

    if (voltage != LNB_VOLTAGE_13V && voltage != LNB_VOLTAGE_18V)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, (int32_t)voltage);
        return LNB_ERROR_INVALID_PARAM;
    }

    lnb_handle_t previous = *hlnb;
    hlnb->voltage[lnb_channel_to_index(channel)] = voltage;
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

lnb_status_t lnb_set_polarization_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, lnb_polarization_t polarization)
{
    if (polarization != LNB_POL_VERTICAL && polarization != LNB_POL_HORIZONTAL)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, (int32_t)polarization);
        return LNB_ERROR_INVALID_PARAM;
    }

    return lnb_set_voltage_for_channel_internal(
        hlnb,
        channel,
        (polarization == LNB_POL_VERTICAL) ? LNB_VOLTAGE_13V : LNB_VOLTAGE_18V);
}

lnb_status_t lnb_set_polarization(lnb_handle_t *hlnb, lnb_polarization_t polarization)
{
    return lnb_set_polarization_for_channel(hlnb, LNB_CHANNEL_A, polarization);
}

static void lnb_sync_data2_shadow(lnb_handle_t *hlnb, uint8_t data2)
{
    hlnb->data2_reg = data2;
    hlnb->tone_enabled[0] = (data2 & LNBH26_DATA2_TEN_A) != 0U;
    hlnb->low_power_enabled[0] = (data2 & LNBH26_DATA2_LPM_A) != 0U;
    hlnb->diseqc_input_mode[0] = (data2 & LNBH26_DATA2_EXTM_A) != 0U
        ? LNB_DISEQC_INPUT_ENABLED
        : LNB_DISEQC_INPUT_DISABLED;
    hlnb->tone_enabled[1] = (data2 & LNBH26_DATA2_TEN_B) != 0U;
    hlnb->low_power_enabled[1] = (data2 & LNBH26_DATA2_LPM_B) != 0U;
    hlnb->diseqc_input_mode[1] = (data2 & LNBH26_DATA2_EXTM_B) != 0U
        ? LNB_DISEQC_INPUT_ENABLED
        : LNB_DISEQC_INPUT_DISABLED;
}

lnb_status_t lnb_set_band_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, lnb_band_t band)
{
    if (hlnb == NULL || !g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    if (!lnb_try_validate_channel(channel))
    {
        return LNB_ERROR_INVALID_PARAM;
    }

    if (band != LNB_BAND_LOW && band != LNB_BAND_HIGH)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, (int32_t)band);
        return LNB_ERROR_INVALID_PARAM;
    }

    const int index = lnb_channel_to_index(channel);
    const bool highBand = band == LNB_BAND_HIGH;
    lnb_handle_t previous = *hlnb;

    if (channel == LNB_CHANNEL_A)
    {
        const diseqc_tx_status_t pinStatus = diseqc_set_envelope_idle(highBand);
        if (pinStatus != DISEQC_TX_OK)
        {
            lnb_set_last_error(LNB_ERROR_HARDWARE, (int32_t)pinStatus);
            return LNB_ERROR_HARDWARE;
        }
    }

    // Band selection always establishes the LNBH26 internal-generator mode:
    // EXTM=0, TEN=1 and DSQIN=high for high band; TEN=0 and DSQIN=low for low.
    // DiSEqC transmission may subsequently select EXTM=1 while it owns PD12.
    hlnb->tone_enabled[index] = highBand;
    hlnb->diseqc_input_mode[index] = LNB_DISEQC_INPUT_DISABLED;
    lnb_refresh_shadow_registers(hlnb);

    lnb_status_t status = lnb_write_data_registers(hlnb);
    if (status != LNB_OK)
    {
        *hlnb = previous;
        if (channel == LNB_CHANNEL_A)
        {
            const bool previousInternalTone =
                previous.tone_enabled[0] && previous.diseqc_input_mode[0] == LNB_DISEQC_INPUT_DISABLED;
            (void)diseqc_set_envelope_idle(previousInternalTone);
        }
        return status;
    }

    uint8_t data2Readback = 0;
    status = lnb_read_register(hlnb, (uint8_t)LNBH26_REGISTER_DATA2, &data2Readback);
    if (status != LNB_OK)
    {
        g_lnb = *hlnb;
        return status;
    }

    const uint8_t toneMask = channel == LNB_CHANNEL_A ? LNBH26_DATA2_TEN_A : LNBH26_DATA2_TEN_B;
    const uint8_t extmMask = channel == LNB_CHANNEL_A ? LNBH26_DATA2_EXTM_A : LNBH26_DATA2_EXTM_B;
    const uint8_t verifyMask = (uint8_t)(toneMask | extmMask);
    if ((data2Readback & verifyMask) != (hlnb->data2_reg & verifyMask))
    {
        lnb_sync_data2_shadow(hlnb, data2Readback);
        if (channel == LNB_CHANNEL_A)
        {
            const bool actualInternalTone =
                (data2Readback & toneMask) != 0U && (data2Readback & extmMask) == 0U;
            (void)diseqc_set_envelope_idle(actualInternalTone);
        }
        g_lnb = *hlnb;
        lnb_set_last_error(LNB_ERROR_I2C, -117);
        return LNB_ERROR_I2C;
    }

    g_lnb = *hlnb;
    return LNB_OK;
}

lnb_status_t lnb_set_band(lnb_handle_t *hlnb, lnb_band_t band)
{
    return lnb_set_band_for_channel(hlnb, LNB_CHANNEL_A, band);
}

lnb_status_t lnb_set_low_power_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, bool enable)
{
    if (hlnb == NULL || !g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    if (!lnb_try_validate_channel(channel))
    {
        return LNB_ERROR_INVALID_PARAM;
    }

    lnb_handle_t previous = *hlnb;
    hlnb->low_power_enabled[lnb_channel_to_index(channel)] = enable;
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

lnb_status_t lnb_set_diseqc_input_mode_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, lnb_diseqc_input_mode_t mode)
{
    if (hlnb == NULL || !g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    if (!lnb_try_validate_channel(channel))
    {
        return LNB_ERROR_INVALID_PARAM;
    }

    if (mode != LNB_DISEQC_INPUT_DISABLED && mode != LNB_DISEQC_INPUT_ENABLED)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, (int32_t)mode);
        return LNB_ERROR_INVALID_PARAM;
    }

    lnb_handle_t previous = *hlnb;
    hlnb->diseqc_input_mode[lnb_channel_to_index(channel)] = mode;
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

static lnb_status_t lnb_update_data3_bit_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, uint8_t maskA, uint8_t maskB, bool setBit)
{
    if (hlnb == NULL || !g_lnb_initialized)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, 0);
        return LNB_ERROR_NOT_INITIALIZED;
    }

    if (!lnb_try_validate_channel(channel))
    {
        return LNB_ERROR_INVALID_PARAM;
    }

    uint8_t bitMask = (channel == LNB_CHANNEL_A) ? maskA : maskB;
    lnb_handle_t previous = *hlnb;

    if (setBit)
    {
        hlnb->data3_reg |= bitMask;
    }
    else
    {
        hlnb->data3_reg &= (uint8_t)~bitMask;
    }

    lnb_status_t status = lnb_write_data_registers(hlnb);
    if (status != LNB_OK)
    {
        *hlnb = previous;
        return status;
    }

    g_lnb = *hlnb;
    return LNB_OK;
}

lnb_status_t lnb_set_iset_low_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, bool lowRange)
{
    return lnb_update_data3_bit_for_channel(hlnb, channel, LNBH26_DATA3_ISET_A, LNBH26_DATA3_ISET_B, lowRange);
}

lnb_status_t lnb_set_isw_low_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, bool lowLimit)
{
    return lnb_update_data3_bit_for_channel(hlnb, channel, LNBH26_DATA3_ISW_A, LNBH26_DATA3_ISW_B, lowLimit);
}

int32_t lnb_get_iset_low_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel)
{
    if (hlnb == NULL || !g_lnb_initialized || !lnb_is_valid_channel(channel))
    {
        return 0;
    }

    uint8_t bitMask = (channel == LNB_CHANNEL_A) ? LNBH26_DATA3_ISET_A : LNBH26_DATA3_ISET_B;
    return ((hlnb->data3_reg & bitMask) != 0) ? 1 : 0;
}

int32_t lnb_get_isw_low_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel)
{
    if (hlnb == NULL || !g_lnb_initialized || !lnb_is_valid_channel(channel))
    {
        return 0;
    }

    uint8_t bitMask = (channel == LNB_CHANNEL_A) ? LNBH26_DATA3_ISW_A : LNBH26_DATA3_ISW_B;
    return ((hlnb->data3_reg & bitMask) != 0) ? 1 : 0;
}

lnb_polarization_t lnb_get_polarization_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel)
{
    int index;

    if (hlnb == NULL || !g_lnb_initialized || !lnb_is_valid_channel(channel))
    {
        return LNB_POL_VERTICAL;
    }

    index = lnb_channel_to_index(channel);
    return (hlnb->voltage[index] == LNB_VOLTAGE_13V) ? LNB_POL_VERTICAL : LNB_POL_HORIZONTAL;
}

lnb_polarization_t lnb_get_polarization(lnb_handle_t *hlnb)
{
    return lnb_get_polarization_for_channel(hlnb, LNB_CHANNEL_A);
}

lnb_band_t lnb_get_band_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel)
{
    int index;

    if (hlnb == NULL || !g_lnb_initialized || !lnb_is_valid_channel(channel))
    {
        return LNB_BAND_LOW;
    }

    index = lnb_channel_to_index(channel);
    return hlnb->tone_enabled[index] ? LNB_BAND_HIGH : LNB_BAND_LOW;
}

lnb_band_t lnb_get_band(lnb_handle_t *hlnb)
{
    return lnb_get_band_for_channel(hlnb, LNB_CHANNEL_A);
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

static lnb_status_t lnb_native_parse_channel(int32_t channelConstant, lnb_channel_t *channel)
{
    if (channel == NULL)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, -120);
        return LNB_ERROR_INVALID_PARAM;
    }

    if (channelConstant == LNB_NATIVE_CHANNEL_A)
    {
        *channel = LNB_CHANNEL_A;
        return LNB_OK;
    }

    if (channelConstant == LNB_NATIVE_CHANNEL_B)
    {
        *channel = LNB_CHANNEL_B;
        return LNB_OK;
    }

    lnb_set_last_error(LNB_ERROR_INVALID_PARAM, channelConstant);
    return LNB_ERROR_INVALID_PARAM;
}

int32_t lnb_native_init(void)
{
    return (int32_t)lnb_init(lnb_get_global_handle(), &I2CD3, LNBH26_I2C_ADDR);
}

int32_t lnb_native_set_enable(int32_t enable)
{
    return (int32_t)lnb_set_enable(lnb_get_global_handle(), enable != 0);
}

int32_t lnb_native_set_enable_for_channel(int32_t channelConstant, int32_t enable)
{
    lnb_channel_t channel;
    lnb_status_t channelStatus = lnb_native_parse_channel(channelConstant, &channel);
    if (channelStatus != LNB_OK)
    {
        return (int32_t)channelStatus;
    }

    return (int32_t)lnb_set_enable_for_channel(lnb_get_global_handle(), channel, enable != 0);
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

int32_t lnb_native_read_status_pair(int32_t *status1Register, int32_t *status2Register)
{
    if (status1Register == NULL || status2Register == NULL)
    {
        lnb_set_last_error(LNB_ERROR_INVALID_PARAM, -119);
        return (int32_t)LNB_ERROR_INVALID_PARAM;
    }

    if (!g_lnb_initialized)
    {
        lnb_status_t initStatus = lnb_init(lnb_get_global_handle(), &I2CD3, LNBH26_I2C_ADDR);
        if (initStatus != LNB_OK)
        {
            return (int32_t)initStatus;
        }
    }

    uint8_t status1 = 0;
    uint8_t status2 = 0;
    lnb_handle_t *handle = lnb_get_global_handle();
    if (handle == NULL)
    {
        lnb_set_last_error(LNB_ERROR_NOT_INITIALIZED, -118);
        return (int32_t)LNB_ERROR_NOT_INITIALIZED;
    }

    lnb_status_t status = lnb_read_register_byte(handle, (uint8_t)LNBH26_REGISTER_STATUS1, &status1);
    if (status == LNB_OK)
    {
        status = lnb_read_register_byte(handle, (uint8_t)LNBH26_REGISTER_STATUS2, &status2);
    }

    // If we still observe an unexpected invalid-parameter result, retry through
    // the register wrapper path used by NativeReadRegister.
    if (status == LNB_ERROR_INVALID_PARAM)
    {
        int32_t fallbackStatus1 = 0;
        int32_t fallbackStatus2 = 0;
        int32_t rc1 = lnb_native_read_register((int32_t)LNBH26_REGISTER_STATUS1, &fallbackStatus1);
        int32_t rc2 = lnb_native_read_register((int32_t)LNBH26_REGISTER_STATUS2, &fallbackStatus2);

        if (rc1 == (int32_t)LNB_OK && rc2 == (int32_t)LNB_OK)
        {
            status1 = (uint8_t)(fallbackStatus1 & 0xFF);
            status2 = (uint8_t)(fallbackStatus2 & 0xFF);
            status = LNB_OK;
        }
    }

    *status1Register = (int32_t)status1;
    *status2Register = (int32_t)status2;
    return (int32_t)status;
}

int32_t lnb_native_set_polarization_for_channel(int32_t channelConstant, int32_t polarizationConstant)
{
    lnb_channel_t channel;
    lnb_status_t status = lnb_native_parse_channel(channelConstant, &channel);
    if (status != LNB_OK)
    {
        return (int32_t)status;
    }

    if (polarizationConstant == LNB_NATIVE_POLARIZATION_VERTICAL)
    {
        return (int32_t)lnb_set_polarization_for_channel(lnb_get_global_handle(), channel, LNB_POL_VERTICAL);
    }

    if (polarizationConstant == LNB_NATIVE_POLARIZATION_HORIZONTAL)
    {
        return (int32_t)lnb_set_polarization_for_channel(lnb_get_global_handle(), channel, LNB_POL_HORIZONTAL);
    }

    lnb_set_last_error(LNB_ERROR_INVALID_PARAM, polarizationConstant);
    return (int32_t)LNB_ERROR_INVALID_PARAM;
}

int32_t lnb_native_set_polarization(int32_t polarizationConstant)
{
    return lnb_native_set_polarization_for_channel(LNB_NATIVE_CHANNEL_A, polarizationConstant);
}

int32_t lnb_native_set_band_for_channel(int32_t channelConstant, int32_t bandConstant)
{
    lnb_channel_t channel;
    lnb_status_t status = lnb_native_parse_channel(channelConstant, &channel);
    if (status != LNB_OK)
    {
        return (int32_t)status;
    }

    if (bandConstant == LNB_NATIVE_BAND_LOW)
    {
        return (int32_t)lnb_set_band_for_channel(lnb_get_global_handle(), channel, LNB_BAND_LOW);
    }

    if (bandConstant == LNB_NATIVE_BAND_HIGH)
    {
        return (int32_t)lnb_set_band_for_channel(lnb_get_global_handle(), channel, LNB_BAND_HIGH);
    }

    lnb_set_last_error(LNB_ERROR_INVALID_PARAM, bandConstant);
    return (int32_t)LNB_ERROR_INVALID_PARAM;
}

int32_t lnb_native_set_band(int32_t bandConstant)
{
    return lnb_native_set_band_for_channel(LNB_NATIVE_CHANNEL_A, bandConstant);
}

int32_t lnb_native_set_low_power_for_channel(int32_t channelConstant, int32_t enable)
{
    lnb_channel_t channel;
    lnb_status_t status = lnb_native_parse_channel(channelConstant, &channel);
    if (status != LNB_OK)
    {
        return (int32_t)status;
    }

    return (int32_t)lnb_set_low_power_for_channel(lnb_get_global_handle(), channel, enable != 0);
}

int32_t lnb_native_set_diseqc_input_mode_for_channel(int32_t channelConstant, int32_t modeConstant)
{
    lnb_channel_t channel;
    lnb_status_t status = lnb_native_parse_channel(channelConstant, &channel);
    if (status != LNB_OK)
    {
        return (int32_t)status;
    }

    if (modeConstant == LNB_NATIVE_DISEQC_INPUT_DISABLED)
    {
        return (int32_t)lnb_set_diseqc_input_mode_for_channel(lnb_get_global_handle(), channel, LNB_DISEQC_INPUT_DISABLED);
    }

    if (modeConstant == LNB_NATIVE_DISEQC_INPUT_ENABLED)
    {
        return (int32_t)lnb_set_diseqc_input_mode_for_channel(lnb_get_global_handle(), channel, LNB_DISEQC_INPUT_ENABLED);
    }

    lnb_set_last_error(LNB_ERROR_INVALID_PARAM, modeConstant);
    return (int32_t)LNB_ERROR_INVALID_PARAM;
}

int32_t lnb_native_set_iset_low_for_channel(int32_t channelConstant, int32_t lowRange)
{
    lnb_channel_t channel;
    lnb_status_t status = lnb_native_parse_channel(channelConstant, &channel);
    if (status != LNB_OK)
    {
        return (int32_t)status;
    }

    return (int32_t)lnb_set_iset_low_for_channel(lnb_get_global_handle(), channel, lowRange != 0);
}

int32_t lnb_native_set_isw_low_for_channel(int32_t channelConstant, int32_t lowLimit)
{
    lnb_channel_t channel;
    lnb_status_t status = lnb_native_parse_channel(channelConstant, &channel);
    if (status != LNB_OK)
    {
        return (int32_t)status;
    }

    return (int32_t)lnb_set_isw_low_for_channel(lnb_get_global_handle(), channel, lowLimit != 0);
}

int32_t lnb_native_get_polarization_for_channel(int32_t channelConstant)
{
    lnb_channel_t channel;
    lnb_status_t status = lnb_native_parse_channel(channelConstant, &channel);
    if (status != LNB_OK)
    {
        return (int32_t)LNB_POL_VERTICAL;
    }

    return (int32_t)lnb_get_polarization_for_channel(lnb_get_global_handle(), channel);
}

int32_t lnb_native_get_polarization(void)
{
    return lnb_native_get_polarization_for_channel(LNB_NATIVE_CHANNEL_A);
}

int32_t lnb_native_get_band_for_channel(int32_t channelConstant)
{
    lnb_channel_t channel;
    lnb_status_t status = lnb_native_parse_channel(channelConstant, &channel);
    if (status != LNB_OK)
    {
        return (int32_t)LNB_BAND_LOW;
    }

    return (int32_t)lnb_get_band_for_channel(lnb_get_global_handle(), channel);
}

int32_t lnb_native_get_band(void)
{
    return lnb_native_get_band_for_channel(LNB_NATIVE_CHANNEL_A);
}

int32_t lnb_native_get_iset_low_for_channel(int32_t channelConstant)
{
    lnb_channel_t channel;
    lnb_status_t status = lnb_native_parse_channel(channelConstant, &channel);
    if (status != LNB_OK)
    {
        return 0;
    }

    return lnb_get_iset_low_for_channel(lnb_get_global_handle(), channel);
}

int32_t lnb_native_get_isw_low_for_channel(int32_t channelConstant)
{
    lnb_channel_t channel;
    lnb_status_t status = lnb_native_parse_channel(channelConstant, &channel);
    if (status != LNB_OK)
    {
        return 0;
    }

    return lnb_get_isw_low_for_channel(lnb_get_global_handle(), channel);
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

int32_t lnb_native_get_last_error_detail(void)
{
    return g_lnb_last_error.detail;
}
