/**
 * @file lnb_control.cpp
 * @brief LNB Control Implementation via I2C for LNBH26PQR
 */

#include "lnb_control.h"
#include "board_diseqc.h"
#include <string.h>

/* Global LNB handle */
static lnb_handle_t g_lnb;
static bool g_lnb_initialized = false;

/* I2C timeout */
#define I2C_TIMEOUT_MS              100

/**
 * @brief Write to LNBH26 control register
 */
static lnb_status_t lnb_write_control(lnb_handle_t *hlnb)
{
    uint8_t tx_buf[2];

    tx_buf[0] = LNBH26_REG_CONTROL;
    tx_buf[1] = hlnb->control_reg;

    msg_t status = i2cMasterTransmitTimeout(
        hlnb->i2c_driver,
        hlnb->i2c_addr,
        tx_buf,
        2,
        NULL,
        0,
        TIME_MS2I(I2C_TIMEOUT_MS)
    );

    if (status != MSG_OK) {
        return LNB_ERROR_I2C;
    }

    return LNB_OK;
}

/**
 * @brief Read from LNBH26 register
 */
static lnb_status_t lnb_read_register(lnb_handle_t *hlnb, uint8_t reg, uint8_t *value)
{
    msg_t status = i2cMasterTransmitTimeout(
        hlnb->i2c_driver,
        hlnb->i2c_addr,
        &reg,
        1,
        value,
        1,
        TIME_MS2I(I2C_TIMEOUT_MS)
    );

    if (status != MSG_OK) {
        return LNB_ERROR_I2C;
    }

    return LNB_OK;
}

/**
 * @brief Initialize LNB control
 */
lnb_status_t lnb_init(lnb_handle_t *hlnb, 
                      I2CDriver *i2c_driver,
                      uint8_t i2c_addr)
{
    if (hlnb == NULL || i2c_driver == NULL) {
        return LNB_ERROR_INVALID_PARAM;
    }

    memset(hlnb, 0, sizeof(lnb_handle_t));

    hlnb->i2c_driver = i2c_driver;
    hlnb->i2c_addr = i2c_addr;

    // Initialize to default: 13V (vertical), no tone (low band), enabled
    hlnb->voltage = LNB_VOLTAGE_13V;
    hlnb->tone_enabled = false;
    hlnb->enabled = true;

    // Build control register:
    // EN=1, VSEL=0 (13V), TONE=0, DiSEqC=1, ILIM=600mA
    hlnb->control_reg = LNBH26_CTRL_EN | LNBH26_CTRL_DISEQC | LNBH26_CTRL_ILIM_600MA;

    // Write initial configuration to LNBH26
    lnb_status_t status = lnb_write_control(hlnb);
    if (status != LNB_OK) {
        return status;
    }

    g_lnb = *hlnb;
    g_lnb_initialized = true;

    return LNB_OK;
}

/**
 * @brief Set LNB voltage
 */
lnb_status_t lnb_set_voltage(lnb_handle_t *hlnb, lnb_voltage_t voltage)
{
    if (hlnb == NULL || !g_lnb_initialized) {
        return LNB_ERROR_NOT_INITIALIZED;
    }

    if (voltage != LNB_VOLTAGE_13V && voltage != LNB_VOLTAGE_18V) {
        return LNB_ERROR_INVALID_PARAM;
    }

    hlnb->voltage = voltage;

    // Update control register
    if (voltage == LNB_VOLTAGE_18V) {
        hlnb->control_reg |= LNBH26_CTRL_VSEL;   // Set bit (18V)
    } else {
        hlnb->control_reg &= ~LNBH26_CTRL_VSEL;  // Clear bit (13V)
    }

    // Write to device
    lnb_status_t status = lnb_write_control(hlnb);

    // Update global state
    g_lnb.voltage = voltage;
    g_lnb.control_reg = hlnb->control_reg;

    return status;
}

/**
 * @brief Set LNB polarization
 */
lnb_status_t lnb_set_polarization(lnb_handle_t *hlnb, lnb_polarization_t polarization)
{
    lnb_voltage_t voltage = (polarization == LNB_POL_VERTICAL) ? 
                            LNB_VOLTAGE_13V : LNB_VOLTAGE_18V;
    return lnb_set_voltage(hlnb, voltage);
}

/**
 * @brief Enable/disable 22kHz tone
 */
lnb_status_t lnb_set_tone(lnb_handle_t *hlnb, bool enable)
{
    if (hlnb == NULL || !g_lnb_initialized) {
        return LNB_ERROR_NOT_INITIALIZED;
    }

    hlnb->tone_enabled = enable;

    // Update control register
    if (enable) {
        hlnb->control_reg |= LNBH26_CTRL_TONE;   // Set bit (tone ON)
    } else {
        hlnb->control_reg &= ~LNBH26_CTRL_TONE;  // Clear bit (tone OFF)
    }

    // Write to device
    lnb_status_t status = lnb_write_control(hlnb);

    // Update global state
    g_lnb.tone_enabled = enable;
    g_lnb.control_reg = hlnb->control_reg;

    return status;
}

/**
 * @brief Set LNB band
 */
lnb_status_t lnb_set_band(lnb_handle_t *hlnb, lnb_band_t band)
{
    bool tone_enable = (band == LNB_BAND_HIGH);
    return lnb_set_tone(hlnb, tone_enable);
}

/**
 * @brief Enable/disable LNB power
 */
lnb_status_t lnb_set_enable(lnb_handle_t *hlnb, bool enable)
{
    if (hlnb == NULL || !g_lnb_initialized) {
        return LNB_ERROR_NOT_INITIALIZED;
    }

    hlnb->enabled = enable;

    // Update control register
    if (enable) {
        hlnb->control_reg |= LNBH26_CTRL_EN;     // Set bit (enable)
    } else {
        hlnb->control_reg &= ~LNBH26_CTRL_EN;    // Clear bit (disable)
    }

    // Write to device
    lnb_status_t status = lnb_write_control(hlnb);

    // Update global state
    g_lnb.enabled = enable;
    g_lnb.control_reg = hlnb->control_reg;

    return status;
}

/**
 * @brief Get current voltage
 */
lnb_voltage_t lnb_get_voltage(lnb_handle_t *hlnb)
{
    if (hlnb == NULL || !g_lnb_initialized) {
        return LNB_VOLTAGE_13V;  // Default
    }
    return hlnb->voltage;
}

/**
 * @brief Get current tone state
 */
bool lnb_get_tone(lnb_handle_t *hlnb)
{
    if (hlnb == NULL || !g_lnb_initialized) {
        return false;
    }
    return hlnb->tone_enabled;
}

/**
 * @brief Get current polarization
 */
lnb_polarization_t lnb_get_polarization(lnb_handle_t *hlnb)
{
    lnb_voltage_t voltage = lnb_get_voltage(hlnb);
    return (voltage == LNB_VOLTAGE_13V) ? LNB_POL_VERTICAL : LNB_POL_HORIZONTAL;
}

/**
 * @brief Get current band
 */
lnb_band_t lnb_get_band(lnb_handle_t *hlnb)
{
    bool tone = lnb_get_tone(hlnb);
    return tone ? LNB_BAND_HIGH : LNB_BAND_LOW;
}

/**
 * @brief Read status register
 */
lnb_status_t lnb_read_status(lnb_handle_t *hlnb, uint8_t *status)
{
    if (hlnb == NULL || !g_lnb_initialized || status == NULL) {
        return LNB_ERROR_INVALID_PARAM;
    }

    return lnb_read_register(hlnb, LNBH26_REG_STATUS, status);
}

/**
 * @brief Get global LNB handle (for C# interop)
 */
lnb_handle_t* lnb_get_global_handle(void)
{
    return &g_lnb;
}
