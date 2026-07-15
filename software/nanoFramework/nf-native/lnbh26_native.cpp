/**
 * @file lnbh26_native.cpp
 * @brief LNB Control Implementation via I2C for LNBH26PQR
 */

#include "lnbh26_native.h"
#include "board_cubley.h"
#include <sys_dev_i2c_native_target.h>
#include <string.h>

extern volatile uint32_t g_cubley_diag_current_status;
extern volatile uint32_t g_cubley_diag_last_error;

// Managed I2C target config provides ConfigPins_I2C1(); the LNB path reuses
// the existing I2C3 pin mux helper from the DiSEqC native surface.
extern void ConfigPins_I2C3(void);

/* Global LNB handle */
static lnb_handle_t g_lnb;
static bool g_lnb_initialized = false;
static int32_t g_lnb_last_i2c_msg = 0;

// DMA on STM32F4 cannot access CCM stack (0x1000xxxx). Keep I2C transfer
// buffers in static SRAM-backed storage to avoid DMA failure hard halts.
static uint8_t g_lnb_i2c_tx_buf[2];
static uint8_t g_lnb_i2c_reg_addr;
static uint8_t g_lnb_i2c_rx_byte;

/* I2C timeout */
#define I2C_TIMEOUT_MS              100

static const I2CConfig g_lnb_i2c_config = {
    OPMODE_I2C,
    100000,
    STD_DUTY_CYCLE
};

static void lnb_prepare_i2c_bus(I2CDriver *i2c_driver)
{
    if (i2c_driver == &I2CD3)
    {
        ConfigPins_I2C3();
    }
    else if (i2c_driver == &I2CD1)
    {
        ConfigPins_I2C1();
    }

    i2cStart(i2c_driver, &g_lnb_i2c_config);
}

bool lnb_is_initialized(void)
{
    return g_lnb_initialized;
}

int32_t lnb_get_last_i2c_msg(void)
{
    return g_lnb_last_i2c_msg;
}

/**
 * @brief Write to LNBH26 control register
 */
static lnb_status_t lnb_write_control(lnb_handle_t *hlnb)
{
    g_lnb_i2c_tx_buf[0] = LNBH26_REG_CONTROL;
    g_lnb_i2c_tx_buf[1] = hlnb->control_reg;

    msg_t status = i2cMasterTransmitTimeout(
        hlnb->i2c_driver,
        hlnb->i2c_addr,
        g_lnb_i2c_tx_buf,
        2,
        NULL,
        0,
        TIME_MS2I(I2C_TIMEOUT_MS)
    );

    g_lnb_last_i2c_msg = (int32_t)status;

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
    g_lnb_i2c_reg_addr = reg;

    msg_t status = i2cMasterTransmitTimeout(
        hlnb->i2c_driver,
        hlnb->i2c_addr,
        &g_lnb_i2c_reg_addr,
        1,
        &g_lnb_i2c_rx_byte,
        1,
        TIME_MS2I(I2C_TIMEOUT_MS)
    );

    if (status != MSG_OK) {
        return LNB_ERROR_I2C;
    }

    *value = g_lnb_i2c_rx_byte;

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
        g_lnb_last_i2c_msg = -127;
        return LNB_ERROR_INVALID_PARAM;
    }

    memset(hlnb, 0, sizeof(lnb_handle_t));

    hlnb->i2c_driver = i2c_driver;
    hlnb->i2c_addr = i2c_addr;

    lnb_prepare_i2c_bus(i2c_driver);

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
        // Record detailed init failure telemetry for SWD mailbox reads.
        // current_status: 0xD5 C1 0E XX (LNB_INIT, FAIL, raw I2C msg low byte)
        // last_error:     0xE3 C1 SS XX (LNB native, LNB_INIT, status enum, raw I2C msg low byte)
        const uint8_t rawDetail = (uint8_t)(g_lnb_last_i2c_msg & 0xFF);
        g_cubley_diag_current_status = ((uint32_t)0xD5 << 24) | ((uint32_t)0xC1 << 16) | ((uint32_t)0x0E << 8) | rawDetail;
        g_cubley_diag_last_error = ((uint32_t)0xE3 << 24) | ((uint32_t)0xC1 << 16) | ((uint32_t)status << 8) | rawDetail;
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

    if (status == LNB_OK) {
        // Update global state only after successful device write.
        g_lnb.voltage = voltage;
        g_lnb.control_reg = hlnb->control_reg;
    }

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

    if (status == LNB_OK) {
        // Update global state only after successful device write.
        g_lnb.tone_enabled = enable;
        g_lnb.control_reg = hlnb->control_reg;
    }

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

    if (status == LNB_OK) {
        // Update global state only after successful device write.
        g_lnb.enabled = enable;
        g_lnb.control_reg = hlnb->control_reg;
    }

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
